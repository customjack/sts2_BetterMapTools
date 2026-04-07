using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using BetterMapTools.Features.MapRouting.Metrics;
using BetterMapTools.Features.Settings;

namespace BetterMapTools.Features.MapRouting.Modals;

internal static partial class RoutePopupController
{
    private const float PresetManagerWidth = 1080f;
    private const float PresetManagerHeight = 640f;

    private static void OpenPresetManager(
        Control overlay,
        NMapScreen mapScreen,
        RoutingSelectionMode selectionMode,
        Label activePresetLabel,
        IReadOnlyDictionary<RouteMetricType, (SpinBox Min, SpinBox Max)> constraintControls,
        IReadOnlyDictionary<RouteMetricType, PriorityControls> priorityControls)
    {
        overlay.GetNodeOrNull<Control>(PresetManagerName)?.QueueFree();
        var hiddenControls = overlay.GetChildren()
            .OfType<Control>()
            .Where(control => control.Visible)
            .ToList();
        foreach (var control in hiddenControls)
        {
            control.Visible = false;
        }

        var managerWindow = new PanelContainer
        {
            Name = PresetManagerName,
            CustomMinimumSize = new Vector2(PresetManagerWidth, PresetManagerHeight),
            Size = new Vector2(PresetManagerWidth, PresetManagerHeight),
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.All
        };
        managerWindow.AddThemeStyleboxOverride("panel", CreateWindowStyle());
        overlay.AddChild(managerWindow);

        var outerMargin = new MarginContainer();
        outerMargin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        outerMargin.AddThemeConstantOverride("margin_left", 14);
        outerMargin.AddThemeConstantOverride("margin_top", 14);
        outerMargin.AddThemeConstantOverride("margin_right", 14);
        outerMargin.AddThemeConstantOverride("margin_bottom", 14);
        managerWindow.AddChild(outerMargin);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 12);
        outerMargin.AddChild(root);

        // Title bar (matches solver style)
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
        titleStack.AddChild(CreateMutedLabel("Preset Manager", new Color(0.78f, 0.84f, 0.9f, 0.88f), 15));

        var closeButton = new Button { Text = "Back", CustomMinimumSize = new Vector2(96f, 34f) };
        titleRow.AddChild(closeButton);

        var contentScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            FollowFocus = true
        };
        root.AddChild(contentScroll);

        // Content section
        var contentSection = AddSection(contentScroll, "Presets");
        contentSection.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

        var status = new Label
        {
            Text = "Select a preset to load, or enter a name to add/save current solver values.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 40f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        status.AddThemeFontSizeOverride("font_size", 14);
        status.AddThemeColorOverride("font_color", new Color(0.77f, 0.81f, 0.87f, 1f));
        contentSection.AddChild(status);

        var list = new ItemList
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 180f),
            AllowRmbSelect = true,
            SelectMode = ItemList.SelectModeEnum.Single
        };
        contentSection.AddChild(list);

        var newName = new LineEdit
        {
            PlaceholderText = "New preset name...",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        contentSection.AddChild(newName);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 6);
        contentSection.AddChild(buttons);

        var addSaveCurrent = new Button { Text = "Add/Save Current" };
        var delete = new Button { Text = "Delete Selected" };
        var load = new Button { Text = "Load Selected" };
        buttons.AddChild(addSaveCurrent);
        buttons.AddChild(delete);
        buttons.AddChild(load);
        buttons.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        void RefreshList(string preferred = "")
        {
            list.Clear();
            var names = RoutingSettings.GetPresetNames();
            var selectIndex = -1;
            for (var i = 0; i < names.Count; i++)
            {
                list.AddItem(names[i]);
                if (string.Equals(names[i], preferred, StringComparison.OrdinalIgnoreCase))
                {
                    selectIndex = i;
                }
            }

            if (selectIndex < 0 && names.Count > 0)
            {
                selectIndex = 0;
            }

            if (selectIndex >= 0)
            {
                list.Select(selectIndex);
            }
        }

        void SetStatus(string text, bool success)
        {
            status.Text = text;
            status.AddThemeColorOverride("font_color", success
                ? new Color(0.55f, 0.95f, 0.55f, 1f)
                : new Color(0.95f, 0.46f, 0.46f, 1f));
        }

        addSaveCurrent.Pressed += () =>
        {
            var name = (newName.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                SetStatus("Preset name is required.", false);
                return;
            }

            SavePresetFromControls(name, selectionMode, constraintControls, priorityControls, status);
            activePresetLabel.Text = $"Preset: {RoutingSettings.ActivePresetName}";
            RefreshList(name);
            newName.Clear();
        };

        delete.Pressed += () =>
        {
            var selected = list.GetSelectedItems();
            if (selected.Length == 0)
            {
                return;
            }

            var name = list.GetItemText(selected[0]);
            if (!RoutingSettings.DeletePreset(name))
            {
                SetStatus($"Cannot delete preset '{name}'.", false);
                return;
            }

            RoutingSettingsRegistration.RefreshPresetSettings();
            SetStatus($"Deleted preset '{name}'.", true);
            activePresetLabel.Text = $"Preset: {RoutingSettings.ActivePresetName}";
            RefreshList(RoutingSettings.ActivePresetName);
        };

        load.Pressed += () =>
        {
            var selected = list.GetSelectedItems();
            if (selected.Length == 0)
            {
                return;
            }

            var name = list.GetItemText(selected[0]);
            if (LoadPresetIntoControls(name, selectionMode, constraintControls, priorityControls))
            {
                SetStatus($"Loaded preset '{name}'.", true);
                activePresetLabel.Text = $"Preset: {RoutingSettings.ActivePresetName}";
            }
            else
            {
                SetStatus($"Could not load preset '{name}'.", false);
            }
        };

        void CloseManager()
        {
            managerWindow.QueueFree();
        }

        managerWindow.TreeExiting += () =>
        {
            foreach (var control in hiddenControls)
            {
                if (GodotObject.IsInstanceValid(control))
                {
                    control.Visible = true;
                }
            }
        };

        closeButton.Pressed += CloseManager;

        RefreshList(RoutingSettings.ActivePresetName);

        AttachPresetManagerPlacement(mapScreen, managerWindow);
        AttachDragBehavior(titleBar, mapScreen, managerWindow);
    }

    private static void AttachPresetManagerPlacement(NMapScreen mapScreen, Control managerWindow)
    {
        var hasCustomPosition = false;

        void PlaceWindow()
        {
            if (!GodotObject.IsInstanceValid(mapScreen) || !GodotObject.IsInstanceValid(managerWindow))
            {
                return;
            }

            var viewportSize = mapScreen.Size;
            if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
            {
                viewportSize = mapScreen.GetWindow()?.ContentScaleSize ?? viewportSize;
            }

            var desired = hasCustomPosition
                ? managerWindow.Position
                : new Vector2(
                    (viewportSize.X - managerWindow.Size.X) * 0.5f,
                    (viewportSize.Y - managerWindow.Size.Y) * 0.5f);
            managerWindow.Position = ClampWindowPosition(viewportSize, managerWindow.Size, desired);
        }

        managerWindow.Resized += PlaceWindow;
        mapScreen.Resized += PlaceWindow;
        Callable.From(PlaceWindow).CallDeferred();
        PlaceWindow();

        managerWindow.SetMeta("bettermaptools_has_custom_position", hasCustomPosition);
        managerWindow.Connect(Control.SignalName.GuiInput, Callable.From<InputEvent>(_ =>
        {
            if (managerWindow.HasMeta("bettermaptools_has_custom_position"))
            {
                hasCustomPosition = managerWindow.GetMeta("bettermaptools_has_custom_position").AsBool();
            }
        }));
    }

    private static void SavePresetFromControls(
        string name,
        RoutingSelectionMode selectionMode,
        IReadOnlyDictionary<RouteMetricType, (SpinBox Min, SpinBox Max)> constraintControls,
        IReadOnlyDictionary<RouteMetricType, PriorityControls> priorityControls,
        Label status)
    {
        var constraints = ReadConstraintState(constraintControls);
        var priorities = selectionMode == RoutingSelectionMode.Weighted
            ? PriorityState.ToDictionary(pair => pair.Key, pair => pair.Value)
            : ReadPriorityState(priorityControls);
        RoutingSettings.SavePreset(name, constraints, priorities);

        RoutingSettings.SetActivePreset(name);
        RoutingSettingsRegistration.RefreshPresetSettings();
        status.Text = $"Saved preset '{name}'.";
        status.AddThemeColorOverride("font_color", new Color(0.55f, 0.95f, 0.55f, 1f));
    }

    private static bool LoadPresetIntoControls(
        string name,
        RoutingSelectionMode selectionMode,
        IReadOnlyDictionary<RouteMetricType, (SpinBox Min, SpinBox Max)> constraintControls,
        IReadOnlyDictionary<RouteMetricType, PriorityControls> priorityControls)
    {
        if (!RoutingSettings.LoadPresetIntoDefaults(name))
        {
            return false;
        }

        foreach (var metric in RouteMetricRegistry.Definitions)
        {
            var constraint = RoutingSettings.GetConstraintDefaults(metric);
            var priority = RoutingSettings.GetPriorityDefaults(metric);

            ConstraintState[metric.Type] = (constraint.Min, constraint.Max);
            SetPriorityState(metric.Type, priority.Mode, priority.Priority);

            var controls = constraintControls[metric.Type];
            controls.Min.Value = constraint.Min;
            controls.Max.Value = constraint.Max;

            if (selectionMode == RoutingSelectionMode.Lexicographic)
            {
                var priorityControl = priorityControls[metric.Type];
                var selectedIndex = priorityControl.Options.ToList().FindIndex(option => option == priority.Mode);
                priorityControl.Mode.Select(selectedIndex >= 0 ? selectedIndex : 0);
                priorityControl.Priority.Value = priority.Priority;
            }
        }

        return true;
    }

    private static Dictionary<RouteMetricType, (int Min, int Max)> ReadConstraintState(
        IReadOnlyDictionary<RouteMetricType, (SpinBox Min, SpinBox Max)> constraintControls)
    {
        var result = new Dictionary<RouteMetricType, (int Min, int Max)>();
        foreach (var metric in RouteMetricRegistry.Definitions)
        {
            var controls = constraintControls[metric.Type];
            var min = (int)Math.Round(controls.Min.Value);
            var max = (int)Math.Round(controls.Max.Value);
            if (min > max)
            {
                (min, max) = (max, min);
            }

            result[metric.Type] = (min, max);
        }

        return result;
    }

    private static Dictionary<RouteMetricType, (RouteObjectiveMode Mode, int Priority)> ReadPriorityState(
        IReadOnlyDictionary<RouteMetricType, PriorityControls> priorityControls)
    {
        var result = new Dictionary<RouteMetricType, (RouteObjectiveMode Mode, int Priority)>();
        foreach (var metric in RouteMetricRegistry.Definitions)
        {
            var controls = priorityControls[metric.Type];
            var mode = controls.Options.ElementAtOrDefault(controls.Mode.Selected);
            var priority = (int)Math.Round(controls.Priority.Value);
            result[metric.Type] = (mode, priority);
        }

        return result;
    }
}
