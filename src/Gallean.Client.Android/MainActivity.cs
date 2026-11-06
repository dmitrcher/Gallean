using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Android;

namespace Gallean.Client.Android;

[Activity(
    Label = "Gallean",
    Theme = "@style/Gallean.Theme",
    Icon = "@drawable/icon",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTask,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ResizeableActivity = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize | ConfigChanges.UiMode | ConfigChanges.Density |
                           ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Navigation)]
public sealed class MainActivity : AvaloniaMainActivity
{
    private static MainActivity? current;

    /// <summary>
    /// Ends the game like closing the window does on desktop: the process goes away and the next launch starts fresh.
    /// </summary>
    public static void ExitApplication()
    {
        ClientPlatform.NotifyExiting();

        MainActivity? activity = current;

        if (activity is not null)
        {
            activity.RunOnUiThread(() =>
            {
                activity.FinishAndRemoveTask();
                global::Android.OS.Process.KillProcess(global::Android.OS.Process.MyPid());
            });
        }
        else
        {
            global::Android.OS.Process.KillProcess(global::Android.OS.Process.MyPid());
        }
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        current = this;

        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        EnterImmersiveMode();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);

        if (hasFocus)
        {
            EnterImmersiveMode();
        }
    }

    protected override void OnStop()
    {
        ClientPlatform.NotifySuspending();

        base.OnStop();
    }

    protected override void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }

        base.OnDestroy();
    }

    private void EnterImmersiveMode()
    {
        if (Window is null)
        {
            return;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            Window.SetDecorFitsSystemWindows(false);

            if (Window.InsetsController is { } controller)
            {
                controller.Hide(WindowInsets.Type.SystemBars());
                controller.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
            }
        }
        else
        {
#pragma warning disable CA1422
            Window.DecorView.SystemUiFlags =
                SystemUiFlags.ImmersiveSticky | SystemUiFlags.Fullscreen | SystemUiFlags.HideNavigation |
                SystemUiFlags.LayoutFullscreen | SystemUiFlags.LayoutHideNavigation | SystemUiFlags.LayoutStable;
#pragma warning restore CA1422
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(28) && Window.Attributes is { } attributes)
        {
            attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
            Window.Attributes = attributes;
        }
    }
}
