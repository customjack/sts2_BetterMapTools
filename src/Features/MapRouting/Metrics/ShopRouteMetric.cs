using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;

namespace BetterMapTools.Features.MapRouting.Metrics;

internal sealed class ShopRouteMetric : RouteMetricDefinition
{
    public static void Register() => RouteMetricRegistry.Register(new ShopRouteMetric());

    public override RouteMetricType Type => RouteMetricType.Shop;
    public override string Label => "shops";
    public override string? IconPath => ImageHelper.GetRoomIconPath(MapPointType.Shop, RoomType.Shop, null);
    public override int DefaultPriority => 20;

    protected override bool CountsPoint(MapPointType pointType)
    {
        return pointType == MapPointType.Shop;
    }
}
