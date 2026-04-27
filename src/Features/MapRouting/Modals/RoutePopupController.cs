using System.Collections.Generic;
using Godot;
using BetterMapTools.Features.MapRouting.Metrics;
using BetterMapTools.Features.Settings;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace BetterMapTools.Features.MapRouting.Modals;

internal static partial class RoutePopupController
{
    private const string PopupName = "BetterMapToolsPopup";
    private const string PresetManagerName = "BetterMapToolsPresetManager";

    private static readonly Dictionary<RouteMetricType, (int Min, int Max)> ConstraintState = new();
    private static readonly Dictionary<RouteMetricType, (RouteObjectiveMode Mode, int Priority)> PriorityState = new();
    private static readonly Dictionary<RouteMetricType, int> WeightedState = new();
    private static RoutingSelectionMode SelectionModeState = RoutingSelectionMode.Lexicographic;

    private readonly record struct PriorityControls(Label ModeLabel, Label ValueLabel, OptionButton Mode, SpinBox Priority, IReadOnlyList<RouteObjectiveMode> Options);

    public static void Toggle(NMapScreen mapScreen)
    {
        InitializeStateFromSettingsDefaults();
        mapScreen.GetNodeOrNull<Control>("BetterMapToolsDrawingColorPopup")?.QueueFree();

        var popup = mapScreen.GetNodeOrNull<Control>(PopupName);
        if (popup != null)
        {
            popup.Visible = !popup.Visible;
            return;
        }

        popup = BuildPopup(mapScreen);
        mapScreen.AddChild(popup);
    }

    private static void InitializeStateFromSettingsDefaults()
    {
        RouteMetricRegistry.RegisterDefaults();
        RoutingSettings.EnsureDefaultsInitialized();
        foreach (var metric in RouteMetricRegistry.Definitions)
        {
            var constraintDefaults = RoutingSettings.GetConstraintDefaults(metric);
            ConstraintState[metric.Type] = (constraintDefaults.Min, constraintDefaults.Max);

            var priorityDefaults = RoutingSettings.GetPriorityDefaults(metric);
            PriorityState[metric.Type] = (priorityDefaults.Mode, priorityDefaults.Priority);
            WeightedState[metric.Type] = RoutingSettings.GetWeightDefault(metric);
        }
    }

    private static (RouteObjectiveMode Mode, int Priority) GetPriorityState(RouteMetricType metric)
    {
        return PriorityState.GetValueOrDefault(metric, (RouteObjectiveMode.None, 0));
    }

    private static int GetWeightedState(RouteMetricType metric)
    {
        return WeightedState.GetValueOrDefault(metric, 0);
    }

    private static void SetPriorityState(RouteMetricType metric, RouteObjectiveMode mode, int priority)
    {
        PriorityState[metric] = (mode, priority);
    }

    private static void SetWeightedState(RouteMetricType metric, int weight)
    {
        WeightedState[metric] = weight;
    }
}
