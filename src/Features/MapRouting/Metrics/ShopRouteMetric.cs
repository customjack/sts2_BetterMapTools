using MegaCrit.Sts2.Core.Map;

namespace RoutingHelper.Features.MapRouting.Metrics;

internal sealed class ShopRouteMetric : RouteMetricDefinition
{
    public static void Register() => RouteMetricRegistry.Register(new ShopRouteMetric());

    public override RouteMetricType Type => RouteMetricType.Shop;
    public override string Label => "shops";
    public override int DefaultPriority => 20;

    protected override bool CountsPoint(MapPointType pointType)
    {
        return pointType == MapPointType.Shop;
    }
}
