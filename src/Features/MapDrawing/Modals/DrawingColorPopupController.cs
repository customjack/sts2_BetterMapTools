using BetterMapTools.Features.Common.Modals;
using Godot;
using BetterMapTools.Features.MapDrawing.Multiplayer;
using BetterMapTools.Features.Settings;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace BetterMapTools.Features.MapDrawing.Modals;

internal static class DrawingColorPopupController
{
    private const string PopupName = "BetterMapToolsDrawingColorPopup";
    private const float WindowWidth = 1080f;
    private const float WindowHeight = 640f;
    private const float WindowMargin = 28f;

    public static void Toggle(NMapScreen mapScreen)
    {
        var existing = mapScreen.GetNodeOrNull<Control>(PopupName);
        if (existing != null)
        {
            existing.QueueFree();
            return;
        }

        mapScreen.GetNodeOrNull<Control>("BetterMapToolsPopup")?.QueueFree();
        mapScreen.AddChild(BuildPopup(mapScreen));
    }

    private static Control BuildPopup(NMapScreen mapScreen)
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

        var titleMargin = new MarginContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
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

        titleStack.AddChild(CreateSectionLabel("BetterMapTools", 22, Colors.White));
        titleStack.AddChild(CreateMutedLabel("Drawing Color", new Color(0.78f, 0.84f, 0.9f, 0.88f), 15));

        var closeButton = new Button { Text = "Close", CustomMinimumSize = new Vector2(96f, 34f) };
        titleRow.AddChild(closeButton);

        var contentScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            FollowFocus = true
        };
        root.AddChild(contentScroll);

        var contentColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
        contentColumn.AddThemeConstantOverride("separation", 12);
        contentScroll.AddChild(contentColumn);

        var instructions = AddSection(contentColumn, "Drawing Color");
        instructions.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        instructions.AddChild(CreateMutedLabel(
            "Override the default character drawing color used by the pencil tool. Multiplayer peers receive your selected color.",
            new Color(0.84f, 0.88f, 0.92f, 0.96f),
            15));

        // Prefer the in-session override, then the saved preference, then the character default.
        string currentRaw;
        if (MapDrawingColorOverrideService.TryGetLocalOverride(out var overrideColor))
        {
            currentRaw = MapDrawingColorOverrideService.ToColorRaw(overrideColor);
        }
        else if (!string.IsNullOrWhiteSpace(RoutingSettings.SavedDrawingColorRaw))
        {
            currentRaw = RoutingSettings.SavedDrawingColorRaw;
        }
        else
        {
            currentRaw = MapDrawingColorOverrideService.ToColorRaw(MapDrawingColorOverrideService.GetLocalDefaultColor());
        }

        DrawingColorState.Remember(currentRaw);
        var colorRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        colorRow.AddThemeConstantOverride("separation", 8);
        instructions.AddChild(colorRow);
        colorRow.AddChild(CreateMutedLabel("Current", new Color(0.9f, 0.92f, 0.96f, 0.92f), 15, minWidth: 72f));

        var colorValue = CreateMutedLabel(string.Empty, new Color(0.85f, 0.9f, 0.96f, 0.94f), 15);
        colorValue.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        colorRow.AddChild(colorValue);

        var colorPreview = new ColorRect
        {
            CustomMinimumSize = new Vector2(32f, 32f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        colorRow.AddChild(colorPreview);

        var pickButton = new Button
        {
            Text = "Pick Color",
            CustomMinimumSize = new Vector2(118f, 34f)
        };
        colorRow.AddChild(pickButton);

        var statusLabel = CreateMutedLabel(string.Empty, new Color(0.82f, 0.86f, 0.92f, 0.88f), 14);
        instructions.AddChild(statusLabel);

        void SyncPreview(string raw)
        {
            if (RoutingSettings.TryResolveColor(raw, out var parsed))
            {
                colorValue.Text = raw;
                colorPreview.Color = parsed;
                colorPreview.Modulate = Colors.White;
                statusLabel.Text = "Selected color will be used for future drawings.";
                statusLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.86f, 0.98f, 0.92f));
            }
            else
            {
                colorValue.Text = raw;
                colorPreview.Color = new Color(0.2f, 0.2f, 0.2f, 1f);
                colorPreview.Modulate = new Color(0.94f, 0.42f, 0.42f, 1f);
                statusLabel.Text = "Invalid color. Use #RRGGBB, #RRGGBBAA, or r,g,b,a.";
                statusLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.56f, 0.56f, 0.96f));
            }
        }

        SyncPreview(currentRaw);
        pickButton.Pressed += () => SharedColorPickerModal.Open(overlay, mapScreen, new SharedColorPickerModal.Request
        {
            Title = "BetterMapTools",
            Subtitle = "Drawing Color",
            Description = "Choose the color used for future manual map drawings. Existing strokes keep their current color.",
            InitialColorRaw = currentRaw,
            PlaceholderText = MapDrawingColorOverrideService.ToColorRaw(MapDrawingColorOverrideService.GetLocalDefaultColor()),
            AllowAlpha = false,
            ApplyButtonText = "Apply",
            OnApply = value => ApplyColor(value, overlay)
        });

        var characterSection = AddSection(contentColumn, "Character Color");
        characterSection.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        characterSection.AddChild(CreateMutedLabel(
            "Use your character's default map drawing color.",
            new Color(0.84f, 0.88f, 0.92f, 0.96f),
            15));

        var characterDefaultColor = MapDrawingColorOverrideService.GetLocalDefaultColor();
        var characterDefaultRaw = MapDrawingColorOverrideService.ToColorRaw(characterDefaultColor);
        var characterRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        characterRow.AddThemeConstantOverride("separation", 8);
        characterSection.AddChild(characterRow);

        var characterSwatch = new ColorRect
        {
            CustomMinimumSize = new Vector2(28f, 28f),
            Color = characterDefaultColor,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        characterRow.AddChild(characterSwatch);

        var characterButton = new Button
        {
            Text = characterDefaultRaw,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        characterButton.Pressed += () =>
        {
            RoutingSettings.SetSavedDrawingColorRaw(null);
            RoutingSettingsRegistration.PersistCurrentValuesIfReady();
            MapDrawingColorOverrideService.ApplyLocalOverride(null);
            MapDrawingSyncService.BroadcastLocalColor("Drawing color default button");
            overlay.QueueFree();
        };
        characterRow.AddChild(characterButton);

        var recentsSection = AddSection(contentColumn, "Recent Colors");
        recentsSection.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        recentsSection.AddChild(CreateMutedLabel(
            "Quickly reuse one of your recent drawing colors.",
            new Color(0.84f, 0.88f, 0.92f, 0.96f),
            15));

        var recentColorsList = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        recentColorsList.AddThemeConstantOverride("separation", 8);
        recentsSection.AddChild(recentColorsList);

        void RefreshRecents()
        {
            foreach (Node child in recentColorsList.GetChildren())
            {
                recentColorsList.RemoveChild(child);
                child.QueueFree();
            }

            if (DrawingColorState.RecentColorRaws.Count == 0)
            {
                recentColorsList.AddChild(CreateMutedLabel(
                    "Recent colors appear here after you apply them.",
                    new Color(0.72f, 0.78f, 0.84f, 0.92f),
                    14));
                return;
            }

            foreach (var raw in DrawingColorState.RecentColorRaws)
            {
                var row = new HBoxContainer
                {
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                };
                row.AddThemeConstantOverride("separation", 8);
                recentColorsList.AddChild(row);

                var swatch = new ColorRect
                {
                    CustomMinimumSize = new Vector2(28f, 28f),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                if (RoutingSettings.TryResolveColor(raw, out var recentColor))
                {
                    swatch.Color = recentColor;
                    swatch.Modulate = Colors.White;
                }
                else
                {
                    swatch.Color = new Color(0.2f, 0.2f, 0.2f, 1f);
                    swatch.Modulate = new Color(0.94f, 0.42f, 0.42f, 1f);
                }

                row.AddChild(swatch);

                var useButton = new Button
                {
                    Text = raw,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                };
                useButton.Pressed += () => ApplyColor(raw, overlay);
                row.AddChild(useButton);
            }
        }

        RefreshRecents();

        // Quick Color Bar toggle section
        var quickBarSection = AddSection(contentColumn, "Quick Color Bar");
        quickBarSection.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        quickBarSection.AddChild(CreateMutedLabel(
            "Show a compact row of color swatches above the toolbar for one-click color switching.",
            new Color(0.84f, 0.88f, 0.92f, 0.96f),
            15));

        var pinRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        pinRow.AddThemeConstantOverride("separation", 10);
        quickBarSection.AddChild(pinRow);

        var pinButton = new Button
        {
            Text = RoutingSettings.ColorQuickBarPinned ? "Pinned — Click to Unpin" : "Unpinned — Click to Pin",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ToggleMode = true,
            ButtonPressed = RoutingSettings.ColorQuickBarPinned
        };
        pinButton.Toggled += pressed =>
        {
            RoutingSettingsRegistration.SetAndPersistColorQuickBarPinned(pressed);
            pinButton.Text = pressed ? "Pinned — Click to Unpin" : "Unpinned — Click to Pin";
            ColorQuickBarFeature.RefreshBar(mapScreen);
        };
        pinRow.AddChild(pinButton);

        closeButton.Pressed += overlay.QueueFree;

        AttachWindowPlacement(mapScreen, window);
        AttachDragBehavior(titleBar, mapScreen, window);
        return overlay;
    }

    private static void ApplyColor(string raw, Control overlay)
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
        MapDrawingSyncService.BroadcastLocalColor("Drawing color apply");
        overlay.QueueFree();
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

        var margin = new MarginContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        section.AddChild(margin);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(content);

        content.AddChild(CreateSectionLabel(title, 18, Colors.White));
        return content;
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

    private static void AttachWindowPlacement(Control mapScreen, Control window)
    {
        var hasCustomPosition = false;

        void PlaceWindow()
        {
            if (!GodotObject.IsInstanceValid(mapScreen) || !GodotObject.IsInstanceValid(window))
            {
                return;
            }

            var viewportSize = mapScreen.Size;
            if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
            {
                viewportSize = mapScreen.GetWindow()?.ContentScaleSize ?? viewportSize;
            }

            var desired = hasCustomPosition
                ? window.Position
                : new Vector2(
                    (viewportSize.X - window.Size.X) * 0.5f,
                    (viewportSize.Y - window.Size.Y) * 0.5f);
            window.Position = ClampWindowPosition(viewportSize, window.Size, desired);
        }

        window.Resized += PlaceWindow;
        mapScreen.Resized += PlaceWindow;
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

    private static void AttachDragBehavior(Control dragHandle, Control mapScreen, Control window)
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
                    var desiredGlobal = mouseMotion.GlobalPosition - dragOffset;
                    var desiredLocal = mapScreen.GetGlobalTransformWithCanvas().AffineInverse() * desiredGlobal;
                    window.Position = ClampWindowPosition(mapScreen.Size, window.Size, desiredLocal);
                    dragHandle.AcceptEvent();
                    break;
            }
        };
    }

    private static Vector2 ClampWindowPosition(Vector2 viewportSize, Vector2 windowSize, Vector2 desiredPosition)
    {
        var maxX = MathF.Max(WindowMargin, viewportSize.X - windowSize.X - WindowMargin);
        var maxY = MathF.Max(WindowMargin, viewportSize.Y - windowSize.Y - WindowMargin);
        return new Vector2(
            Mathf.Clamp(desiredPosition.X, WindowMargin, maxX),
            Mathf.Clamp(desiredPosition.Y, WindowMargin, maxY));
    }
}
