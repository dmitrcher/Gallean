using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;

namespace Gallean.Client;

internal sealed partial class FatalErrorView : UserControl
{
    public FatalErrorView()
    {
        InitializeComponent();
    }

    public FatalErrorView(string title, string message, string? logLocation)
        : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
        LogLocationText.Text = logLocation ?? Tr("<No log>");
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
        else if (ClientPlatform.Exit is { } exit)
        {
            exit();
        }
        else if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Close();
        }
    }
}
