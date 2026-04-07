using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using BetterMapTools.Features.Common.Modals;
using BetterMapTools.Features.MapRouting.Metrics;
using BetterMapTools.Features.Settings;

namespace BetterMapTools.Features.MapRouting.Modals;

internal static partial class RoutePopupController
{
    private const string SolverWindowName = "BetterMapToolsSolverWindow";
    private const float WindowWidth = 1080f;
    private const float WindowHeight = 640f;
    private const float WindowMargin = 28f;

    private static Control BuildPopup(NMapScreen mapScreen)
    {
        var metrics = RouteMetricRegistry.Definitions;

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

        var solverWindow = new PanelContainer
        {
            Name = SolverWindowName,
            CustomMinimumSize = new Vector2(WindowWidth, WindowHeight),
            Size = new Vector2(WindowWidth, WindowHeight),
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.All
        };
        solverWindow.AddThemeStyleboxOverride("panel", CreateWindowStyle());
        overlay.AddChild(solverWindow);

        var outerMargin = new MarginContainer();
        outerMargin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        outerMargin.AddThemeConstantOverride("margin_left", 14);
        outerMargin.AddThemeConstantOverride("margin_top", 14);
        outerMargin.AddThemeConstantOverride("margin_right", 14);
        outerMargin.AddThemeConstantOverride("margin_bottom", 14);
        solverWindow.AddChild(outerMargin);

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
        titleStack.AddChild(CreateMutedLabel("Route Solver", new Color(0.78f, 0.84f, 0.9f, 0.88f), 15));

        var activePresetLabel = CreateMutedLabel($"Preset: {RoutingSettings.ActivePresetName}", new Color(0.87f, 0.81f, 0.56f, 0.9f), 14);
        activePresetLabel.VerticalAlignment = VerticalAlignment.Center;
        titleRow.AddChild(activePresetLabel);

        var headerButtons = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        headerButtons.AddThemeConstantOverride("separation", 8);
        titleRow.AddChild(headerButtons);

        var managerButton = new Button { Text = "Presets", CustomMinimumSize = new Vector2(96f, 34f) };
        var closeButton = new Button { Text = "Close", CustomMinimumSize = new Vector2(96f, 34f) };
        headerButtons.AddChild(managerButton);
        headerButtons.AddChild(closeButton);

        var contentRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        contentRow.AddThemeConstantOverride("separation", 12);
        root.AddChild(contentRow);

        var configScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            FollowFocus = true,
            CustomMinimumSize = new Vector2(0f, 0f)
        };
        contentRow.AddChild(configScroll);

        var configColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(RoutingSettings.UseSeparateResultsPanel ? 640f : 0f, 0f)
        };
        configColumn.AddThemeConstantOverride("separation", 12);
        configScroll.AddChild(configColumn);

        var currentSelectionMode = SelectionModeState;


        var selectionModeContent = AddSection(configColumn, "Selection Mode");
        var selectionModeDescription = CreateMutedLabel(string.Empty, new Color(0.82f, 0.86f, 0.92f, 0.94f), 15);
        selectionModeDescription.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        selectionModeContent.AddChild(selectionModeDescription);

        var selectionModeTabs = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        selectionModeTabs.AddThemeConstantOverride("separation", 0);
        selectionModeContent.AddChild(selectionModeTabs);

        var lexicographicButton = CreateModeTabButton("Lexicographic");
        var weightedButton = CreateModeTabButton("Weighted Score");
        selectionModeTabs.AddChild(lexicographicButton);
        selectionModeTabs.AddChild(weightedButton);

        var constraintContent = AddSection(configColumn, "Constraints");
        var constraintControls = new Dictionary<RouteMetricType, (SpinBox Min, SpinBox Max)>();
        foreach (var metric in metrics)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 8);
            constraintContent.AddChild(row);

            row.AddChild(CreateMetricLabelWithIcon(metric, minWidth: 168f));

            var state = ConstraintState[metric.Type];
            var minSpin = new SpinBox
            {
                MinValue = 0,
                MaxValue = 99,
                Step = 1,
                Rounded = true,
                Value = state.Min,
                CustomMinimumSize = new Vector2(108f, 0f)
            };
            var maxSpin = new SpinBox
            {
                MinValue = 0,
                MaxValue = 99,
                Step = 1,
                Rounded = true,
                Value = state.Max,
                CustomMinimumSize = new Vector2(108f, 0f)
            };

            row.AddChild(CreateMutedLabel("Min", new Color(0.72f, 0.78f, 0.84f, 1f), 14));
            row.AddChild(minSpin);
            row.AddChild(CreateMutedLabel("Max", new Color(0.72f, 0.78f, 0.84f, 1f), 14));
            row.AddChild(maxSpin);
            row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            constraintControls[metric.Type] = (minSpin, maxSpin);
        }

        var priorityContent = AddSection(configColumn, "Priorities");
        var priorityHeaderLabel = priorityContent.GetChild<Label>(0);
        var priorityControls = new Dictionary<RouteMetricType, PriorityControls>();
        foreach (var metric in metrics)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 8);
            priorityContent.AddChild(row);

            row.AddChild(CreateMetricLabelWithIcon(metric, minWidth: 168f));

            var mode = new OptionButton { CustomMinimumSize = new Vector2(132f, 0f) };
            var modeOptions = metric.ObjectiveOptions.ToList();
            foreach (var option in modeOptions)
            {
                mode.AddItem(RoutingSettings.ObjectiveModeToSettingValue(option));
            }

            var priorityState = GetPriorityState(metric.Type);
            var selectedIndex = modeOptions.FindIndex(option => option == priorityState.Mode);
            mode.Select(selectedIndex >= 0 ? selectedIndex : 0);

            var priority = new SpinBox
            {
                MinValue = currentSelectionMode == RoutingSelectionMode.Weighted ? -99 : 0,
                MaxValue = currentSelectionMode == RoutingSelectionMode.Weighted ? 99 : 99,
                Step = 1,
                Rounded = true,
                Value = currentSelectionMode == RoutingSelectionMode.Weighted ? GetWeightedState(metric.Type) : priorityState.Priority,
                CustomMinimumSize = new Vector2(108f, 0f)
            };

            var modeLabel = CreateMutedLabel("Mode", new Color(0.72f, 0.78f, 0.84f, 1f), 14);
            row.AddChild(modeLabel);
            row.AddChild(mode);
            var priorityLabel = CreateMutedLabel("Priority", new Color(0.72f, 0.78f, 0.84f, 1f), 14);
            row.AddChild(priorityLabel);
            row.AddChild(priority);
            row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            priorityControls[metric.Type] = new PriorityControls(modeLabel, priorityLabel, mode, priority, modeOptions);
        }

        void PersistCurrentPriorityInputs()
        {
            foreach (var pair in priorityControls)
            {
                var controls = pair.Value;
                if (currentSelectionMode == RoutingSelectionMode.Weighted)
                {
                    SetWeightedState(pair.Key, (int)Math.Round(controls.Priority.Value));
                }
                else
                {
                    SetPriorityState(
                        pair.Key,
                        controls.Options.ElementAtOrDefault(controls.Mode.Selected),
                        (int)Math.Round(controls.Priority.Value));
                }
            }
        }

        void RefreshSelectionModeUi()
        {
            SelectionModeState = currentSelectionMode;
            ApplyModeTabStyle(lexicographicButton, currentSelectionMode == RoutingSelectionMode.Lexicographic, isLeft: true, isRight: false);
            ApplyModeTabStyle(weightedButton, currentSelectionMode == RoutingSelectionMode.Weighted, isLeft: false, isRight: true);
            priorityHeaderLabel.Text = currentSelectionMode == RoutingSelectionMode.Weighted ? "Weights" : "Priorities";
            selectionModeDescription.Text = currentSelectionMode == RoutingSelectionMode.Weighted
                ? "Weighted scoring keeps all routes within constraints and ranks them by maximizing the weighted sum of each metric count."
                : "Lexicographic mode keeps only routes tied on the highest-priority metric, then breaks ties using the next priorities.";

            var valueLabel = currentSelectionMode == RoutingSelectionMode.Weighted ? "Weight" : "Priority";
            foreach (var pair in priorityControls)
            {
                var controls = pair.Value;
                var priorityState = GetPriorityState(pair.Key);
                controls.ValueLabel.Text = valueLabel;
                controls.ModeLabel.Visible = currentSelectionMode != RoutingSelectionMode.Weighted;
                controls.Mode.Visible = currentSelectionMode != RoutingSelectionMode.Weighted;
                controls.Priority.MinValue = currentSelectionMode == RoutingSelectionMode.Weighted ? -99 : 0;
                controls.Priority.MaxValue = 99;
                if (currentSelectionMode == RoutingSelectionMode.Weighted)
                {
                    controls.Priority.Value = GetWeightedState(pair.Key);
                    continue;
                }

                var selectedIndex = controls.Options.ToList().FindIndex(option => option == priorityState.Mode);
                controls.Mode.Select(selectedIndex >= 0 ? selectedIndex : 0);
                controls.Priority.Value = priorityState.Priority;
            }
        }

        lexicographicButton.Pressed += () =>
        {
            PersistCurrentPriorityInputs();
            currentSelectionMode = RoutingSelectionMode.Lexicographic;
            RefreshSelectionModeUi();
        };
        weightedButton.Pressed += () =>
        {
            PersistCurrentPriorityInputs();
            currentSelectionMode = RoutingSelectionMode.Weighted;
            RefreshSelectionModeUi();
        };
        RefreshSelectionModeUi();

        var summary = new Label
        {
            Text = "No route computation yet.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(0.77f, 0.81f, 0.87f, 1f),
            CustomMinimumSize = new Vector2(0f, 48f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        var tableScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 220f)
        };

        var tableGrid = new GridContainer
        {
            Columns = 6,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        tableGrid.AddThemeConstantOverride("h_separation", 0);
        tableGrid.AddThemeConstantOverride("v_separation", 0);
        tableScroll.AddChild(tableGrid);

        if (RoutingSettings.UseSeparateResultsPanel)
        {
            var resultsColumn = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(404f, 0f)
            };
            resultsColumn.AddThemeConstantOverride("separation", 12);
            contentRow.AddChild(resultsColumn);

            var resultsContent = AddSection(resultsColumn, "Route Summary");
            resultsContent.AddChild(summary);
            resultsContent.AddChild(tableScroll);
        }
        else
        {
            var resultsContent = AddSection(configColumn, "Route Summary");
            resultsContent.AddChild(summary);
            resultsContent.AddChild(tableScroll);
        }

        var actionRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actionRow.AddThemeConstantOverride("separation", 8);
        configColumn.AddChild(actionRow);
        actionRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var apply = new Button { Text = "Apply Route", CustomMinimumSize = new Vector2(126f, 34f) };
        actionRow.AddChild(apply);

        apply.Pressed += () => ApplyRouting(
            mapScreen,
            currentSelectionMode,
            constraintControls,
            priorityControls,
            summary,
            tableGrid);
        closeButton.Pressed += () => overlay.Visible = false;
        managerButton.Pressed += () =>
        {
            PersistCurrentPriorityInputs();
            OpenPresetManager(overlay, mapScreen, currentSelectionMode, activePresetLabel, constraintControls, priorityControls);
        };

        AttachWindowPlacement(mapScreen, solverWindow);
        AttachDragBehavior(titleBar, mapScreen, solverWindow);
        PopulateMetricTable(tableGrid, Array.Empty<RouteMetricSummary>());
        return overlay;
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
            CustomMinimumSize = minWidth > 0f ? new Vector2(minWidth, 0f) : Vector2.Zero,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Button CreateModeTabButton(string text)
    {
        return new Button
        {
            Text = text,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 34f)
        };
    }

    private static void ApplyModeTabStyle(Button button, bool selected, bool isLeft, bool isRight)
    {
        var style = new StyleBoxFlat
        {
            BgColor = selected
                ? new Color(0.27f, 0.39f, 0.49f, 0.98f)
                : new Color(0.13f, 0.18f, 0.23f, 0.98f),
            BorderColor = selected
                ? new Color(0.62f, 0.76f, 0.88f, 0.92f)
                : new Color(0.30f, 0.40f, 0.50f, 0.72f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = isLeft ? 8 : 0,
            CornerRadiusBottomLeft = isLeft ? 8 : 0,
            CornerRadiusTopRight = isRight ? 8 : 0,
            CornerRadiusBottomRight = isRight ? 8 : 0
        };

        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", style);
        button.AddThemeStyleboxOverride("focus", style);
        button.AddThemeColorOverride("font_color", selected ? Colors.White : new Color(0.82f, 0.88f, 0.94f, 0.92f));
    }


    private static Control CreateMetricLabelWithIcon(RouteMetricDefinition metric, float minWidth = 0f)
    {
        var container = new HBoxContainer
        {
            CustomMinimumSize = minWidth > 0f ? new Vector2(minWidth, 0f) : Vector2.Zero,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        container.AddThemeConstantOverride("separation", 6);

        var iconPath = metric.IconPath;
        if (!string.IsNullOrEmpty(iconPath))
        {
            try
            {
                var texture = PreloadManager.Cache.GetCompressedTexture2D(iconPath);
                if (texture != null)
                {
                    var icon = new TextureRect
                    {
                        Texture = texture,
                        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                        CustomMinimumSize = new Vector2(22f, 22f),
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        MouseFilter = Control.MouseFilterEnum.Ignore
                    };
                    container.AddChild(icon);
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[BetterMapTools] Could not load metric icon '{iconPath}': {ex.Message}");
            }
        }

        var label = new Label
        {
            Text = metric.Label,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_color", new Color(0.9f, 0.92f, 0.96f, 0.92f));
        container.AddChild(label);

        return container;
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

    private static StyleBoxFlat CreateMetricCellStyle(bool header, int rowIndex)
    {
        var bgColor = header
            ? new Color(0.19f, 0.26f, 0.33f, 0.94f)
            : rowIndex % 2 == 0
                ? new Color(0.13f, 0.17f, 0.22f, 0.72f)
                : new Color(0.10f, 0.14f, 0.18f, 0.72f);

        return new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderColor = header
                ? new Color(0.26f, 0.34f, 0.42f, 0.68f)
                : new Color(0.22f, 0.30f, 0.38f, 0.58f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1
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

    private static void ApplyRouting(
        NMapScreen mapScreen,
        RoutingSelectionMode selectionMode,
        IReadOnlyDictionary<RouteMetricType, (SpinBox Min, SpinBox Max)> constraintControls,
        IReadOnlyDictionary<RouteMetricType, PriorityControls> priorityControls,
        Label summary,
        GridContainer tableGrid)
    {
        try
        {
            var metrics = RouteMetricRegistry.Definitions;
            var constraints = new List<RoutingConstraint>();
            foreach (var metric in metrics)
            {
                var (minControl, maxControl) = constraintControls[metric.Type];
                var min = (int)Math.Round(minControl.Value);
                var max = (int)Math.Round(maxControl.Value);
                if (min > max)
                {
                    (min, max) = (max, min);
                }

                ConstraintState[metric.Type] = (min, max);
                constraints.Add(new RoutingConstraint
                {
                    Metric = metric.Type,
                    Min = min,
                    Max = max
                });
            }

            var priorities = new List<RoutingPriorityRule>();
            foreach (var metric in metrics)
            {
                var controls = priorityControls[metric.Type];
                var priority = (int)Math.Round(controls.Priority.Value);
                if (selectionMode == RoutingSelectionMode.Weighted)
                {
                    SetWeightedState(metric.Type, priority);
                    priorities.Add(new RoutingPriorityRule
                    {
                        Metric = metric.Type,
                        Mode = RouteObjectiveMode.None,
                        Priority = priority
                    });
                }
                else
                {
                    var mode = controls.Options.ElementAtOrDefault(controls.Mode.Selected);
                    SetPriorityState(metric.Type, mode, priority);
                    priorities.Add(new RoutingPriorityRule
                    {
                        Metric = metric.Type,
                        Mode = mode,
                        Priority = priority
                    });
                }
            }

            RoutingSettingsRegistration.PersistCurrentValuesIfReady();

            var runState = RunManager.Instance.DebugOnlyGetState();
            if (runState == null)
            {
                summary.Text = "No active run state; cannot compute route.";
                summary.Modulate = new Color(0.95f, 0.46f, 0.46f, 1f);
                PopulateMetricTable(tableGrid, Array.Empty<RouteMetricSummary>());
                return;
            }

            var request = new RoutingRequest
            {
                Constraints = constraints,
                Priorities = priorities,
                SelectionMode = selectionMode
            };

            var map = runState.Map;
            var start = runState.CurrentMapPoint ?? map.StartingMapPoint;
            var boss = map.BossMapPoint;

            var result = RouteSolver.Solve(start, boss, request);
            var renderer = new RouteOverlayRenderer(mapScreen);
            renderer.Render(result.Routes);

            summary.Text = result.StatusMessage;
            summary.Modulate = result.Found
                ? new Color(0.55f, 0.95f, 0.55f, 1f)
                : new Color(0.95f, 0.46f, 0.46f, 1f);
            PopulateMetricTable(tableGrid, result.MetricSummaries);

        }
        catch (Exception ex)
        {
            summary.Text = $"Failed to apply routing: {ex.Message}";
            summary.Modulate = new Color(0.95f, 0.46f, 0.46f, 1f);
            PopulateMetricTable(tableGrid, Array.Empty<RouteMetricSummary>());
            Log.Error($"[BetterMapTools] Failed applying route highlight. {ex}");
        }
    }


    private static void PopulateMetricTable(GridContainer tableGrid, IReadOnlyList<RouteMetricSummary> summaries)
    {
        foreach (Node child in tableGrid.GetChildren())
        {
            tableGrid.RemoveChild(child);
            child.QueueFree();
        }

        AddCell(tableGrid, "Route Metric", header: true, rowIndex: -1, HorizontalAlignment.Left);
        AddCell(tableGrid, "Selected", header: true, rowIndex: -1, HorizontalAlignment.Center);
        AddCell(tableGrid, "Global Min", header: true, rowIndex: -1, HorizontalAlignment.Center);
        AddCell(tableGrid, "Global Max", header: true, rowIndex: -1, HorizontalAlignment.Center);
        AddCell(tableGrid, "Feasible Min", header: true, rowIndex: -1, HorizontalAlignment.Center);
        AddCell(tableGrid, "Feasible Max", header: true, rowIndex: -1, HorizontalAlignment.Center);

        for (var rowIndex = 0; rowIndex < summaries.Count; rowIndex++)
        {
            var summary = summaries[rowIndex];
            var selectedRange = summary.SelectedMin == summary.SelectedMax
                ? summary.SelectedMin.ToString()
                : $"{summary.SelectedMin}-{summary.SelectedMax}";

            var metric = RouteMetricRegistry.Definitions.FirstOrDefault(m => m.Type == summary.Metric);
            AddMetricCell(tableGrid, metric, rowIndex);
            AddCell(tableGrid, selectedRange, header: false, rowIndex, HorizontalAlignment.Center);
            AddCell(tableGrid, summary.GlobalMin.ToString(), header: false, rowIndex, HorizontalAlignment.Center);
            AddCell(tableGrid, summary.GlobalMax.ToString(), header: false, rowIndex, HorizontalAlignment.Center);
            AddCell(tableGrid, summary.FeasibleMin.ToString(), header: false, rowIndex, HorizontalAlignment.Center);
            AddCell(tableGrid, summary.FeasibleMax.ToString(), header: false, rowIndex, HorizontalAlignment.Center);
        }
    }

    private static void AddCell(GridContainer grid, string text, bool header, int rowIndex, HorizontalAlignment alignment)
    {
        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(header ? 92f : 88f, header ? 34f : 32f)
        };
        panel.AddThemeStyleboxOverride("panel", CreateMetricCellStyle(header, rowIndex));

        var margin = new MarginContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 6);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        panel.AddChild(margin);

        var label = new Label
        {
            Text = text,
            ClipText = true,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", header ? 15 : 16);
        label.AddThemeColorOverride("font_color", header ? Colors.White : new Color(0.88f, 0.92f, 0.97f, 1f));
        margin.AddChild(label);

        grid.AddChild(panel);
    }

    private static void AddMetricCell(GridContainer grid, RouteMetricDefinition? metric, int rowIndex)
    {
        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(88f, 32f)
        };
        panel.AddThemeStyleboxOverride("panel", CreateMetricCellStyle(false, rowIndex));

        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 6);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        panel.AddChild(margin);

        margin.AddChild(metric != null
            ? CreateMetricLabelWithIcon(metric)
            : new Label
            {
                Text = "?",
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

        grid.AddChild(panel);
    }
}
