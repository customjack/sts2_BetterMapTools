using MegaCrit.Sts2.Core.Map;

namespace RoutingHelper.Features.MapRouting.Metrics;

internal sealed class UnknownRouteMetric : RouteMetricDefinition
{
    public static void Register() => RouteMetricRegistry.Register(new UnknownRouteMetric());

    public override RouteMetricType Type => RouteMetricType.Unknown;
    public override string Label => "unknowns";
    public override int DefaultPriority => 10;

    protected override bool CountsPoint(MapPointType pointType)
    {
        return pointType == MapPointType.Unknown;
    }
}
