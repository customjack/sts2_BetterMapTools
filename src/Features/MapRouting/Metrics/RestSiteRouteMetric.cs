using MegaCrit.Sts2.Core.Map;

namespace RoutingHelper.Features.MapRouting.Metrics;

internal sealed class RestSiteRouteMetric : RouteMetricDefinition
{
    public static void Register() => RouteMetricRegistry.Register(new RestSiteRouteMetric());

    public override RouteMetricType Type => RouteMetricType.RestSite;
    public override string Label => "rests";
    public override int DefaultPriority => 30;

    protected override bool CountsPoint(MapPointType pointType)
    {
        return pointType == MapPointType.RestSite;
    }
}
