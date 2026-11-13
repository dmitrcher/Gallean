using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Gallean.Client;

/// <summary>
/// The game content, independent of how it is hosted: in a desktop window or in a single mobile view.
/// </summary>
internal sealed partial class GameSurface : UserControl
{
    private const double SceneHeight = 600.0;

    public static readonly StyledProperty<AbstractScreenViewModel?> TopScreenProperty =
        AvaloniaProperty.Register<GameSurface, AbstractScreenViewModel?>(nameof(TopScreen));

    public static readonly StyledProperty<double> MinContentWidthProperty =
        AvaloniaProperty.Register<GameSurface, double>(nameof(MinContentWidth), 1280);

    public static readonly StyledProperty<double> MinContentHeightProperty =
        AvaloniaProperty.Register<GameSurface, double>(nameof(MinContentHeight), 720);

    public static readonly StyledProperty<bool> ShowsCursorProperty =
        AvaloniaProperty.Register<GameSurface, bool>(nameof(ShowsCursor), true);

    public static readonly StyledProperty<Language> LanguageProperty =
        AvaloniaProperty.Register<GameSurface, Language>(nameof(Language));

    private Point? touchPressPosition;
    private bool detailsShownByHold;

    public GameSurface()
    {
        InitializeComponent();

        AddHandler(PointerPressedEvent, OnDetailsPointerPressed, RoutingStrategies.Tunnel, true);
        AddHandler(PointerReleasedEvent, OnDetailsPointerReleased, RoutingStrategies.Tunnel, true);
        AddHandler(PointerCaptureLostEvent, OnDetailsPointerCaptureLost, RoutingStrategies.Tunnel, true);
        AddHandler(HoldingEvent, OnHolding, RoutingStrategies.Bubble, true);
    }

    private GameViewModel? ViewModel => (GameViewModel?)DataContext;

    public AbstractScreenViewModel? TopScreen
    {
        get => GetValue(TopScreenProperty);
        set => SetValue(TopScreenProperty, value);
    }

    public double MinContentWidth
    {
        get => GetValue(MinContentWidthProperty);
        set => SetValue(MinContentWidthProperty, value);
    }

    public double MinContentHeight
    {
        get => GetValue(MinContentHeightProperty);
        set => SetValue(MinContentHeightProperty, value);
    }

    public Language Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    /// <summary>
    /// Whether the animated game cursor is drawn. Touch-only hosts turn it off.
    /// </summary>
    public bool ShowsCursor
    {
        get => GetValue(ShowsCursorProperty);
        set => SetValue(ShowsCursorProperty, value);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);

        if (e.Source is Visual focused)
        {
            focused.DetachedFromVisualTree += OnFocusedDetached;
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);

        if (e.Source is Visual focused)
        {
            focused.DetachedFromVisualTree -= OnFocusedDetached;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!e.Handled && TopScreen is not null)
        {
            Control? container = ScreenHost.ContainerFromItem(TopScreen);

            if (container is ContentPresenter { Child: not null } presenter)
            {
                foreach (KeyBinding binding in presenter.Child.KeyBindings)
                {
                    if (e.Handled)
                    {
                        break;
                    }

                    binding.TryHandle(e);
                }
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == TopScreenProperty)
        {
            FocusTopScreen();
        }
        else if (e.Property == LanguageProperty)
        {
            UpdateBindingTargets();
        }
        else if (e.Property == AssetScope.AssetsProperty || e.Property == BoundsProperty)
        {
            UpdateAssetPixelScale();
        }
    }

    /// <summary>
    /// Limits the decoded asset size by the screen when the surface is the whole app view, as on mobile.
    /// A desktop window does this itself by the screen it is on.
    /// </summary>
    private void UpdateAssetPixelScale()
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        IAssetProvider? assets = AssetScope.GetOptionalAssets(this);

        if (topLevel is null || topLevel is Window || assets is null)
        {
            return;
        }

        double pixelHeight = topLevel.ClientSize.Height * topLevel.RenderScaling;

        if (pixelHeight > 0)
        {
            assets.SetMaxPixelScale((int)Math.Ceiling(pixelHeight / SceneHeight));
        }
    }

    private void UpdateBindingTargets()
    {
        HashSet<AvaloniaObject> visited = [];
        Stack<AvaloniaObject> stack = new();

        stack.Push(this);

        while (stack.TryPop(out AvaloniaObject? target))
        {
            if (visited.Add(target))
            {
                Type type = target.GetType();

                UpdateBindingTargets(target, AvaloniaPropertyRegistry.Instance.GetRegistered(type));
                UpdateBindingTargets(target, AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(type));
                UpdateBindingTargets(target, AvaloniaPropertyRegistry.Instance.GetRegisteredDirect(type));

                if (target is Visual visual)
                {
                    foreach (Visual child in visual.GetVisualChildren())
                    {
                        stack.Push(child);
                    }
                }

                if (target is ILogical logical)
                {
                    foreach (ILogical child in logical.LogicalChildren)
                    {
                        if (child is AvaloniaObject logicalChild)
                        {
                            stack.Push(logicalChild);
                        }
                    }
                }
            }
        }
    }

    private static void UpdateBindingTargets(AvaloniaObject target, IReadOnlyList<AvaloniaProperty> properties)
    {
        foreach (AvaloniaProperty property in properties)
        {
            BindingExpressionBase? expression = BindingOperations.GetBindingExpressionBase(target, property);

            if (expression is not null)
            {
                if (ReadsNamedElement(expression))
                {
                    continue;
                }
                else
                {
                    expression.UpdateTarget();
                }
            }
        }

        static bool ReadsNamedElement(BindingExpressionBase expression)
        {
            return expression is UntypedBindingExpressionBase untyped && untyped.Description.Contains('#');
        }
    }

    private void OnFocusedDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Visual visual)
        {
            visual.DetachedFromVisualTree -= OnFocusedDetached;
        }

        FocusTopScreen();
    }

    private void OnDetailsPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(this);
        GameViewModel? game = ViewModel;

        touchPressPosition = e.Pointer.Type == PointerType.Touch ? point.Position : null;

        if (point.Properties.IsRightButtonPressed && game is not null)
        {
            object? content = GetDetailsContentAt(point.Position);

            if (content is not null)
            {
                game.ShowDetails(content);
                e.Pointer.Capture(this);
                e.Handled = true;
            }
        }
    }

    private void OnDetailsPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        GameViewModel? game = ViewModel;

        touchPressPosition = null;

        if (detailsShownByHold)
        {
            // A long press shows details instead of clicking what is under the finger.
            detailsShownByHold = false;
            game?.HideDetails();
            e.Handled = true;
        }
        else if (e.InitialPressMouseButton == MouseButton.Right && game is not null)
        {
            game.HideDetails();

            if (ReferenceEquals(e.Pointer.Captured, this))
            {
                e.Pointer.Capture(null);
            }
        }
    }

    private void OnDetailsPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        detailsShownByHold = false;
        ViewModel?.HideDetails();
    }

    private void OnHolding(object? sender, HoldingRoutedEventArgs e)
    {
        GameViewModel? game = ViewModel;

        if (game is null)
        {
            return;
        }

        if (e.HoldingState == HoldingState.Started && touchPressPosition is Point position)
        {
            object? content = GetDetailsContentAt(position);

            if (content is not null)
            {
                game.ShowDetails(content);
                detailsShownByHold = true;
                e.Handled = true;
            }
        }
        else if (e.HoldingState == HoldingState.Canceled && detailsShownByHold)
        {
            detailsShownByHold = false;
            game.HideDetails();
        }
    }

    private object? GetDetailsContentAt(Point position)
    {
        if (TopScreen is not null)
        {
            Control? container = ScreenHost.ContainerFromItem(TopScreen);

            if (container is not null)
            {
                foreach (Visual visual in this.GetVisualsAt(position))
                {
                    IDetailsSource? source = FindDetailsSource(visual, container);

                    if (source is not null)
                    {
                        return source.DetailsContent;
                    }
                }
            }
        }

        return null;
    }

    private static IDetailsSource? FindDetailsSource(Visual visual, Control screen)
    {
        IDetailsSource? source = null;

        for (Visual? current = visual; current is not null; current = GetParent(current))
        {
            if (current == screen)
            {
                return source;
            }

            source ??= current.DataContext as IDetailsSource;
        }

        return null;

        static Visual? GetParent(Visual visual)
        {
            if (visual is OverlayPopupHost)
            {
                return visual.Parent as Visual;
            }
            else
            {
                return visual.GetVisualParent();
            }
        }
    }

    private void FocusTopScreen()
    {
        if (TopScreen is not null)
        {
            Control? container = ScreenHost.ContainerFromItem(TopScreen);

            if (container is ContentPresenter presenter)
            {
                presenter.ApplyTemplate();
                presenter.Child?.Focus();
            }
        }
    }
}
