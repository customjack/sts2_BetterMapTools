using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace BetterMapTools.Features.MapDrawing;

internal static class ScreenColorDropperController
{
    private const string OverlayName = "BetterMapToolsColorDropperOverlay";
    private const string HeaderPanelName = "HeaderPanel";
    private const string PreviewName = "Preview";
    private const string PreviewLabelName = "PreviewLabel";

    public static void Toggle(NMapScreen mapScreen)
    {
        var existing = mapScreen.GetNodeOrNull<Control>(OverlayName);
        if (existing != null)
        {
            existing.QueueFree();
            return;
        }

        mapScreen.AddChild(BuildOverlay(mapScreen));
    }

    private static Control BuildOverlay(NMapScreen mapScreen)
    {
        var overlay = new Control
        {
            Name = OverlayName,
            AnchorLeft = 0f,
            AnchorTop = 0f,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 0f,
            OffsetTop = 0f,
            OffsetRight = 0f,
            OffsetBottom = 0f,
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.Cross
        };

        var header = new PanelContainer
        {
            Name = HeaderPanelName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        header.AnchorLeft = 0.5f;
        header.AnchorTop = 0f;
        header.AnchorRight = 0.5f;
        header.AnchorBottom = 0f;
        header.OffsetLeft = -360f;
        header.OffsetTop = 24f;
        header.OffsetRight = 360f;
        header.OffsetBottom = 96f;
        header.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.09f, 0.12f, 0.16f, 0.90f),
            BorderColor = new Color(0.44f, 0.58f, 0.70f, 0.92f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        });
        overlay.AddChild(header);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        header.AddChild(margin);

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 12);
        margin.AddChild(row);

        var textColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        textColumn.AddThemeConstantOverride("separation", 4);
        row.AddChild(textColumn);

        textColumn.AddChild(new Label
        {
            Text = "Screen Eyedropper",
            Modulate = Colors.White
        });
        textColumn.AddChild(new Label
        {
            Text = "Left click to copy a color for drawing. Right click or Esc cancels.",
            Modulate = new Color(0.80f, 0.86f, 0.92f, 0.96f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var current = MapDrawingColorOverrideService.GetLocalEffectiveColor();
        row.AddChild(new ColorRect
        {
            Name = PreviewName,
            CustomMinimumSize = new Vector2(44f, 44f),
            Color = current,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        row.AddChild(new Label
        {
            Name = PreviewLabelName,
            Text = MapDrawingColorOverrideService.ToColorRaw(current),
            Modulate = new Color(0.90f, 0.94f, 1f, 0.96f),
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        overlay.GuiInput += inputEvent => HandleInput(overlay, mapScreen, inputEvent);
        Callable.From(() => overlay.GrabFocus()).CallDeferred();
        return overlay;
    }

    private static void HandleInput(Control overlay, NMapScreen mapScreen, InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventKey { Pressed: true, Echo: false } keyEvent:
                if (keyEvent.Keycode == Key.Escape || keyEvent.PhysicalKeycode == Key.Escape)
                {
                    overlay.QueueFree();
                    mapScreen.AcceptEvent();
                }
                break;
            case InputEventMouseMotion motion:
                UpdatePreview(overlay, mapScreen, motion.Position);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
                overlay.QueueFree();
                mapScreen.AcceptEvent();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                if (IsOverHeader(overlay))
                {
                    mapScreen.AcceptEvent();
                    return;
                }

                if (!TrySampleColor(mapScreen, click.Position, out var sampled))
                {
                    Log.Warn("[BetterMapTools] Screen eyedropper failed to sample a color.");
                    overlay.QueueFree();
                    mapScreen.AcceptEvent();
                    return;
                }

                var normalized = MapDrawingColorOverrideService.NormalizeDrawingColor(sampled);
                var raw = MapDrawingColorOverrideService.ToColorRaw(normalized);
                ColorQuickBarFeature.ApplyColor(raw);
                overlay.QueueFree();
                mapScreen.AcceptEvent();
                break;
        }
    }

    private static bool IsOverHeader(Control overlay)
    {
        var header = overlay.GetNodeOrNull<Control>(HeaderPanelName);
        return header != null && header.GetGlobalRect().HasPoint(overlay.GetGlobalMousePosition());
    }

    private static void UpdatePreview(Control overlay, NMapScreen mapScreen, Vector2 position)
    {
        if (IsOverHeader(overlay) || !TrySampleColor(mapScreen, position, out var sampled))
        {
            return;
        }

        var preview = overlay.FindChild(PreviewName, recursive: true, owned: false) as ColorRect;
        var label = overlay.FindChild(PreviewLabelName, recursive: true, owned: false) as Label;
        if (preview != null)
        {
            preview.Color = sampled;
        }

        if (label != null)
        {
            label.Text = MapDrawingColorOverrideService.ToColorRaw(MapDrawingColorOverrideService.NormalizeDrawingColor(sampled));
        }
    }

    private static bool TrySampleColor(NMapScreen mapScreen, Vector2 viewportPosition, out Color sampled)
    {
        sampled = Colors.White;
        var viewport = mapScreen.GetViewport();
        if (viewport == null)
        {
            return false;
        }

        var image = viewport.GetTexture()?.GetImage();
        if (image == null || image.IsEmpty())
        {
            return false;
        }

        var visibleRect = viewport.GetVisibleRect();
        var visibleWidth = Mathf.Max(1f, visibleRect.Size.X);
        var visibleHeight = Mathf.Max(1f, visibleRect.Size.Y);
        var maxX = Mathf.Max(0, image.GetWidth() - 1);
        var maxY = Mathf.Max(0, image.GetHeight() - 1);
        var pixelX = Mathf.Clamp(Mathf.RoundToInt(viewportPosition.X / visibleWidth * maxX), 0, maxX);
        var pixelY = Mathf.Clamp(Mathf.RoundToInt(viewportPosition.Y / visibleHeight * maxY), 0, maxY);

        sampled = image.GetPixel(pixelX, pixelY);
        return true;
    }
}
