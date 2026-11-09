using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Gallean.Client;

/// <summary>
/// Touch input for the battle: a tap acts when the finger lifts, so that holding the finger shows details
/// (like the right mouse button) without also acting.
/// </summary>
internal sealed partial class BattleScreenView
{
    private int? touchPointerId;
    private Control? touchControl;
    private BattleGridTile? touchTile;
    private bool touchDetailsShown;

    private void InitializeTouch()
    {
        AddHandler(PointerPressedEvent, OnTouchPointerPressed, RoutingStrategies.Tunnel, true);
        AddHandler(PointerReleasedEvent, OnTouchPointerReleased, RoutingStrategies.Tunnel, true);
        AddHandler(HoldingEvent, OnTouchHolding, RoutingStrategies.Bubble, true);
    }

    private void OnTouchPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch)
        {
            HideTouchDetails();
            touchPointerId = e.Pointer.Id;
            touchControl = null;
            touchTile = null;
        }
    }

    private void SetTouchTarget(Control control, BattleGridTile tile)
    {
        touchControl = control;
        touchTile = tile;
    }

    private void OnTouchPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch || e.Pointer.Id != touchPointerId)
        {
            return;
        }

        Control? control = touchControl;
        BattleGridTile? tile = touchTile;
        bool detailsShown = touchDetailsShown;

        touchPointerId = null;

        if (detailsShown)
        {
            HideTouchDetails();
            touchControl = null;
            touchTile = null;
            e.Handled = true;
        }
        else if (control is not null && tile is not null && IsStillOnTarget(control, tile, e))
        {
            touchControl = null;
            touchTile = null;
            ViewModel.ClickAsync(tile).FileAndForget();
        }
    }

    private bool IsStillOnTarget(Control control, BattleGridTile tile, PointerEventArgs e)
    {
        if (ReferenceEquals(control, Battlefield))
        {
            return Battlefield.HitTestPointer(e) == tile;
        }

        return new Rect(control.Bounds.Size).Contains(e.GetPosition(control));
    }

    private void OnTouchHolding(object? sender, HoldingRoutedEventArgs e)
    {
        if (e.HoldingState != HoldingState.Started || touchPointerId is null || touchDetailsShown)
        {
            return;
        }

        if (ReferenceEquals(touchControl, Battlefield))
        {
            if (touchTile is not null && ViewModel.TryShowUnitDetails(touchTile))
            {
                BattlefieldUnitDetails.IsOpen = true;
                touchDetailsShown = true;
                e.Handled = true;
            }

            return;
        }

        for (Visual? current = e.Source as Visual; current is not null; current = current.GetVisualParent())
        {
            if (current is Panel panel && panel.Children.OfType<Popup>().Any() && TryShowTouchDetails(panel))
            {
                SetDetailsOpen(panel, true);
                touchControl = panel;
                touchDetailsShown = true;
                e.Handled = true;

                return;
            }
        }
    }

    private bool TryShowTouchDetails(Panel panel)
    {
        switch (panel.DataContext)
        {
            case BattleGridUnitViewModel unit:
                ViewModel.ShowUnitDetails(unit.UnitId);
                return true;
            case BattleGridTileViewModel tile:
                return ViewModel.TryShowUnitDetails(tile.Tile);
            case BattleItemViewModel item:
                ViewModel.ShowItemDetails(item.Slot);
                return true;
            default:
                return false;
        }
    }

    private void HideTouchDetails()
    {
        if (touchDetailsShown)
        {
            touchDetailsShown = false;
            ViewModel.HideUnitDetails();
            ViewModel.HideItemDetails();
            BattlefieldUnitDetails.IsOpen = false;
            SetDetailsOpen(touchControl, false);
        }
    }
}
