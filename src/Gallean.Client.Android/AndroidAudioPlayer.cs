using System.Runtime.InteropServices;
using Android.Media;
using NAudio.Wave;

namespace Gallean.Client.Android;

/// <summary>
/// Plays 32-bit float PCM from NAudio through an Android <see cref="AudioTrack"/>.
/// </summary>
internal sealed class AndroidAudioPlayer : IWavePlayer
{
    private const int BufferMilliseconds = 60;

    private readonly object gate = new();
    private IWaveProvider? source;
    private AudioTrack? track;
    private Thread? thread;
    private volatile bool stopRequested;
    private float volume = 1;

    public event EventHandler<StoppedEventArgs>? PlaybackStopped;

    public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

    public WaveFormat OutputWaveFormat => source?.WaveFormat ?? throw new InvalidOperationException("Not initialized.");

    public float Volume
    {
        get => volume;
        set
        {
            volume = Math.Clamp(value, 0, 1);
            track?.SetVolume(volume);
        }
    }

    public void Init(IWaveProvider waveProvider)
    {
        WaveFormat format = waveProvider.WaveFormat;

        if (format.Encoding != WaveFormatEncoding.IeeeFloat || format.BitsPerSample != 32)
        {
            throw new ArgumentException("Only 32-bit float PCM is supported.", nameof(waveProvider));
        }

        if (format.Channels is not (1 or 2))
        {
            throw new ArgumentException("Only mono and stereo are supported.", nameof(waveProvider));
        }

        source = waveProvider;

        ChannelOut channelMask = format.Channels == 1 ? ChannelOut.Mono : ChannelOut.Stereo;
        int minBufferBytes = AudioTrack.GetMinBufferSize(format.SampleRate, channelMask, Encoding.PcmFloat);
        int desiredBytes = format.AverageBytesPerSecond * BufferMilliseconds / 1000;

        track = new AudioTrack.Builder()
            .SetAudioAttributes(new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Game)!
                .SetContentType(AudioContentType.Music)!
                .Build()!)
            .SetAudioFormat(new AudioFormat.Builder()
                .SetEncoding(Encoding.PcmFloat)!
                .SetSampleRate(format.SampleRate)!
                .SetChannelMask(channelMask)!
                .Build()!)
            .SetBufferSizeInBytes(Math.Max(minBufferBytes, desiredBytes))
            .SetTransferMode(AudioTrackMode.Stream)
            .SetPerformanceMode(AudioTrackPerformanceMode.LowLatency)
            .Build();

        track.SetVolume(volume);
    }

    public void Play()
    {
        lock (gate)
        {
            if (track is null || source is null)
            {
                throw new InvalidOperationException("Not initialized.");
            }

            if (PlaybackState == PlaybackState.Playing)
            {
                return;
            }

            track.Play();
            PlaybackState = PlaybackState.Playing;

            if (thread is null)
            {
                stopRequested = false;
                thread = new Thread(PlaybackLoop)
                {
                    IsBackground = true,
                    Name = "Gallean audio",
                    Priority = ThreadPriority.AboveNormal
                };
                thread.Start();
            }
        }
    }

    public void Pause()
    {
        lock (gate)
        {
            if (PlaybackState == PlaybackState.Playing)
            {
                track?.Pause();
                PlaybackState = PlaybackState.Paused;
            }
        }
    }

    public void Stop()
    {
        Thread? playbackThread;

        lock (gate)
        {
            stopRequested = true;
            playbackThread = thread;
            PlaybackState = PlaybackState.Stopped;
        }

        if (playbackThread is not null && playbackThread != Thread.CurrentThread)
        {
            playbackThread.Join();
        }
    }

    public void Dispose()
    {
        Stop();
        track?.Release();
        track?.Dispose();
        track = null;
    }

    private void PlaybackLoop()
    {
        Exception? error = null;

        try
        {
            IWaveProvider provider = source!;
            AudioTrack audioTrack = track!;
            WaveFormat format = provider.WaveFormat;
            int frameBytes = format.BlockAlign;
            int chunkBytes = format.AverageBytesPerSecond * 10 / 1000 / frameBytes * frameBytes;
            byte[] bytes = new byte[chunkBytes];
            float[] samples = new float[chunkBytes / sizeof(float)];

            while (!stopRequested)
            {
                if (PlaybackState == PlaybackState.Paused)
                {
                    Thread.Sleep(10);
                    continue;
                }

                int read = provider.Read(bytes.AsSpan());

                if (read <= 0)
                {
                    break;
                }

                int count = read / sizeof(float);
                MemoryMarshal.Cast<byte, float>(bytes.AsSpan(0, count * sizeof(float))).CopyTo(samples);

                int offset = 0;

                while (offset < count && !stopRequested)
                {
                    int written = audioTrack.Write(samples, offset, count - offset, WriteMode.Blocking);

                    if (written < 0)
                    {
                        throw new InvalidOperationException($"AudioTrack write failed with code {written}.");
                    }

                    offset += written;
                }
            }

            audioTrack.Stop();
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            lock (gate)
            {
                thread = null;
                PlaybackState = PlaybackState.Stopped;
            }

            PlaybackStopped?.Invoke(this, new StoppedEventArgs(error));
        }
    }
}
