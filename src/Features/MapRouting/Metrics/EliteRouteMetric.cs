using MegaCrit.Sts2.Core.Map;

namespace RoutingHelper.Features.MapRouting.Metrics;

internal sealed class EliteRouteMetric : RouteMetricDefinition
{
    public static void Register() => RouteMetricRegistry.Register(new EliteRouteMetric());

    public override RouteMetricType Type => RouteMetricType.Elite;
    public override string Label => "elites";
    public override int DefaultPriority => 50;

    protected override bool CountsPoint(MapPointType pointType)
    {
        return pointType == MapPointType.Elite;
    }
}
