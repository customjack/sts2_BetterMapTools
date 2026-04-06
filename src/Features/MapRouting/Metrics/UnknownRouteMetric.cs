using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;

namespace BetterMapTools.Features.MapRouting.Metrics;

internal sealed class UnknownRouteMetric : RouteMetricDefinition
{
    public static void Register() => RouteMetricRegistry.Register(new UnknownRouteMetric());

    public override RouteMetricType Type => RouteMetricType.Unknown;
    public override string Label => "unknowns";
    public override string? IconPath => ImageHelper.GetRoomIconPath(MapPointType.Unknown, RoomType.Event, null);
    public override int DefaultPriority => 10;

    protected override bool CountsPoint(MapPointType pointType)
    {
        return pointType == MapPointType.Unknown;
    }
}
