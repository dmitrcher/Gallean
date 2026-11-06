using Avalonia;
using Avalonia.Input;

namespace Gallean.Client;

/// <summary>
/// Touch input for the world map: tap to click, drag to pan, pinch to zoom, and long press for the position info
/// that a right click shows.
/// </summary>
internal sealed partial class WorldVisual
{
    private const double TouchSlopDips = 10.0;
    private const double DoubleTapDips = 24.0;
    private static readonly TimeSpan DoubleTapTime = TimeSpan.FromMilliseconds(400);

    private readonly Dictionary<IPointer, Point> touches = [];
    private Point? tapStart;
    private bool touchPanning;
    private bool touchPinched;
    private bool touchInfoShown;
    private double pinchStartDistance;
    private double pinchStartZoom;
    private Point? lastTapPosition;
    private DateTime lastTapTime;

    private static bool IsTouch(PointerEventArgs e)
    {
        return e.Pointer.Type == PointerType.Touch;
    }

    private double LocalDips(double dips)
    {
        // The world is laid out in scaled units, so screen distances are converted to local ones.
        double factor = UiScaleDecorator.GetScalingFactor(this);

        return factor > 0 ? dips / factor : dips;
    }

    private void OnTouchPressed(PointerPressedEventArgs e)
    {
        Point position = e.GetPosition(this);

        touches[e.Pointer] = position;
        e.Pointer.Capture(this);
        e.Handled = true;

        if (touches.Count == 1)
        {
            tapStart = position;
            touchPanning = false;
            touchPinched = false;
            touchInfoShown = false;
            panAnchor = camera.ScreenToWorld(position);

            UpdateHoverAt(position);
        }
        else if (touches.Count == 2)
        {
            (Point first, Point second) = GetTwoTouches();

            tapStart = null;
            touchPinched = true;
            pinchStartDistance = Math.Max(1, Point.Distance(first, second));
            pinchStartZoom = camera.TargetZoom;
            panAnchor = camera.ScreenToWorld(Midpoint(first, second));
            HideTouchInfo();
        }
    }

    private void OnTouchMoved(PointerEventArgs e)
    {
        if (!touches.ContainsKey(e.Pointer))
        {
            return;
        }

        Point position = e.GetPosition(this);
        touches[e.Pointer] = position;

        if (touches.Count == 1 && panAnchor is not null)
        {
            if (!touchPanning && tapStart is { } start && Point.Distance(start, position) > LocalDips(TouchSlopDips))
            {
                touchPanning = true;
                HideTouchInfo();
            }

            if (touchPanning || touchPinched)
            {
                camera.Move(panAnchor.Value - camera.ScreenToWorld(position));
                redrawRequested = true;
            }
        }
        else if (touches.Count >= 2)
        {
            (Point first, Point second) = GetTwoTouches();
            Point center = Midpoint(first, second);
            double distance = Math.Max(1, Point.Distance(first, second));

            camera.ZoomTo(pinchStartZoom * distance / pinchStartDistance, center);

            if (panAnchor is not null)
            {
                camera.Move(panAnchor.Value - camera.ScreenToWorld(center));
            }

            redrawRequested = true;
        }

        e.Handled = true;
    }

    private void OnTouchReleased(PointerReleasedEventArgs e)
    {
        if (!touches.Remove(e.Pointer, out Point position))
        {
            return;
        }

        // Releasing the capture raises capture lost synchronously, which must not see this pointer as active.
        e.Pointer.Capture(null);
        e.Handled = true;

        if (touches.Count == 0)
        {
            bool isTap = tapStart is not null && !touchPanning && !touchPinched && !touchInfoShown;

            HideTouchInfo();
            panAnchor = null;
            tapStart = null;

            if (isTap && ViewModel.IsActive)
            {
                Tap(position);
            }
        }
        else if (touches.Count == 1)
        {
            // One finger left after a pinch keeps panning from where it is.
            Point remaining = touches.Values.First();
            panAnchor = camera.ScreenToWorld(remaining);
            pinchStartZoom = camera.TargetZoom;
        }
    }

    private void OnTouchCaptureLost(PointerCaptureLostEventArgs e)
    {
        if (!touches.Remove(e.Pointer))
        {
            return;
        }

        if (touches.Count == 0)
        {
            tapStart = null;
            panAnchor = null;
            HideTouchInfo();
        }
    }

    private void OnTouchHolding(HoldingRoutedEventArgs e)
    {
        if (e.HoldingState == HoldingState.Started && tapStart is { } start && !touchPanning && !touchPinched && ViewModel.IsActive)
        {
            Point worldPoint = camera.ScreenToWorld(start);
            WorldPosition? hitPosition = HitTestWorld(worldPoint);

            ViewModel.ShowPositionInfo(WorldTransform.FromWorldPoint(worldPoint), hitPosition);
            touchInfoShown = true;
            e.Handled = true;
        }
        else if (e.HoldingState == HoldingState.Canceled)
        {
            HideTouchInfo();
        }
    }

    private void Tap(Point position)
    {
        DateTime now = DateTime.UtcNow;
        int clickCount = 1;

        if (lastTapPosition is { } last &&
            now - lastTapTime <= DoubleTapTime &&
            Point.Distance(last, position) <= LocalDips(DoubleTapDips))
        {
            clickCount = 2;
            lastTapPosition = null;
        }
        else
        {
            lastTapPosition = position;
            lastTapTime = now;
        }

        Point worldPoint = camera.ScreenToWorld(position);
        WorldPosition? hitPosition = HitTestWorld(worldPoint);

        ViewModel.HandleMapClickAsync(WorldTransform.FromWorldPoint(worldPoint), hitPosition, clickCount).FileAndForget();
    }

    private void UpdateHoverAt(Point position)
    {
        Point worldPoint = camera.ScreenToWorld(position);

        ViewModel.UpdateHover(WorldTransform.FromWorldPoint(worldPoint), HitTestWorld(worldPoint));
    }

    private WorldPosition? HitTestWorld(Point worldPoint)
    {
        IReadOnlyList<WorldRenderContent.HitTarget> hitTargets = snapshotWorker.Current?.WorldContent.HitTargets ?? [];

        return WorldHitTester.HitTest(hitTargets, worldPoint);
    }

    private void HideTouchInfo()
    {
        if (touchInfoShown)
        {
            touchInfoShown = false;
            ViewModel.HidePositionInfo();
        }
    }

    private (Point First, Point Second) GetTwoTouches()
    {
        using IEnumerator<Point> enumerator = touches.Values.GetEnumerator();
        enumerator.MoveNext();
        Point first = enumerator.Current;
        enumerator.MoveNext();

        return (first, enumerator.Current);
    }

    private static Point Midpoint(Point first, Point second)
    {
        return new Point((first.X + second.X) / 2, (first.Y + second.Y) / 2);
    }
}
