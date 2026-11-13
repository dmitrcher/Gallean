using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Platform;

namespace Gallean.Client;

internal sealed partial class GameView : Window
{
    private const double SceneHeight = 600.0;

    public static readonly StyledProperty<bool> IsWindowedProperty =
        AvaloniaProperty.Register<GameView, bool>(nameof(IsWindowed), defaultValue: true, defaultBindingMode: BindingMode.TwoWay);

    private readonly Size minSize;
    private Task? exitSaveTask;

    public GameView()
    {
        InitializeComponent();

        minSize = new Size(MinWidth, MinHeight);

        ScalingChanged += OnScalingChanged;
        PositionChanged += OnPositionChanged;
        Resized += OnResized;
    }

    private GameViewModel? ViewModel => (GameViewModel?)DataContext;

    internal ItemsControl ScreenHost => Surface.ScreenHost;

    public bool IsWindowed
    {
        get => GetValue(IsWindowedProperty);
        set => SetValue(IsWindowedProperty, value);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        FitToScreen();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        GameViewModel? game = ViewModel;

        if (e.IsProgrammatic || game is null)
        {
            return;
        }

        e.Cancel = true;

        if (exitSaveTask is null)
        {
            exitSaveTask = SaveAndCloseAsync(game);
            exitSaveTask.FileAndForget();
        }
    }

    private async Task SaveAndCloseAsync(GameViewModel game)
    {
        await game.WriteExitSaveAsync();

        Close();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == IsWindowedProperty)
        {
            bool windowed = WindowState != WindowState.FullScreen;

            if (IsWindowed != windowed)
            {
                WindowState = IsWindowed ? WindowState.Normal : WindowState.FullScreen;
            }
        }
        else if (e.Property == WindowStateProperty)
        {
            bool windowed = WindowState != WindowState.FullScreen;

            if (WindowState != WindowState.Minimized)
            {
                SetCurrentValue(IsWindowedProperty, windowed);
            }

            FitToScreen();
        }
    }

    private void OnScalingChanged(object? sender, EventArgs e)
    {
        FitToScreen();
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        FitToScreen();
    }

    private void OnResized(object? sender, WindowResizedEventArgs e)
    {
        FitToScreen();
    }

    private void FitToScreen()
    {
        Screen? screen = Screens.ScreenFromWindow(this);

        if (screen is null)
        {
            return;
        }

        PixelRect area = WindowState == WindowState.FullScreen ? screen.Bounds : screen.WorkingArea;
        PixelSize areaPixels = area.Size;
        Size areaSize = areaPixels.ToSize(DesktopScaling);
        Size frameSize = FrameSize ?? ClientSize;
        Size decorationSize = frameSize - ClientSize;
        Size maxClientSize = areaSize - decorationSize;

        MinWidth = Math.Min(minSize.Width, maxClientSize.Width);
        MinHeight = Math.Min(minSize.Height, maxClientSize.Height);
        Surface.MinContentWidth = MinWidth;
        Surface.MinContentHeight = MinHeight;

        if (WindowState == WindowState.Normal)
        {
            Size requestedSize = new(Width, Height);

            if (requestedSize.Width > maxClientSize.Width || requestedSize.Height > maxClientSize.Height)
            {
                Size clientSize = requestedSize.Constrain(maxClientSize);
                PixelSize pixelSize = PixelSize.FromSize(clientSize + decorationSize, DesktopScaling);

                Width = clientSize.Width;
                Height = clientSize.Height;
                Position = new PixelPoint(
                    Math.Max(area.X, Math.Min(Position.X, area.Right - pixelSize.Width)),
                    Math.Max(area.Y, Math.Min(Position.Y, area.Bottom - pixelSize.Height)));
            }
        }

        UpdateAssetPixelScale(screen);
    }

    private void UpdateAssetPixelScale(Screen screen)
    {
        IAssetProvider? assets = AssetScope.GetOptionalAssets(this);

        if (assets is not null)
        {
            int maxPixelScale = (int)Math.Ceiling(screen.Bounds.Height / SceneHeight);

            assets.SetMaxPixelScale(maxPixelScale);
        }
    }
}
