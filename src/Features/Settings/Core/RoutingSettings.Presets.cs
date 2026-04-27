using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BetterMapTools.Features.MapRouting;
using BetterMapTools.Features.MapRouting.Metrics;

namespace BetterMapTools.Features.Settings;

internal static partial class RoutingSettings
{
    private sealed class PresetPayload
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, PresetMetricPayload> Metrics { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class PresetMetricPayload
    {
        public int Min { get; set; }
        public int Max { get; set; }
        public string Mode { get; set; } = "none";
        public int Priority { get; set; }
        public int Weight { get; set; }
    }

    public static void SavePreset(
        string name,
        IReadOnlyDictionary<RouteMetricType, (int Min, int Max)> constraints,
        IReadOnlyDictionary<RouteMetricType, (RouteObjectiveMode Mode, int Priority)> priorities,
        IReadOnlyDictionary<RouteMetricType, int> weights)
    {
        EnsureDefaultsInitialized();
        var normalizedName = NormalizePresetName(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return;
        }

        var existing = PresetsByKey.TryGetValue(PresetKey(normalizedName), out var ex) ? ex : null;

        var constraintCopy = new Dictionary<RouteMetricType, ConstraintDefaults>();
        var priorityCopy = new Dictionary<RouteMetricType, PriorityDefaults>();
        var weightCopy = new Dictionary<RouteMetricType, int>();

        foreach (var definition in RouteMetricRegistry.Definitions)
        {
            var fallbackConstraint = GetConstraintDefaults(definition);
            var fallbackPriority = GetPriorityDefaults(definition);
            var fallbackWeight = existing != null && existing.Weights.TryGetValue(definition.Type, out var ew) ? ew : GetWeightDefault(definition);

            constraintCopy[definition.Type] = constraints.TryGetValue(definition.Type, out var c)
                ? new ConstraintDefaults(Math.Min(c.Min, c.Max), Math.Max(c.Min, c.Max))
                : fallbackConstraint;
            priorityCopy[definition.Type] = priorities.TryGetValue(definition.Type, out var p)
                ? new PriorityDefaults(p.Mode, p.Priority)
                : fallbackPriority;
            weightCopy[definition.Type] = weights.TryGetValue(definition.Type, out var w) ? w : fallbackWeight;
        }

        PresetsByKey[PresetKey(normalizedName)] = new RoutingPreset
        {
            Name = normalizedName,
            Constraints = constraintCopy,
            Priorities = priorityCopy,
            Weights = weightCopy
        };
    }

    public static void SavePreset(
        string name,
        IReadOnlyDictionary<RouteMetricType, ConstraintDefaults> constraints,
        IReadOnlyDictionary<RouteMetricType, PriorityDefaults> priorities,
        IReadOnlyDictionary<RouteMetricType, int> weights)
    {
        var normalizedName = NormalizePresetName(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return;
        }

        var copiedConstraints = constraints.ToDictionary(
            pair => pair.Key,
            pair => new ConstraintDefaults(Math.Min(pair.Value.Min, pair.Value.Max), Math.Max(pair.Value.Min, pair.Value.Max)));
        var copiedPriorities = priorities.ToDictionary(
            pair => pair.Key,
            pair => new PriorityDefaults(pair.Value.Mode, pair.Value.Priority));
        var copiedWeights = weights.ToDictionary(pair => pair.Key, pair => pair.Value);

        PresetsByKey[PresetKey(normalizedName)] = new RoutingPreset
        {
            Name = normalizedName,
            Constraints = copiedConstraints,
            Priorities = copiedPriorities,
            Weights = copiedWeights
        };
    }

    public static string SerializePresetToJson(string name)
    {
        EnsureDefaultsInitialized();
        if (!TryGetPreset(name, out var preset))
        {
            return "{}";
        }

        var payload = new PresetPayload
        {
            Name = preset.Name,
            Metrics = new Dictionary<string, PresetMetricPayload>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var definition in RouteMetricRegistry.Definitions)
        {
            var constraint = preset.Constraints.TryGetValue(definition.Type, out var c)
                ? c
                : new ConstraintDefaults(definition.DefaultMin, definition.DefaultMax);
            var priority = preset.Priorities.TryGetValue(definition.Type, out var p)
                ? p
                : new PriorityDefaults(definition.DefaultObjectiveMode, definition.DefaultPriority);
            var weight = preset.Weights.TryGetValue(definition.Type, out var w) ? w : 0;

            payload.Metrics[definition.Type.ToString()] = new PresetMetricPayload
            {
                Min = constraint.Min,
                Max = constraint.Max,
                Mode = ObjectiveModeToSettingValue(priority.Mode),
                Priority = priority.Priority,
                Weight = weight
            };
        }

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    public static bool ApplyPresetJson(string name, string json)
    {
        EnsureDefaultsInitialized();
        PresetPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<PresetPayload>(json);
        }
        catch
        {
            return false;
        }

        if (payload?.Metrics == null)
        {
            return false;
        }

        var normalizedName = NormalizePresetName(string.IsNullOrWhiteSpace(payload.Name) ? name : payload.Name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return false;
        }

        var constraints = new Dictionary<RouteMetricType, ConstraintDefaults>();
        var priorities = new Dictionary<RouteMetricType, PriorityDefaults>();
        var weights = new Dictionary<RouteMetricType, int>();
        foreach (var definition in RouteMetricRegistry.Definitions)
        {
            if (!payload.Metrics.TryGetValue(definition.Type.ToString(), out var metricPayload))
            {
                constraints[definition.Type] = new ConstraintDefaults(definition.DefaultMin, definition.DefaultMax);
                priorities[definition.Type] = new PriorityDefaults(definition.DefaultObjectiveMode, definition.DefaultPriority);
                weights[definition.Type] = 0;
                continue;
            }

            var min = Math.Min(metricPayload.Min, metricPayload.Max);
            var max = Math.Max(metricPayload.Min, metricPayload.Max);
            constraints[definition.Type] = new ConstraintDefaults(min, max);
            priorities[definition.Type] = new PriorityDefaults(
                ParseObjectiveMode(metricPayload.Mode, definition.DefaultObjectiveMode),
                metricPayload.Priority);
            weights[definition.Type] = metricPayload.Weight;
        }

        SavePreset(normalizedName, constraints, priorities, weights);
        return true;
    }

    public static string PathSafePresetName(string name)
    {
        var normalized = NormalizePresetName(name);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Unnamed";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(normalized
            .Select(ch => invalid.Contains(ch) || ch == '/' || ch == '\\' ? '_' : ch)
            .ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "Unnamed" : cleaned;
    }

}
