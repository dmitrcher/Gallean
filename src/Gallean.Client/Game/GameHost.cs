using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Gallean.Configuration;

namespace Gallean.Client;

/// <summary>
/// Shows the game and its error screens, either in desktop windows or in the single view of a mobile app.
/// </summary>
internal interface IGameHost
{
    event EventHandler? Exiting;

    IControlledApplicationLifetime? Lifetime { get; }

    Control ShowGame(GameViewModel game, GameSettings settings);

    void ShowError(Control view);

    void Shutdown();
}

internal sealed class DesktopGameHost : IGameHost
{
    private readonly IClassicDesktopStyleApplicationLifetime desktop;

    public DesktopGameHost(IClassicDesktopStyleApplicationLifetime desktop)
    {
        this.desktop = desktop;

        desktop.Exit += (sender, _) => Exiting?.Invoke(sender, EventArgs.Empty);
    }

    public event EventHandler? Exiting;

    public IControlledApplicationLifetime Lifetime => desktop;

    public Control ShowGame(GameViewModel game, GameSettings settings)
    {
        GameView gameView = new()
        {
            WindowState = settings.Windowed ? WindowState.Normal : WindowState.FullScreen,
            DataContext = game
        };

        desktop.MainWindow = gameView;
        gameView.Show();

        return gameView;
    }

    public void ShowError(Control view)
    {
        Window window = view is AssetPackErrorView ? new AssetPackErrorWindow() : new FatalErrorWindow();
        window.Content = view;

        Window? previousWindow = desktop.MainWindow;
        desktop.MainWindow = window;
        window.Show();
        previousWindow?.Close();
    }

    public void Shutdown()
    {
        desktop.Shutdown();
    }
}

internal sealed class SingleViewGameHost : IGameHost
{
    // The view of a mobile app can be created before the game starts and recreated when the activity is,
    // so it is set once to this root and the screens are swapped inside it.
    private readonly ContentControl root = new()
    {
        Background = Brushes.Black,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Stretch
    };

    private GameViewModel? game;

    public SingleViewGameHost(ISingleViewApplicationLifetime singleView)
    {
        if (singleView is IActivityApplicationLifetime activity)
        {
            activity.MainViewFactory = () => root;
        }
        else
        {
            singleView.MainView = root;
        }

        ClientPlatform.Suspending += OnSuspending;
        ClientPlatform.Exiting += (sender, e) => Exiting?.Invoke(sender, e);
    }

    public event EventHandler? Exiting;

    public IControlledApplicationLifetime? Lifetime => null;

    public Control ShowGame(GameViewModel game, GameSettings settings)
    {
        this.game = game;

        GameSurface surface = new()
        {
            DataContext = game,
            ShowsCursor = !ClientPlatform.IsTouchFirst
        };

        root.Content = surface;
        surface.Focus();

        return surface;
    }

    public void ShowError(Control view)
    {
        // Error screens are sized for small desktop windows, so they are scaled down to fit a phone screen.
        root.Content = new Viewbox
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16),
            Child = new Border
            {
                Width = view is AssetPackErrorView ? 680 : 720,
                Child = view
            }
        };

        game = null;
    }

    public void Shutdown()
    {
        Exiting?.Invoke(this, EventArgs.Empty);
        ClientPlatform.Exit?.Invoke();
    }

    private void OnSuspending(object? sender, EventArgs e)
    {
        game?.WriteExitSaveAsync().FileAndForget();
    }
}
