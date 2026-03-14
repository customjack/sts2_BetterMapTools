using System;
using System.Collections.Generic;
using System.Linq;
using RoutingHelper.Features.MapRouting;
using RoutingHelper.Features.MapRouting.Metrics;
using Godot;

namespace RoutingHelper.Features.Settings;

internal static partial class RoutingSettings
{
    public readonly record struct ConstraintDefaults(int Min, int Max);
    public readonly record struct PriorityDefaults(RouteObjectiveMode Mode, int Priority);

    public sealed class RoutingPreset
    {
        public required string Name { get; init; }
        public required Dictionary<RouteMetricType, ConstraintDefaults> Constraints { get; init; }
        public required Dictionary<RouteMetricType, PriorityDefaults> Priorities { get; init; }
    }

    public const string DefaultHighlightColor = "#FFF2A6FF";
    public const string DefaultPresetName = "Default";

    public static string HighlightColorRaw { get; set; } = DefaultHighlightColor;
    public static string ActivePresetName { get; private set; } = DefaultPresetName;

    private static readonly Dictionary<RouteMetricType, ConstraintDefaults> ConstraintDefaultsByMetric = new();
    private static readonly Dictionary<RouteMetricType, PriorityDefaults> PriorityDefaultsByMetric = new();
    private static readonly Dictionary<string, RoutingPreset> PresetsByKey = new(StringComparer.OrdinalIgnoreCase);

    public static void EnsureDefaultsInitialized()
    {
        RouteMetricRegistry.RegisterDefaults();

        foreach (var definition in RouteMetricRegistry.Definitions)
        {
            if (!ConstraintDefaultsByMetric.ContainsKey(definition.Type))
            {
                ConstraintDefaultsByMetric[definition.Type] = new ConstraintDefaults(definition.DefaultMin, definition.DefaultMax);
            }

            if (!PriorityDefaultsByMetric.ContainsKey(definition.Type))
            {
                PriorityDefaultsByMetric[definition.Type] = new PriorityDefaults(definition.DefaultObjectiveMode, definition.DefaultPriority);
            }
        }

        if (PresetsByKey.Count == 0)
        {
            SavePreset(DefaultPresetName, ConstraintDefaultsByMetric, PriorityDefaultsByMetric);
            ActivePresetName = DefaultPresetName;
        }
    }

    public static IReadOnlyList<string> GetPresetNames()
    {
        EnsureDefaultsInitialized();
        return PresetsByKey.Values
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool DeletePreset(string name)
    {
        EnsureDefaultsInitialized();
        var normalizedName = NormalizePresetName(name);
        if (string.IsNullOrWhiteSpace(normalizedName) ||
            string.Equals(normalizedName, DefaultPresetName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var removed = PresetsByKey.Remove(PresetKey(normalizedName));
        if (removed && string.Equals(ActivePresetName, normalizedName, StringComparison.OrdinalIgnoreCase))
        {
            ActivePresetName = DefaultPresetName;
            LoadPresetIntoDefaults(DefaultPresetName);
        }

        return removed;
    }

    public static bool LoadPresetIntoDefaults(string name)
    {
        EnsureDefaultsInitialized();
        if (!TryGetPreset(name, out var preset))
        {
            return false;
        }

        foreach (var definition in RouteMetricRegistry.Definitions)
        {
            var constraint = preset.Constraints.TryGetValue(definition.Type, out var c)
                ? c
                : new ConstraintDefaults(definition.DefaultMin, definition.DefaultMax);
            var priority = preset.Priorities.TryGetValue(definition.Type, out var p)
                ? p
                : new PriorityDefaults(definition.DefaultObjectiveMode, definition.DefaultPriority);

            ConstraintDefaultsByMetric[definition.Type] = constraint;
            PriorityDefaultsByMetric[definition.Type] = priority;
        }

        ActivePresetName = preset.Name;
        return true;
    }

    public static bool SetActivePreset(string name)
    {
        return LoadPresetIntoDefaults(name);
    }

    public static void ResetAllToDefaults()
    {
        HighlightColorRaw = DefaultHighlightColor;
        ConstraintDefaultsByMetric.Clear();
        PriorityDefaultsByMetric.Clear();
        PresetsByKey.Clear();
        ActivePresetName = DefaultPresetName;
        EnsureDefaultsInitialized();
    }

    public static bool TryGetPreset(string name, out RoutingPreset preset)
    {
        EnsureDefaultsInitialized();
        return PresetsByKey.TryGetValue(PresetKey(name), out preset!);
    }

    public static ConstraintDefaults GetConstraintDefaults(RouteMetricDefinition definition)
    {
        EnsureDefaultsInitialized();
        if (ConstraintDefaultsByMetric.TryGetValue(definition.Type, out var current))
        {
            return current;
        }

        var defaults = new ConstraintDefaults(definition.DefaultMin, definition.DefaultMax);
        ConstraintDefaultsByMetric[definition.Type] = defaults;
        return defaults;
    }

    public static PriorityDefaults GetPriorityDefaults(RouteMetricDefinition definition)
    {
        EnsureDefaultsInitialized();
        if (PriorityDefaultsByMetric.TryGetValue(definition.Type, out var current))
        {
            return current;
        }

        var defaults = new PriorityDefaults(definition.DefaultObjectiveMode, definition.DefaultPriority);
        PriorityDefaultsByMetric[definition.Type] = defaults;
        return defaults;
    }

    public static void SetConstraintDefaults(RouteMetricType metric, int min, int max)
    {
        ConstraintDefaultsByMetric[metric] = new ConstraintDefaults(Math.Min(min, max), Math.Max(min, max));
    }

    public static void SetPriorityDefaults(RouteMetricType metric, RouteObjectiveMode mode, int priority)
    {
        PriorityDefaultsByMetric[metric] = new PriorityDefaults(mode, Math.Max(0, priority));
    }

    public static string ObjectiveModeToSettingValue(RouteObjectiveMode mode)
    {
        return mode switch
        {
            RouteObjectiveMode.Max => "max",
            RouteObjectiveMode.Min => "min",
            _ => "none"
        };
    }

    public static RouteObjectiveMode ParseObjectiveMode(string raw, RouteObjectiveMode fallback)
    {
        return raw.Trim().ToLowerInvariant() switch
        {
            "max" => RouteObjectiveMode.Max,
            "min" => RouteObjectiveMode.Min,
            "none" => RouteObjectiveMode.None,
            _ => fallback
        };
    }

    public static Color ResolveHighlightColor()
    {
        return TryParseColor(HighlightColorRaw, out var parsed) ? parsed : new Color(1f, 0.95f, 0.65f, 1f);
    }

    private static string NormalizePresetName(string value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static string PresetKey(string name)
    {
        return NormalizePresetName(name).ToLowerInvariant();
    }
}
