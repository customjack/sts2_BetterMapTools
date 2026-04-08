using Godot;

namespace BetterMapTools.Features.MapRouting.Modals;

internal static class MapModalLayout
{
    public const float WindowMargin = 28f;
    private const float FallbackMinWidth = 420f;
    private const float FallbackMinHeight = 320f;

    public static VBoxContainer CreateScrollContentColumn(ScrollContainer scroll, int separation = 12)
    {
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
        column.AddThemeConstantOverride("separation", separation);
        scroll.AddChild(column);
        return column;
    }

    public static void AttachResponsiveWindow(
        Control placementRoot,
        Control window,
        Control dragHandle,
        float preferredWidth,
        float preferredHeight,
        float minWidth = FallbackMinWidth,
        float minHeight = FallbackMinHeight)
    {
        AttachResponsivePlacement(placementRoot, window, preferredWidth, preferredHeight, minWidth, minHeight);
        AttachDragBehavior(dragHandle, placementRoot, window);
    }

    public static Vector2 ResolveViewportSize(Control placementRoot)
    {
        var viewportSize = placementRoot.Size;
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            viewportSize = placementRoot.GetViewportRect().Size;
        }

        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            viewportSize = placementRoot.GetWindow()?.ContentScaleSize ?? viewportSize;
        }

        return viewportSize;
    }

    public static Vector2 ResolveWindowSize(
        Vector2 viewportSize,
        float preferredWidth,
        float preferredHeight,
        float minWidth = FallbackMinWidth,
        float minHeight = FallbackMinHeight)
    {
        var maxWidth = MathF.Max(320f, viewportSize.X - WindowMargin * 2f);
        var maxHeight = MathF.Max(280f, viewportSize.Y - WindowMargin * 2f);

        var resolvedWidth = MathF.Min(preferredWidth, maxWidth);
        var resolvedHeight = MathF.Min(preferredHeight, maxHeight);

        resolvedWidth = MathF.Max(resolvedWidth, MathF.Min(minWidth, maxWidth));
        resolvedHeight = MathF.Max(resolvedHeight, MathF.Min(minHeight, maxHeight));

        return new Vector2(resolvedWidth, resolvedHeight);
    }

    public static Vector2 ClampWindowPosition(Vector2 viewportSize, Vector2 windowSize, Vector2 desiredPosition)
    {
        var maxX = MathF.Max(WindowMargin, viewportSize.X - windowSize.X - WindowMargin);
        var maxY = MathF.Max(WindowMargin, viewportSize.Y - windowSize.Y - WindowMargin);
        return new Vector2(
            Mathf.Clamp(desiredPosition.X, WindowMargin, maxX),
            Mathf.Clamp(desiredPosition.Y, WindowMargin, maxY));
    }

    private static void AttachResponsivePlacement(
        Control placementRoot,
        Control window,
        float preferredWidth,
        float preferredHeight,
        float minWidth,
        float minHeight)
    {
        var hasCustomPosition = false;

        void PlaceWindow()
        {
            if (!GodotObject.IsInstanceValid(placementRoot) || !GodotObject.IsInstanceValid(window))
            {
                return;
            }

            var viewportSize = ResolveViewportSize(placementRoot);
            if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
            {
                return;
            }

            var windowSize = ResolveWindowSize(viewportSize, preferredWidth, preferredHeight, minWidth, minHeight);
            if (window.Size != windowSize)
            {
                window.Size = windowSize;
            }

            if (window.CustomMinimumSize != windowSize)
            {
                window.CustomMinimumSize = windowSize;
            }

            var desiredPosition = hasCustomPosition
                ? window.Position
                : new Vector2(
                    (viewportSize.X - windowSize.X) * 0.5f,
                    (viewportSize.Y - windowSize.Y) * 0.5f);
            window.Position = ClampWindowPosition(viewportSize, windowSize, desiredPosition);
        }

        window.Resized += PlaceWindow;
        placementRoot.Resized += PlaceWindow;
        Callable.From(PlaceWindow).CallDeferred();
        PlaceWindow();

        window.SetMeta("bettermaptools_has_custom_position", hasCustomPosition);
        window.Connect(Control.SignalName.GuiInput, Callable.From<InputEvent>(_ =>
        {
            if (window.HasMeta("bettermaptools_has_custom_position"))
            {
                hasCustomPosition = window.GetMeta("bettermaptools_has_custom_position").AsBool();
            }
        }));
    }

    private static void AttachDragBehavior(Control dragHandle, Control placementRoot, Control window)
    {
        var dragging = false;
        var dragOffset = Vector2.Zero;

        dragHandle.GuiInput += @event =>
        {
            switch (@event)
            {
                case InputEventMouseButton mouseButton when mouseButton.ButtonIndex == MouseButton.Left:
                    if (mouseButton.Pressed)
                    {
                        dragging = true;
                        dragOffset = mouseButton.GlobalPosition - window.GlobalPosition;
                        window.SetMeta("bettermaptools_has_custom_position", true);
                        dragHandle.AcceptEvent();
                    }
                    else
                    {
                        dragging = false;
                        dragHandle.AcceptEvent();
                    }
                    break;
                case InputEventMouseMotion mouseMotion when dragging:
                    var viewportSize = ResolveViewportSize(placementRoot);
                    var desiredGlobal = mouseMotion.GlobalPosition - dragOffset;
                    var desiredLocal = placementRoot.GetGlobalTransformWithCanvas().AffineInverse() * desiredGlobal;
                    window.Position = ClampWindowPosition(viewportSize, window.Size, desiredLocal);
                    dragHandle.AcceptEvent();
                    break;
            }
        };
    }
}
