using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Gallean.Configuration;
using Gallean.Entities.GameData;

namespace Gallean.Client.Android;

[Application(
    Label = "Gallean",
    Icon = "@drawable/icon",
    LargeHeap = true,
    HardwareAccelerated = true)]
public sealed class MainApplication : AvaloniaAndroidApplication<Avalonia.Application>
{
    private const long MaxGpuResourceSizeBytes = 512L * 1024 * 1024;

    public MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CreateAppBuilder()
    {
        ConfigurePlatform();

        return ClientApp
            .Configure(useDirectRendering: false)
            .UseAndroid()
            .With(new AndroidPlatformOptions
            {
                RenderingMode = [AndroidRenderingMode.Egl, AndroidRenderingMode.Software]
            });
    }

    private void ConfigurePlatform()
    {
        // App-specific external storage is reachable over USB and from file managers, so the player can put
        // the asset pack there and copy saves and logs without root.
        string root = GetExternalFilesDir(null)?.AbsolutePath ?? FilesDir!.AbsolutePath;

        AppDirectories.UseRoot(root);
        Directory.CreateDirectory(AppDirectories.Assets);

        // The game data is read often, so it goes to internal storage rather than the slower external one.
        string gameDataDirectory = Path.Combine(FilesDir!.AbsolutePath, RiseOfTheElves.DirectoryName);
        RiseOfTheElves.RootPath = gameDataDirectory;
        ClientPlatform.PrepareAsync = () => Task.Run(() => GameDataInstaller.Install(this, gameDataDirectory));

        ClientPlatform.AssetPackDirectory = AppDirectories.Assets;
        ClientPlatform.ImportsAssetPack = true;
        ClientPlatform.IsTouchFirst = true;
        ClientPlatform.MaxGpuResourceSizeBytes = MaxGpuResourceSizeBytes;
        ClientPlatform.CreateAudioDevice = static () => new AndroidAudioPlayer();
        ClientPlatform.Exit = MainActivity.ExitApplication;
    }
}
