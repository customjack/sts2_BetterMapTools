using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using ModManagerSettings.Api;
using BetterMapTools.Features.Settings;

namespace BetterMapTools.Features.MapRouting.Metrics;

internal abstract class RouteMetricDefinition
{
    private static readonly IReadOnlyList<RouteObjectiveMode> DefaultObjectiveOptions =
    [
        RouteObjectiveMode.None,
        RouteObjectiveMode.Max,
        RouteObjectiveMode.Min
    ];

    public abstract RouteMetricType Type { get; }
    public abstract string Label { get; }

    /// <summary>Returns the game icon path for this metric type, or null if no icon applies.</summary>
    public virtual string? IconPath => null;

    public virtual int DefaultMin => 0;
    public virtual int DefaultMax => 99;
    public virtual RouteObjectiveMode DefaultObjectiveMode => RouteObjectiveMode.None;
    public virtual int DefaultPriority => 0;
    public virtual IReadOnlyList<RouteObjectiveMode> ObjectiveOptions => DefaultObjectiveOptions;

    private string SettingPrefix => $"optimizer_{Type.ToString().ToLowerInvariant()}";
    private string MinSettingKey => $"{SettingPrefix}_min";
    private string MaxSettingKey => $"{SettingPrefix}_max";
    private string ModeSettingKey => $"{SettingPrefix}_mode";
    private string PrioritySettingKey => $"{SettingPrefix}_priority";

    public virtual int Measure(IReadOnlyList<MapPoint> path)
    {
        var total = 0;

        // Skip index 0 so metrics count forward route choices only.
        for (var i = 1; i < path.Count; i++)
        {
            if (CountsPoint(path[i].PointType))
            {
                total++;
            }
        }

        return total;
    }

    public virtual RouteMetricConstraint CreateConstraint(int min, int max)
    {
        return new RangeRouteMetricConstraint(Type, min, max);
    }

    public virtual void RegisterOptimizerSettings(
        List<ModSettingNumberDefinition> numberSettings,
        List<ModSettingChoiceDefinition> choiceSettings)
    {
        var modeOptions = ObjectiveOptions
            .Select(RoutingSettings.ObjectiveModeToSettingValue)
            .ToList();

        numberSettings.Add(new ModSettingNumberDefinition
        {
            Key = MinSettingKey,
            Label = $"{Label} min",
            Description = $"Minimum allowed value for {Label} when solving routes.",
            MinValue = 0,
            MaxValue = 99,
            Step = 1,
            DefaultValue = DefaultMin,
            GetCurrentValue = () => RoutingSettings.GetConstraintDefaults(this).Min,
            OnApply = value =>
            {
                var current = RoutingSettings.GetConstraintDefaults(this);
                RoutingSettings.SetConstraintDefaults(Type, (int)Math.Round(value), current.Max);
            }
        });

        numberSettings.Add(new ModSettingNumberDefinition
        {
            Key = MaxSettingKey,
            Label = $"{Label} max",
            Description = $"Maximum allowed value for {Label} when solving routes.",
            MinValue = 0,
            MaxValue = 99,
            Step = 1,
            DefaultValue = DefaultMax,
            GetCurrentValue = () => RoutingSettings.GetConstraintDefaults(this).Max,
            OnApply = value =>
            {
                var current = RoutingSettings.GetConstraintDefaults(this);
                RoutingSettings.SetConstraintDefaults(Type, current.Min, (int)Math.Round(value));
            }
        });

        choiceSettings.Add(new ModSettingChoiceDefinition
        {
            Key = ModeSettingKey,
            Label = $"{Label} objective mode",
            Description = $"Lexicographic objective mode used by default for {Label}.",
            Options = modeOptions,
            DefaultValue = RoutingSettings.ObjectiveModeToSettingValue(DefaultObjectiveMode),
            GetCurrentValue = () => RoutingSettings.ObjectiveModeToSettingValue(RoutingSettings.GetPriorityDefaults(this).Mode),
            OnApply = value =>
            {
                var current = RoutingSettings.GetPriorityDefaults(this);
                var parsed = RoutingSettings.ParseObjectiveMode(value, current.Mode);
                RoutingSettings.SetPriorityDefaults(Type, parsed, current.Priority);
            }
        });

        numberSettings.Add(new ModSettingNumberDefinition
        {
            Key = PrioritySettingKey,
            Label = $"{Label} priority",
            Description = $"Default lexicographic priority for {Label} (higher runs first).",
            MinValue = 0,
            MaxValue = 999,
            Step = 1,
            DefaultValue = DefaultPriority,
            GetCurrentValue = () => RoutingSettings.GetPriorityDefaults(this).Priority,
            OnApply = value =>
            {
                var current = RoutingSettings.GetPriorityDefaults(this);
                RoutingSettings.SetPriorityDefaults(Type, current.Mode, (int)Math.Round(value));
            }
        });
    }

    protected virtual bool CountsPoint(MapPointType pointType)
    {
        return false;
    }
}
