using NAudio.Wave;

namespace Gallean.Client;

/// <summary>
/// Hooks that a platform head (for example the Android app) sets before the application starts.
/// Desktop heads leave them unset and get the default behavior.
/// </summary>
public static class ClientPlatform
{
    /// <summary>
    /// Creates the sound output device when the built-in Windows, Linux and macOS devices do not apply.
    /// </summary>
    public static Func<IWavePlayer>? CreateAudioDevice { get; set; }

    /// <summary>
    /// Leaves the application when there is no desktop lifetime to shut down.
    /// </summary>
    public static Action? Exit { get; set; }

    /// <summary>
    /// The folder shown to the player as the place to put the asset pack.
    /// Defaults to the folder of the game executable.
    /// </summary>
    public static string? AssetPackDirectory { get; set; }

    /// <summary>
    /// Whether the asset pack screen imports a picked pack file instead of opening the asset folder,
    /// for platforms where that folder is hard to reach.
    /// </summary>
    public static bool ImportsAssetPack { get; set; }

    /// <summary>
    /// Whether the main input is touch, so the mouse cursor is not drawn.
    /// </summary>
    public static bool IsTouchFirst { get; set; }

    /// <summary>
    /// The GPU resource cache limit for Skia. Mobile GPUs share memory with the system and need a smaller cache.
    /// </summary>
    public static long? MaxGpuResourceSizeBytes { get; set; }

    /// <summary>
    /// Prepares platform data (for example unpacks the game data) while the game starts.
    /// It completes before any game data is read.
    /// </summary>
    public static Func<Task>? PrepareAsync { get; set; }

    /// <summary>
    /// Raised by the platform head when the application goes to the background and may be killed.
    /// </summary>
    public static event EventHandler? Suspending;

    /// <summary>
    /// Raised by the platform head when the application is about to end.
    /// </summary>
    public static event EventHandler? Exiting;

    public static void NotifySuspending()
    {
        Suspending?.Invoke(null, EventArgs.Empty);
    }

    public static void NotifyExiting()
    {
        Exiting?.Invoke(null, EventArgs.Empty);
    }
}
