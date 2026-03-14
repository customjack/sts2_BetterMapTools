namespace RoutingHelper.Features.MapRouting.Metrics;

internal abstract class RouteMetricConstraint
{
    protected RouteMetricConstraint(RouteMetricType metric)
    {
        Metric = metric;
    }

    public RouteMetricType Metric { get; }

    public abstract bool IsSatisfied(IReadOnlyDictionary<RouteMetricType, int> metricValues);
}

internal sealed class RangeRouteMetricConstraint : RouteMetricConstraint
{
    public RangeRouteMetricConstraint(RouteMetricType metric, int min, int max)
        : base(metric)
    {
        Min = Math.Min(min, max);
        Max = Math.Max(min, max);
    }

    public int Min { get; }
    public int Max { get; }

    public override bool IsSatisfied(IReadOnlyDictionary<RouteMetricType, int> metricValues)
    {
        if (!metricValues.TryGetValue(Metric, out var value))
        {
            return false;
        }

        return value >= Min && value <= Max;
    }
}
