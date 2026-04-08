using Godot;
using BetterMapTools.Features.MapDrawing.Multiplayer;
using BetterMapTools.Features.Common.Modals;
using BetterMapTools.Features.MapRouting.Buttons;
using BetterMapTools.Features.Settings;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace BetterMapTools.Features.MapDrawing;

/// <summary>
/// A compact row of color swatches shown below the map toolbar when pinned.
/// Contains a + button (opens color picker) and up to 10 recent color squares.
/// A pin toggle button is also added to the map toolbar row.
/// </summary>
internal static class ColorQuickBarFeature
{
    private const string QuickBarName = "BetterMapToolsColorQuickBar";
    private const string ColorIconResourceName = "BetterMapTools.EyeDropperIconPng";
    private const string ColorIconGlowResourceName = "BetterMapTools.EyeDropperIconPng";
    private const float SwatchSize = 28f;
    private const float SwatchGap = 4f;
    private const int MaxSwatches = 10;
    private static Texture2D? _colorIconTexture;
    private static Texture2D? _colorIconGlowTexture;

    public static void AttachToMapScreen(NMapScreen mapScreen)
    {
        RefreshBar(mapScreen);

        // Re-populate swatches whenever a color is applied.
        DrawingColorState.RecentColorsChanged += () =>
        {
            if (GodotObject.IsInstanceValid(mapScreen))
            {
                RefreshBar(mapScreen);
            }
        };
    }

    public static void RefreshBar(NMapScreen mapScreen)
    {
        if (!GodotObject.IsInstanceValid(mapScreen))
        {
            return;
        }

        var existing = mapScreen.GetNodeOrNull<Control>(QuickBarName);

        if (!RoutingSettings.ColorQuickBarPinned)
        {
            existing?.QueueFree();
            return;
        }

        if (existing != null)
        {
            RepopulateBar(existing, mapScreen);
            return;
        }

        var bar = new HBoxContainer
        {
            Name = QuickBarName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        bar.AddThemeConstantOverride("separation", (int)SwatchGap);

        PopulateBar(bar, mapScreen);
        mapScreen.AddChild(bar);

        mapScreen.Resized += () => PlaceBar(mapScreen, bar);
        Callable.From(() => PlaceBar(mapScreen, bar)).CallDeferred();
    }

    private static void RepopulateBar(Control bar, NMapScreen mapScreen)
    {
        foreach (var child in bar.GetChildren())
        {
            if (child is Node n)
            {
                bar.RemoveChild(n);
                n.QueueFree();
            }
        }

        PopulateBar(bar, mapScreen);
    }

    private static void PopulateBar(Control bar, NMapScreen mapScreen)
    {
        var dropperButton = CreateCompactColorDropperButton(mapScreen);
        bar.AddChild(dropperButton);

        // + button — opens color picker directly
        var addButton = new Button
        {
            Text = "+",
            CustomMinimumSize = new Vector2(SwatchSize, SwatchSize),
            Size = new Vector2(SwatchSize, SwatchSize),
            TooltipText = "Pick drawing color",
            FocusMode = Control.FocusModeEnum.All
        };
        StyleSwatch(addButton, isAdd: true);
        addButton.Pressed += () => OpenColorPicker(mapScreen);
        bar.AddChild(addButton);

        // Recent color swatches
        var shown = 0;
        foreach (var raw in DrawingColorState.RecentColorRaws)
        {
            if (shown >= MaxSwatches)
            {
                break;
            }

            if (!RoutingSettings.TryResolveColor(raw, out var color))
            {
                continue;
            }

            var capturedRaw = raw;
            var swatch = new Button
            {
                CustomMinimumSize = new Vector2(SwatchSize, SwatchSize),
                Size = new Vector2(SwatchSize, SwatchSize),
                TooltipText = raw,
                FocusMode = Control.FocusModeEnum.All
            };
            StyleSwatch(swatch, isAdd: false, color: color);
            swatch.Pressed += () => ApplyColor(capturedRaw);
            bar.AddChild(swatch);
            shown++;
        }
    }

    private static void StyleSwatch(Button button, bool isAdd, Color? color = null)
    {
        // Do NOT set Flat = true — in Godot 4 it suppresses the normal stylebox override
        // making the button invisible until hovered. Drive all states via explicit overrides.
        button.Text = isAdd ? "+" : string.Empty;

        var bg = isAdd ? new Color(0.18f, 0.24f, 0.30f, 0.90f) : (color ?? Colors.White);
        var bgHover = isAdd ? new Color(0.26f, 0.36f, 0.44f, 0.95f) : (color?.Lightened(0.15f) ?? Colors.White);

        var normalStyle = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = new Color(0.46f, 0.58f, 0.68f, 0.60f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
        };
        var hoverStyle = new StyleBoxFlat
        {
            BgColor = bgHover,
            BorderColor = new Color(0.68f, 0.80f, 0.90f, 0.90f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
        };

        button.AddThemeStyleboxOverride("normal", normalStyle);
        button.AddThemeStyleboxOverride("hover", hoverStyle);
        button.AddThemeStyleboxOverride("pressed", hoverStyle);
        button.AddThemeStyleboxOverride("focus", normalStyle);
        button.AddThemeStyleboxOverride("disabled", normalStyle);

        if (isAdd)
        {
            button.AddThemeColorOverride("font_color", new Color(0.80f, 0.88f, 0.96f, 1f));
            button.AddThemeFontSizeOverride("font_size", 16);
        }
    }

    private static Button CreateCompactColorDropperButton(NMapScreen mapScreen)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(SwatchSize, SwatchSize),
            Size = new Vector2(SwatchSize, SwatchSize),
            TooltipText = "Sample drawing color from screen",
            FocusMode = Control.FocusModeEnum.All
        };
        StyleSwatch(button, isAdd: false, color: new Color(0.18f, 0.24f, 0.30f, 0.90f));

        var icon = new TextureRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            CustomMinimumSize = new Vector2(18f, 18f),
            Position = new Vector2((SwatchSize - 18f) * 0.5f, (SwatchSize - 18f) * 0.5f),
            Size = new Vector2(18f, 18f)
        };
        icon.Texture = LoadCompactColorIcon(hovered: false);
        button.AddChild(icon);

        button.MouseEntered += () => icon.Texture = LoadCompactColorIcon(hovered: true);
        button.MouseExited += () => icon.Texture = LoadCompactColorIcon(hovered: false);
        button.FocusEntered += () => icon.Texture = LoadCompactColorIcon(hovered: true);
        button.FocusExited += () => icon.Texture = LoadCompactColorIcon(hovered: false);
        button.Pressed += () => ScreenColorDropperController.Toggle(mapScreen);
        return button;
    }

    private static Texture2D? LoadCompactColorIcon(bool hovered)
    {
        _colorIconTexture ??= MapToolButtonFeatureBase.LoadEmbeddedPngTexture(ColorIconResourceName);
        _colorIconGlowTexture ??= MapToolButtonFeatureBase.LoadEmbeddedPngTexture(ColorIconGlowResourceName);
        return hovered ? (_colorIconGlowTexture ?? _colorIconTexture) : _colorIconTexture;
    }

    private static void OpenColorPicker(NMapScreen mapScreen)
    {
        var currentRaw = MapDrawingColorOverrideService.TryGetLocalOverride(out var overrideColor)
            ? MapDrawingColorOverrideService.ToColorRaw(overrideColor)
            : RoutingSettings.SavedDrawingColorRaw
              ?? MapDrawingColorOverrideService.ToColorRaw(MapDrawingColorOverrideService.GetLocalDefaultColor());

        SharedColorPickerModal.Open(mapScreen, mapScreen, new SharedColorPickerModal.Request
        {
            Title = "BetterMapTools",
            Subtitle = "Drawing Color",
            Description = "Choose the color used for future manual map drawings.",
            InitialColorRaw = currentRaw,
            PlaceholderText = MapDrawingColorOverrideService.ToColorRaw(MapDrawingColorOverrideService.GetLocalDefaultColor()),
            AllowAlpha = false,
            ApplyButtonText = "Apply",
            OnApply = ApplyColor
        }, hideHostChildren: false);
    }

    internal static void ApplyColor(string raw)
    {
        if (!RoutingSettings.TryResolveColor(raw, out var selected))
        {
            return;
        }

        selected = MapDrawingColorOverrideService.NormalizeDrawingColor(selected);
        var normalizedRaw = MapDrawingColorOverrideService.ToColorRaw(selected);
        DrawingColorState.Remember(normalizedRaw);
        RoutingSettings.SetSavedDrawingColorRaw(normalizedRaw);
        RoutingSettingsRegistration.PersistCurrentValuesIfReady();
        MapDrawingColorOverrideService.ApplyLocalOverride(selected);
        MapDrawingSyncService.BroadcastLocalColor("Color quick bar apply");
    }

    private static void PlaceBar(NMapScreen mapScreen, Control bar)
    {
        if (!GodotObject.IsInstanceValid(mapScreen) || !GodotObject.IsInstanceValid(bar))
        {
            return;
        }

        var tools = mapScreen.GetNodeOrNull<Control>("%DrawingTools");
        if (tools == null)
        {
            return;
        }

        bar.Size = bar.CustomMinimumSize;
        var barHeight = bar.Size.Y > 0f ? bar.Size.Y : SwatchSize;
        bar.Position = new Vector2(tools.Position.X, tools.Position.Y - barHeight - 4f);
    }
}
