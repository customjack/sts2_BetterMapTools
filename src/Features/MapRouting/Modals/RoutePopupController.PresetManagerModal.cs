using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RoutingHelper.Features.MapRouting.Metrics;
using RoutingHelper.Features.Settings;

namespace RoutingHelper.Features.MapRouting.Modals;

internal static partial class RoutePopupController
{
    private static void OpenPresetManager(
        Control backdrop,
        Control solverPanel,
        Label activePresetLabel,
        IReadOnlyDictionary<RouteMetricType, (SpinBox Min, SpinBox Max)> constraintControls,
        IReadOnlyDictionary<RouteMetricType, PriorityControls> priorityControls)
    {
        var existing = backdrop.GetNodeOrNull<Control>(PresetManagerName);
        if (existing != null)
        {
            MapModalLayout.ApplyCenteredPanelLayout(existing);
            solverPanel.Visible = false;
            existing.Visible = true;
            return;
        }

        var managerBackdrop = new PanelContainer
        {
            Name = PresetManagerName,
            CustomMinimumSize = new Vector2(MapModalLayout.PanelWidth, MapModalLayout.PanelHeight),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        MapModalLayout.ApplyCenteredPanelLayout(managerBackdrop);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        managerBackdrop.AddChild(margin);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        root.AddChild(new Label
        {
            Text = "Preset Manager",
            Modulate = Colors.White
        });

        var status = new Label
        {
            Text = "Select a preset to load, or enter a name to add/save current solver values.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(0.77f, 0.81f, 0.87f, 1f),
            CustomMinimumSize = new Vector2(0f, 42f)
        };
        root.AddChild(status);

        var list = new ItemList
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            AllowRmbSelect = true,
            SelectMode = ItemList.SelectModeEnum.Single
        };
        root.AddChild(list);

        var newName = new LineEdit
        {
            PlaceholderText = "New preset name...",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(newName);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 6);
        root.AddChild(buttons);

        var addSaveCurrent = new Button { Text = "Add/Save Current" };
        var delete = new Button { Text = "Delete Selected" };
        var load = new Button { Text = "Load Selected" };
        var close = new Button { Text = "Back" };
        buttons.AddChild(addSaveCurrent);
        buttons.AddChild(delete);
        buttons.AddChild(load);
        buttons.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        buttons.AddChild(close);

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

        addSaveCurrent.Pressed += () =>
        {
            var name = (newName.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                status.Text = "Preset name is required.";
                status.Modulate = new Color(0.95f, 0.46f, 0.46f, 1f);
                return;
            }

            SavePresetFromControls(name, constraintControls, priorityControls, status);
            activePresetLabel.Text = $"Active preset: {RoutingSettings.ActivePresetName}";
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
                status.Text = $"Cannot delete preset '{name}'.";
                status.Modulate = new Color(0.95f, 0.46f, 0.46f, 1f);
                return;
            }

            RoutingSettingsRegistration.RefreshPresetSettings();
            status.Text = $"Deleted preset '{name}'.";
            status.Modulate = new Color(0.55f, 0.95f, 0.55f, 1f);
            activePresetLabel.Text = $"Active preset: {RoutingSettings.ActivePresetName}";
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
            if (LoadPresetIntoControls(name, constraintControls, priorityControls))
            {
                status.Text = $"Loaded preset '{name}'.";
                status.Modulate = new Color(0.55f, 0.95f, 0.55f, 1f);
                activePresetLabel.Text = $"Active preset: {RoutingSettings.ActivePresetName}";
            }
            else
            {
                status.Text = $"Could not load preset '{name}'.";
                status.Modulate = new Color(0.95f, 0.46f, 0.46f, 1f);
            }
        };

        close.Pressed += () =>
        {
            managerBackdrop.Visible = false;
            solverPanel.Visible = true;
        };

        RefreshList(RoutingSettings.ActivePresetName);
        backdrop.AddChild(managerBackdrop);
        solverPanel.Visible = false;
    }

    private static void SavePresetFromControls(
        string name,
        IReadOnlyDictionary<RouteMetricType, (SpinBox Min, SpinBox Max)> constraintControls,
        IReadOnlyDictionary<RouteMetricType, PriorityControls> priorityControls,
        Label status)
    {
        var constraints = ReadConstraintState(constraintControls);
        var priorities = ReadPriorityState(priorityControls);
        if (!RoutingSettings.SavePreset(name, constraints, priorities))
        {
            status.Text = $"Failed to save preset '{name}'.";
            status.Modulate = new Color(0.95f, 0.46f, 0.46f, 1f);
            return;
        }

        RoutingSettings.SetActivePreset(name);
        RoutingSettingsRegistration.RefreshPresetSettings();
        status.Text = $"Saved preset '{name}'.";
        status.Modulate = new Color(0.55f, 0.95f, 0.55f, 1f);
    }

    private static bool LoadPresetIntoControls(
        string name,
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
            PriorityState[metric.Type] = (priority.Mode, priority.Priority);

            var controls = constraintControls[metric.Type];
            controls.Min.Value = constraint.Min;
            controls.Max.Value = constraint.Max;

            var priorityControl = priorityControls[metric.Type];
            var selectedIndex = priorityControl.Options.ToList().FindIndex(option => option == priority.Mode);
            priorityControl.Mode.Select(selectedIndex >= 0 ? selectedIndex : 0);
            priorityControl.Priority.Value = priority.Priority;
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
