using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;

namespace BetterMapTools.Features.MapRouting.Metrics;

internal sealed class RestSiteRouteMetric : RouteMetricDefinition
{
    public static void Register() => RouteMetricRegistry.Register(new RestSiteRouteMetric());

    public override RouteMetricType Type => RouteMetricType.RestSite;
    public override string Label => "rests";
    public override string? IconPath => ImageHelper.GetRoomIconPath(MapPointType.RestSite, RoomType.RestSite, null);
    public override int DefaultPriority => 30;

    protected override bool CountsPoint(MapPointType pointType)
    {
        return pointType == MapPointType.RestSite;
    }
}
