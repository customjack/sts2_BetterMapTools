using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BetterMapTools.Features.MapDrawing;
using BetterMapTools.Features.MapRouting.Modals;
using BetterMapTools.Features.Settings;

namespace BetterMapTools.Features.Common.Modals;

internal static class SharedColorPickerModal
{
    private const string PopupName = "BetterMapToolsSharedColorPickerModal";
    private const float WindowWidth = 1080f;
    private const float WindowHeight = 640f;

    internal sealed class Request
    {
        public required string Title { get; init; }
        public required string Subtitle { get; init; }
        public required string Description { get; init; }
        public required string InitialColorRaw { get; init; }
        public required string PlaceholderText { get; init; }
        public bool AllowAlpha { get; init; } = true;
        public string ApplyButtonText { get; init; } = "Apply";
        public required Action<string> OnApply { get; init; }
    }

    public static void Open(Control modalHost, Control placementRoot, Request request, bool hideHostChildren = true)
    {
        modalHost.GetNodeOrNull<Control>(PopupName)?.QueueFree();
        var hiddenControls = hideHostChildren
            ? HideVisibleChildren(modalHost)
            : System.Array.Empty<Control>();
        modalHost.AddChild(BuildPopup(placementRoot, request, hiddenControls));
    }

    private static Control BuildPopup(Control placementRoot, Request request, IReadOnlyList<Control> hiddenControls)
    {
        var overlay = new Control
        {
            Name = PopupName,
            AnchorLeft = 0f,
            AnchorTop = 0f,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            FocusMode = Control.FocusModeEnum.None
        };

        var window = new PanelContainer
        {
            CustomMinimumSize = new Vector2(WindowWidth, WindowHeight),
            Size = new Vector2(WindowWidth, WindowHeight),
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.All
        };
        window.AddThemeStyleboxOverride("panel", CreateWindowStyle());
        overlay.AddChild(window);

        var outerMargin = new MarginContainer();
        outerMargin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        outerMargin.AddThemeConstantOverride("margin_left", 14);
        outerMargin.AddThemeConstantOverride("margin_top", 14);
        outerMargin.AddThemeConstantOverride("margin_right", 14);
        outerMargin.AddThemeConstantOverride("margin_bottom", 14);
        window.AddChild(outerMargin);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 12);
        outerMargin.AddChild(root);

        var titleBar = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 56f)
        };
        titleBar.AddThemeStyleboxOverride("panel", CreateHeaderStyle());
        root.AddChild(titleBar);

        var titleMargin = new MarginContainer();
        titleMargin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        titleMargin.AddThemeConstantOverride("margin_left", 14);
        titleMargin.AddThemeConstantOverride("margin_top", 10);
        titleMargin.AddThemeConstantOverride("margin_right", 14);
        titleMargin.AddThemeConstantOverride("margin_bottom", 10);
        titleBar.AddChild(titleMargin);

        var titleRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        titleRow.AddThemeConstantOverride("separation", 10);
        titleMargin.AddChild(titleRow);

        var titleStack = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        titleStack.AddThemeConstantOverride("separation", 2);
        titleRow.AddChild(titleStack);

        titleStack.AddChild(CreateSectionLabel(request.Title, 22, Colors.White));
        titleStack.AddChild(CreateMutedLabel(request.Subtitle, new Color(0.78f, 0.84f, 0.9f, 0.88f), 15));

        var closeButton = new Button { Text = "Close", CustomMinimumSize = new Vector2(96f, 34f) };
        titleRow.AddChild(closeButton);

        var contentScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            FollowFocus = true
        };
        root.AddChild(contentScroll);

        var contentColumn = MapModalLayout.CreateScrollContentColumn(contentScroll);
        var section = AddSection(contentColumn, "Color");
        section.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        section.AddChild(CreateMutedLabel(request.Description, new Color(0.84f, 0.88f, 0.92f, 0.96f), 15));

        var initialRaw = string.IsNullOrWhiteSpace(request.InitialColorRaw)
            ? request.PlaceholderText
            : request.InitialColorRaw;
        RoutingSettings.TryResolveColor(initialRaw, out var initialColor);
        if (!request.AllowAlpha)
        {
            initialColor.A = 1f;
        }

        var picker = new ColorPicker
        {
            Color = initialColor,
            EditAlpha = request.AllowAlpha,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(0f, 360f)
        };
        ConfigurePickerUi(picker);

        var pickerRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
        pickerRow.AddThemeConstantOverride("separation", 12);
        section.AddChild(pickerRow);
        pickerRow.AddChild(picker);

        var previewColumn = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(188f, 0f),
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
        previewColumn.AddThemeConstantOverride("separation", 8);
        pickerRow.AddChild(previewColumn);
        previewColumn.AddChild(CreateMutedLabel("Preview", new Color(0.9f, 0.92f, 0.96f, 0.92f), 15));

        var preview = new ColorRect
        {
            CustomMinimumSize = new Vector2(188f, 188f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        previewColumn.AddChild(preview);

        var previewValue = CreateMutedLabel(string.Empty, new Color(0.82f, 0.88f, 0.94f, 0.94f), 14);
        previewValue.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        previewColumn.AddChild(previewValue);

        var colorRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        colorRow.AddThemeConstantOverride("separation", 8);
        section.AddChild(colorRow);
        colorRow.AddChild(CreateMutedLabel("Hex", new Color(0.9f, 0.92f, 0.96f, 0.92f), 15, minWidth: 64f));

        var colorInput = new LineEdit
        {
            Text = initialRaw,
            PlaceholderText = request.PlaceholderText,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        colorRow.AddChild(colorInput);

        var statusLabel = CreateMutedLabel(string.Empty, new Color(0.82f, 0.86f, 0.92f, 0.88f), 14);
        section.AddChild(statusLabel);

        var syncing = false;
        void SyncPreview(string raw)
        {
            if (RoutingSettings.TryResolveColor(raw, out var parsed))
            {
                preview.Color = parsed;
                preview.Modulate = Colors.White;
                previewValue.Text = raw;
                statusLabel.Text = "Selected color will be applied when you confirm.";
                statusLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.86f, 0.98f, 0.92f));
            }
            else
            {
                preview.Color = new Color(0.2f, 0.2f, 0.2f, 1f);
                preview.Modulate = new Color(0.94f, 0.42f, 0.42f, 1f);
                previewValue.Text = raw;
                statusLabel.Text = "Invalid color. Use #RRGGBB, #RRGGBBAA, or r,g,b,a.";
                statusLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.56f, 0.56f, 0.96f));
            }
        }

        picker.ColorChanged += color =>
        {
            if (syncing)
            {
                return;
            }

            if (!request.AllowAlpha)
            {
                color.A = 1f;
            }

            syncing = true;
            colorInput.Text = MapDrawingColorOverrideService.ToColorRaw(color);
            SyncPreview(colorInput.Text);
            syncing = false;
        };

        colorInput.TextChanged += value =>
        {
            SyncPreview(value);
            if (syncing || !RoutingSettings.TryResolveColor(value, out var parsed))
            {
                return;
            }

            if (!request.AllowAlpha)
            {
                parsed.A = 1f;
            }

            syncing = true;
            picker.Color = parsed;
            syncing = false;
        };

        SyncPreview(colorInput.Text);

        var actionRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actionRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(actionRow);
        actionRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var applyButton = new Button
        {
            Text = request.ApplyButtonText,
            CustomMinimumSize = new Vector2(108f, 34f)
        };
        actionRow.AddChild(applyButton);

        closeButton.Pressed += overlay.QueueFree;
        overlay.TreeExiting += () => RestoreHiddenChildren(hiddenControls);
        applyButton.Pressed += () =>
        {
            if (!RoutingSettings.TryResolveColor(colorInput.Text, out var selected))
            {
                SyncPreview(colorInput.Text);
                return;
            }

            if (!request.AllowAlpha)
            {
                selected.A = 1f;
            }

            request.OnApply(MapDrawingColorOverrideService.ToColorRaw(selected));
            overlay.QueueFree();
        };

        MapModalLayout.AttachResponsiveWindow(placementRoot, window, titleBar, WindowWidth, WindowHeight);
        return overlay;
    }

    private static IReadOnlyList<Control> HideVisibleChildren(Control host)
    {
        var hidden = host.GetChildren()
            .OfType<Control>()
            .Where(control => control.Visible)
            .ToList();
        foreach (var control in hidden)
        {
            control.Visible = false;
        }

        return hidden;
    }

    private static void RestoreHiddenChildren(IReadOnlyList<Control> hiddenControls)
    {
        foreach (var control in hiddenControls)
        {
            if (GodotObject.IsInstanceValid(control))
            {
                control.Visible = true;
            }
        }
    }

    private static VBoxContainer AddSection(Control parent, string title)
    {
        var section = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        section.AddThemeStyleboxOverride("panel", CreateSectionStyle());
        parent.AddChild(section);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        section.AddChild(margin);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(content);

        content.AddChild(CreateSectionLabel(title, 18, Colors.White));
        return content;
    }

    private static void ConfigurePickerUi(ColorPicker picker)
    {
        TrySetPickerProperty(picker, "presets_visible", false);
        TrySetPickerProperty(picker, "can_add_swatches", false);
        TrySetPickerProperty(picker, "sampler_visible", false);
    }

    private static void TrySetPickerProperty(ColorPicker picker, string propertyName, Variant value)
    {
        foreach (Godot.Collections.Dictionary property in picker.GetPropertyList())
        {
            if (!property.TryGetValue("name", out var nameVariant))
            {
                continue;
            }

            var name = nameVariant.ToString();
            if (!string.Equals(name, propertyName, StringComparison.Ordinal))
            {
                continue;
            }

            picker.Set(propertyName, value);
            return;
        }
    }

    private static Label CreateSectionLabel(string text, int fontSize, Color color)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Label CreateMutedLabel(string text, Color color, int fontSize, float minWidth = 0f)
    {
        var label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = minWidth > 0f ? new Vector2(minWidth, 0f) : Vector2.Zero,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static StyleBoxFlat CreateWindowStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.11f, 0.15f, 0.95f),
            BorderColor = new Color(0.46f, 0.58f, 0.68f, 0.78f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ShadowColor = new Color(0f, 0f, 0f, 0.32f),
            ShadowSize = 8
        };
    }

    private static StyleBoxFlat CreateHeaderStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.22f, 0.29f, 0.94f),
            BorderColor = new Color(0.34f, 0.45f, 0.55f, 0.78f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10
        };
    }

    private static StyleBoxFlat CreateSectionStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.11f, 0.15f, 0.19f, 0.72f),
            BorderColor = new Color(0.28f, 0.36f, 0.44f, 0.68f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        };
    }

}
