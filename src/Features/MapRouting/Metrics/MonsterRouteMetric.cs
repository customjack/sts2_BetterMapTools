using MegaCrit.Sts2.Core.Map;

namespace RoutingHelper.Features.MapRouting.Metrics;

internal sealed class MonsterRouteMetric : RouteMetricDefinition
{
    public static void Register() => RouteMetricRegistry.Register(new MonsterRouteMetric());

    public override RouteMetricType Type => RouteMetricType.Monster;
    public override string Label => "monsters";
    public override int DefaultPriority => 40;

    protected override bool CountsPoint(MapPointType pointType)
    {
        return pointType == MapPointType.Monster;
    }
}
