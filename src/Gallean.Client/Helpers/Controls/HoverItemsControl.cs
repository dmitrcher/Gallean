using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Gallean.Client;

internal sealed class HoverItemsControl : ItemsControl
{
    public static readonly StyledProperty<object?> HoveredItemProperty =
        AvaloniaProperty.Register<HoverItemsControl, object?>(nameof(HoveredItem));

    public HoverItemsControl()
    {
        // The items handle their own presses and releases, so these are watched even when handled.
        AddHandler(PointerReleasedEvent, OnAnyPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, OnAnyPointerCaptureLost, RoutingStrategies.Bubble, handledEventsToo: true);

        ItemsView.CollectionChanged += OnItemsChanged;
    }

    public object? HoveredItem
    {
        get => GetValue(HoveredItemProperty);
        private set => SetValue(HoveredItemProperty, value);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        HoveredItem = FindItem(e.Source as Visual);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        HoveredItem = null;
    }

    // A finger or pen does not hover, and it may not leave the control after lifting, so its item is
    // only shown while it is pressed.
    private void OnAnyPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse)
        {
            HoveredItem = null;
        }
    }

    private void OnAnyPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse)
        {
            HoveredItem = null;
        }
    }

    // The hovered item may no longer be shown, and a mouse that stays still would otherwise keep its
    // caption until it moves again.
    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HoveredItem = null;
    }

    private object? FindItem(Visual? source)
    {
        foreach (Visual visual in source?.GetSelfAndVisualAncestors() ?? [])
        {
            if (visual is Control control)
            {
                int index = IndexFromContainer(control);

                if (index >= 0)
                {
                    return Items[index];
                }
            }
        }

        return null;
    }
}
