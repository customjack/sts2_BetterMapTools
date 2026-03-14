using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RoutingHelper.Features.MapRouting.Metrics;
using RoutingHelper.Features.Settings;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace RoutingHelper.Features.MapRouting.Modals;

internal static partial class RoutePopupController
{
    private static Control BuildPopup(NMapScreen mapScreen)
    {
        var metrics = RouteMetricRegistry.Definitions;

        var backdrop = new ColorRect
        {
            Name = PopupName,
            AnchorLeft = 0f,
            AnchorTop = 0f,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Color = new Color(0f, 0f, 0f, 0.45f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(MapModalLayout.PanelWidth, MapModalLayout.PanelHeight),
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            Position = MapModalLayout.PanelPosition,
            Size = new Vector2(MapModalLayout.PanelWidth, MapModalLayout.PanelHeight)
        };
        backdrop.AddChild(panel);

        var margin = new MarginContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        root.AddChild(new Label
        {
            Text = "Routing Helper",
            HorizontalAlignment = HorizontalAlignment.Left,
            Modulate = Colors.White
        });

        var presetRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        presetRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(presetRow);

        var activePresetLabel = new Label
        {
            Text = $"Active preset: {RoutingSettings.ActivePresetName}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        presetRow.AddChild(activePresetLabel);

        var managerButton = new Button { Text = "Preset Manager", CustomMinimumSize = new Vector2(148f, 30f) };
        presetRow.AddChild(managerButton);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            FollowFocus = true
        };
        root.AddChild(scroll);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(content);

        content.AddChild(new Label
        {
            Text = "Constrained",
            Modulate = Colors.White
        });

        var constraintControls = new Dictionary<RouteMetricType, (SpinBox Min, SpinBox Max)>();
        foreach (var metric in metrics)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 8);
            content.AddChild(row);

            row.AddChild(new Label
            {
                Text = metric.Label,
                CustomMinimumSize = new Vector2(170f, 0f),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
            });

            var state = ConstraintState[metric.Type];

            var minSpin = new SpinBox
            {
                MinValue = 0,
                MaxValue = 99,
                Step = 1,
                Rounded = true,
                Value = state.Min,
                CustomMinimumSize = new Vector2(120f, 0f)
            };
            var maxSpin = new SpinBox
            {
                MinValue = 0,
                MaxValue = 99,
                Step = 1,
                Rounded = true,
                Value = state.Max,
                CustomMinimumSize = new Vector2(120f, 0f)
            };

            row.AddChild(new Label { Text = "min" });
            row.AddChild(minSpin);
            row.AddChild(new Label { Text = "max" });
            row.AddChild(maxSpin);
            row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            constraintControls[metric.Type] = (minSpin, maxSpin);
        }

        content.AddChild(new HSeparator());

        content.AddChild(new Label
        {
            Text = "Lexicographic Priorities",
            Modulate = Colors.White
        });

        var priorityControls = new Dictionary<RouteMetricType, PriorityControls>();
        foreach (var metric in metrics)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 8);
            content.AddChild(row);

            row.AddChild(new Label
            {
                Text = metric.Label,
                CustomMinimumSize = new Vector2(170f, 0f),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
            });

            var mode = new OptionButton { CustomMinimumSize = new Vector2(130f, 0f) };
            var modeOptions = metric.ObjectiveOptions.ToList();
            foreach (var option in modeOptions)
            {
                mode.AddItem(RoutingSettings.ObjectiveModeToSettingValue(option));
            }

            var state = PriorityState[metric.Type];
            var selectedIndex = modeOptions.FindIndex(option => option == state.Mode);
            mode.Select(selectedIndex >= 0 ? selectedIndex : 0);

            var priority = new SpinBox
            {
                MinValue = 0,
                MaxValue = 99,
                Step = 1,
                Rounded = true,
                Value = state.Priority,
                CustomMinimumSize = new Vector2(120f, 0f)
            };

            row.AddChild(new Label { Text = "mode" });
            row.AddChild(mode);
            row.AddChild(new Label { Text = "priority" });
            row.AddChild(priority);
            row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            priorityControls[metric.Type] = new PriorityControls(mode, priority, modeOptions);
        }

        var summary = new Label
        {
            Text = "No route computation yet.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(0.77f, 0.81f, 0.87f, 1f),
            CustomMinimumSize = new Vector2(0f, 52f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(summary);

        var tableScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 180f)
        };
        root.AddChild(tableScroll);

        var tableGrid = new GridContainer
        {
            Columns = 6,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        tableGrid.AddThemeConstantOverride("h_separation", 12);
        tableGrid.AddThemeConstantOverride("v_separation", 4);
        tableScroll.AddChild(tableGrid);

        var actionRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actionRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(actionRow);
        actionRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var apply = new Button { Text = "Apply", CustomMinimumSize = new Vector2(110f, 30f) };
        var close = new Button { Text = "Close", CustomMinimumSize = new Vector2(110f, 30f) };
        actionRow.AddChild(apply);
        actionRow.AddChild(close);

        apply.Pressed += () => ApplyRouting(mapScreen, constraintControls, priorityControls, summary, tableGrid);
        close.Pressed += () => backdrop.Visible = false;

        managerButton.Pressed += () =>
            OpenPresetManager(backdrop, panel, activePresetLabel, constraintControls, priorityControls);

        return backdrop;
    }

    private static void ApplyRouting(
        NMapScreen mapScreen,
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
                var mode = controls.Options.ElementAtOrDefault(controls.Mode.Selected);
                var priority = (int)Math.Round(controls.Priority.Value);

                PriorityState[metric.Type] = (mode, priority);
                priorities.Add(new RoutingPriorityRule
                {
                    Metric = metric.Type,
                    Mode = mode,
                    Priority = priority
                });
            }

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
                Priorities = priorities
            };

            var map = runState.Map;
            var start = runState.CurrentMapPoint ?? map.StartingMapPoint;
            var boss = map.BossMapPoint;

            var result = RouteSolver.Solve(start, boss, request);
            var renderer = new RouteOverlayRenderer(mapScreen);
            if (result.Found)
            {
                renderer.Render(result.Routes);
                summary.Text = $"Found {result.Routes.Count} route(s) (optimized from {result.FeasibleRoutes}/{result.TotalRoutes} routes within constraints).";
                summary.Modulate = new Color(0.55f, 0.95f, 0.55f, 1f);
            }
            else
            {
                renderer.Clear();
                summary.Text = $"Found {result.Routes.Count} route(s) (optimized from {result.FeasibleRoutes}/{result.TotalRoutes} routes within constraints).";
                summary.Modulate = new Color(0.95f, 0.46f, 0.46f, 1f);
            }
            PopulateMetricTable(tableGrid, result.MetricSummaries);

            Log.Info($"[RoutingHelper] {result.StatusMessage}");
        }
        catch (Exception ex)
        {
            summary.Text = $"Failed to apply routing: {ex.Message}";
            summary.Modulate = new Color(0.95f, 0.46f, 0.46f, 1f);
            PopulateMetricTable(tableGrid, Array.Empty<RouteMetricSummary>());
            Log.Error($"[RoutingHelper] Failed applying route highlight. {ex}");
        }
    }

    private static void PopulateMetricTable(GridContainer tableGrid, IReadOnlyList<RouteMetricSummary> summaries)
    {
        foreach (Node child in tableGrid.GetChildren())
        {
            tableGrid.RemoveChild(child);
            child.QueueFree();
        }

        AddCell(tableGrid, "metric", header: true);
        AddCell(tableGrid, "selected", header: true);
        AddCell(tableGrid, "global min", header: true);
        AddCell(tableGrid, "global max", header: true);
        AddCell(tableGrid, "constrained min", header: true);
        AddCell(tableGrid, "constrained max", header: true);

        foreach (var summary in summaries)
        {
            var selectedRange = summary.SelectedMin == summary.SelectedMax
                ? summary.SelectedMin.ToString()
                : $"{summary.SelectedMin}-{summary.SelectedMax}";

            AddCell(tableGrid, RouteSolver.MetricLabel(summary.Metric));
            AddCell(tableGrid, selectedRange);
            AddCell(tableGrid, summary.GlobalMin.ToString());
            AddCell(tableGrid, summary.GlobalMax.ToString());
            AddCell(tableGrid, summary.FeasibleMin.ToString());
            AddCell(tableGrid, summary.FeasibleMax.ToString());
        }
    }

    private static void AddCell(GridContainer grid, string text, bool header = false)
    {
        grid.AddChild(new Label
        {
            Text = text,
            Modulate = header ? Colors.White : new Color(0.84f, 0.88f, 0.94f, 1f),
            HorizontalAlignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(95f, 0f)
        });
    }
}
