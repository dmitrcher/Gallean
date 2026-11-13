using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using SkiaSharp;

namespace Gallean.Client;

internal sealed class DragDropBehavior(Control view)
{
    private const double DragThreshold = 5;
    private const string DragImageClass = "drag-image";
    private const string DropZoneClass = "drop-zone";
    private const string DragOverClass = "drag-over";

    private static readonly Size SmallUnitImageSize = new(76, 91);
    private static readonly Size LargeUnitImageSize = new(156, 91);
    private static readonly Size ItemImageSize = new(78, 78);

    private DragDropViewModel? dragDrop;
    private CursorDragImage? dragImage;
    private TopLevel? dragImageTopLevel;
    private Control? hoverZone;
    private IDragSource? pressSource;
    private Point pressPosition;
    private double pressScale = 1;
    private PointerType pressPointerType;
    private ContentControl? touchImage;
    private OverlayLayer? touchLayer;

    public static void Attach(Control view)
    {
        DragDropBehavior behavior = new(view);

        view.AddHandler(InputElement.PointerPressedEvent, behavior.OnDragPointerPressed, RoutingStrategies.Tunnel, true);
        view.AddHandler(InputElement.PointerMovedEvent, behavior.OnDragPointerMoved, RoutingStrategies.Bubble, true);
        view.AddHandler(InputElement.PointerReleasedEvent, behavior.OnDragPointerReleased, RoutingStrategies.Bubble, true);
        view.AddHandler(InputElement.PointerCaptureLostEvent, behavior.OnDragPointerCaptureLost, RoutingStrategies.Bubble, true);
        view.DetachedFromVisualTree += behavior.OnViewDetachedFromVisualTree;
    }

    private static Size GetImageSize(DragPreviewType type)
    {
        return type switch
        {
            DragPreviewType.SmallUnit => SmallUnitImageSize,
            DragPreviewType.LargeUnit => LargeUnitImageSize,
            DragPreviewType.Item => ItemImageSize
        };
    }

    private static IDragSourceReference GetDragReference(IDragSource source, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Shift) && source is IGroupDragSource groupSource)
        {
            return groupSource.GroupDragReference;
        }
        else
        {
            return source.DragReference;
        }
    }

    private static CursorDragImage RenderDragImage(DragPreview preview, Visual host, double scale, double renderScaling)
    {
        double pixelScale = scale * renderScaling;
        Size size = GetImageSize(preview.Type);
        PixelSize pixelSize = PixelSize.FromSize(size, pixelScale);
        Vector dpi = new(96 * pixelScale, 96 * pixelScale);

        ContentControl adorner = new()
        {
            Content = preview,
            Width = size.Width,
            Height = size.Height
        };

        adorner.Classes.Add(DragImageClass);
        AdornerLayer.SetAdorner(host, adorner);
        adorner.Measure(size);
        adorner.Arrange(new Rect(size));

        using RenderTargetBitmap target = new(pixelSize, dpi);
        target.Render(adorner);

        AdornerLayer.SetAdorner(host, null);

        SKImageInfo info = new(pixelSize.Width, pixelSize.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

        using SKBitmap pixels = new(info);
        target.CopyPixels(new PixelRect(pixelSize), pixels.GetPixels(), pixels.ByteCount, pixels.RowBytes);

        SKImage image = SKImage.FromBitmap(pixels);

        return new CursorDragImage(image, size * scale);
    }

    private Visual? FindElementAt<T>(Point point) where T : class
    {
        foreach (Visual visual in view.GetVisualsAt(point))
        {
            for (Visual? current = visual; current is not null; current = current.GetVisualParent())
            {
                if (current.DataContext is T)
                {
                    return current;
                }
            }
        }

        return null;
    }

    private double ScaleAt(Point point)
    {
        foreach (Visual visual in view.GetVisualsAt(point))
        {
            Matrix? transform = visual.TransformToVisual(view);

            if (transform is not null)
            {
                return transform.Value.M11;
            }
        }

        return 1;
    }

    private Control? FindDropZoneAt(Point point)
    {
        foreach (Visual visual in view.GetVisualsAt(point))
        {
            for (Visual? current = visual; current is not null; current = current.GetVisualParent())
            {
                if (current is Control control && control.Classes.Contains(DropZoneClass))
                {
                    return control;
                }
            }
        }

        return null;
    }

    private void OnDragPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        pressSource = null;

        PointerPoint point = e.GetCurrentPoint(view);

        if (dragDrop is null && point.Properties.IsLeftButtonPressed)
        {
            Point position = e.GetPosition(view);
            Visual? element = FindElementAt<IDragSource>(position);
            IDragSource? source = element?.DataContext as IDragSource;

            if (source?.DragPreview is not null)
            {
                pressPointerType = e.Pointer.Type;
                pressSource = source;
                pressPosition = position;
                pressScale = ScaleAt(position);
            }
        }
    }

    private void OnDragPointerMoved(object? sender, PointerEventArgs e)
    {
        Point point = e.GetPosition(view);
        Vector delta = point - pressPosition;

        if (dragDrop is not null)
        {
            UpdateHoverZone(point);
            MoveTouchImage(point);
            e.Handled = true;
        }
        else if (pressSource is not null && delta.Length >= DragThreshold)
        {
            IDragSource source = pressSource;

            pressSource = null;

            if (TryBeginDrag(source, e.KeyModifiers))
            {
                e.Pointer.Capture(view);
                UpdateHoverZone(point);
                MoveTouchImage(point);
                e.Handled = true;
            }
        }
    }

    private void OnDragPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        pressSource = null;

        if (dragDrop is not null)
        {
            DragDropViewModel current = dragDrop;
            Visual? targetElement = FindElementAt<IDropTarget>(e.GetPosition(view));
            IDropTarget? target = targetElement?.DataContext as IDropTarget;

            EndDragImage();

            e.Pointer.Capture(null);
            e.Handled = true;

            if (target is not null)
            {
                current.DropAsync(target.DropReference).FileAndForget();
            }
            else
            {
                current.EndDrag();
            }
        }
    }

    private void OnDragPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (dragDrop is not null)
        {
            if (ReferenceEquals(e.Pointer.Captured, view))
            {
                return;
            }

            DragDropViewModel current = dragDrop;

            EndDragImage();
            current.EndDrag();
        }
    }

    private void OnViewDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (dragDrop is not null)
        {
            DragDropViewModel current = dragDrop;

            EndDragImage();
            current.EndDrag();
        }
    }

    private bool TryBeginDrag(IDragSource source, KeyModifiers modifiers)
    {
        DragPreview? preview = source.DragPreview;
        TopLevel? topLevel = TopLevel.GetTopLevel(view);

        if (preview is not null && topLevel is not null && view.DataContext is IDragDropHost host)
        {
            double renderScaling = UiScaleDecorator.GetRenderScaling(view);

            dragDrop = host.DragDrop;

            if (pressPointerType == PointerType.Touch)
            {
                // A finger has no cursor to carry the image, so it follows the finger in the overlay layer instead.
                ShowTouchImage(preview);
            }
            else
            {
                dragImage = RenderDragImage(preview, view, pressScale, renderScaling);
                dragImageTopLevel = topLevel;
                CursorScope.SetDragImage(topLevel, dragImage);
            }

            dragDrop.BeginDrag(GetDragReference(source, modifiers));

            return true;
        }
        else
        {
            return false;
        }
    }

    private void UpdateHoverZone(Point point)
    {
        Control? zone = FindDropZoneAt(point);

        if (ReferenceEquals(zone, hoverZone))
        {
            return;
        }

        hoverZone?.Classes.Remove(DragOverClass);
        zone?.Classes.Add(DragOverClass);
        hoverZone = zone;
    }

    private void ShowTouchImage(DragPreview preview)
    {
        OverlayLayer? layer = OverlayLayer.GetOverlayLayer(view);

        if (layer is null)
        {
            return;
        }

        Size size = GetImageSize(preview.Type);
        double scale = pressScale * (view.TransformToVisual(layer)?.M11 ?? 1);

        touchImage = new ContentControl
        {
            Content = preview,
            Width = size.Width,
            Height = size.Height,
            IsHitTestVisible = false,
            Opacity = 0.9,
            RenderTransformOrigin = RelativePoint.TopLeft,
            RenderTransform = new ScaleTransform(scale, scale)
        };

        touchImage.Classes.Add(DragImageClass);

        // The overlay layer is outside the screen, so the image does not inherit the screen's assets.
        if (AssetScope.GetOptionalAssets(view) is { } assets)
        {
            AssetScope.SetAssets(touchImage, assets);
        }

        touchImage.SetValue(UiScaleDecorator.RenderScalingProperty, UiScaleDecorator.GetRenderScaling(view));

        touchLayer = layer;
        layer.Children.Add(touchImage);
    }

    private void MoveTouchImage(Point point)
    {
        if (touchImage is null || touchLayer is null || view.TranslatePoint(point, touchLayer) is not { } position)
        {
            return;
        }

        double scale = touchImage.RenderTransform is ScaleTransform transform ? transform.ScaleX : 1;
        double width = touchImage.Width * scale;
        double height = touchImage.Height * scale;

        // Above the finger, so the finger does not hide what is being dragged.
        Canvas.SetLeft(touchImage, position.X - (width / 2));
        Canvas.SetTop(touchImage, position.Y - height - (height / 4));
    }

    private void EndDragImage()
    {
        if (touchImage is not null)
        {
            touchLayer?.Children.Remove(touchImage);
            touchImage = null;
            touchLayer = null;
        }

        hoverZone?.Classes.Remove(DragOverClass);
        hoverZone = null;
        dragDrop = null;

        if (dragImageTopLevel is not null)
        {
            CursorScope.SetDragImage(dragImageTopLevel, null);
            dragImageTopLevel = null;
        }

        dragImage?.Image.Dispose();
        dragImage = null;
    }
}
