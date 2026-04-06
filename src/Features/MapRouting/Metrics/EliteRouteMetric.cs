using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;

namespace BetterMapTools.Features.MapRouting.Metrics;

internal sealed class EliteRouteMetric : RouteMetricDefinition
{
    public static void Register() => RouteMetricRegistry.Register(new EliteRouteMetric());

    public override RouteMetricType Type => RouteMetricType.Elite;
    public override string Label => "elites";
    public override string? IconPath => ImageHelper.GetRoomIconPath(MapPointType.Elite, RoomType.Elite, null);
    public override int DefaultPriority => 50;

    protected override bool CountsPoint(MapPointType pointType)
    {
        return pointType == MapPointType.Elite;
    }
}
