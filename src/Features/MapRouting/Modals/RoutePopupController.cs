using System.Collections.Generic;
using Godot;
using RoutingHelper.Features.MapRouting.Metrics;
using RoutingHelper.Features.Settings;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace RoutingHelper.Features.MapRouting.Modals;

internal static partial class RoutePopupController
{
    private const string PopupName = "RoutingHelperPopup";
    private const string PresetManagerName = "RoutingHelperPresetManager";

    private static readonly Dictionary<RouteMetricType, (int Min, int Max)> ConstraintState = new();
    private static readonly Dictionary<RouteMetricType, (RouteObjectiveMode Mode, int Priority)> PriorityState = new();

    private readonly record struct PriorityControls(OptionButton Mode, SpinBox Priority, IReadOnlyList<RouteObjectiveMode> Options);

    public static void Toggle(NMapScreen mapScreen)
    {
        InitializeStateFromSettingsDefaults();

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
        }
    }
}
