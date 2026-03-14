using Godot;
using RoutingHelper.Features.Settings;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace RoutingHelper.Features.MapRouting;

internal sealed class RouteOverlayRenderer
{
    private readonly NMapScreen _mapScreen;

    public RouteOverlayRenderer(NMapScreen mapScreen)
    {
        _mapScreen = mapScreen;
    }

    public void Clear()
    {
        _mapScreen.Drawings?.ClearDrawnLinesLocal();

        foreach (var point in GetAllMapPoints())
        {
            point.Modulate = Colors.White;
        }
    }

    public void Render(IReadOnlyList<IReadOnlyList<MapPoint>> routes)
    {
        Clear();

        var drawings = _mapScreen.Drawings;
        if (drawings == null)
        {
            Log.Warn("[RoutingHelper] NMapScreen.Drawings is null; skipping route render.");
            return;
        }

        var nodeTint = RoutingSettings.ResolveHighlightColor();
        var coordToNode = GetNodeByCoord();
        var highlighted = new HashSet<MapCoord>();
        var uniqueEdges = new HashSet<RouteEdge>();
        var existingLines = GetAllDrawnLines(drawings).ToHashSet();

        foreach (var route in routes)
        {
            for (var i = 0; i < route.Count; i++)
            {
                highlighted.Add(route[i].coord);

                if (i + 1 >= route.Count)
                {
                    continue;
                }

                if (!coordToNode.TryGetValue(route[i].coord, out var fromNode) ||
                    !coordToNode.TryGetValue(route[i + 1].coord, out var toNode))
                {
                    continue;
                }

                var edge = new RouteEdge(route[i].coord, route[i + 1].coord);
                if (!uniqueEdges.Add(edge))
                {
                    continue;
                }

                var fromPos = ToDrawingSpace(drawings, fromNode);
                var toPos = ToDrawingSpace(drawings, toNode);

                drawings.BeginLineLocal(fromPos, DrawingMode.Drawing);
                drawings.UpdateCurrentLinePositionLocal(toPos);
                drawings.StopLineLocal();
            }
        }

        foreach (var coord in highlighted)
        {
            if (coordToNode.TryGetValue(coord, out var node))
            {
                node.Modulate = nodeTint;
            }
        }

        var recoloredCount = RecolorNewLines(drawings, nodeTint, existingLines);
        Log.Info($"[RoutingHelper] Rendered route overlay for {routes.Count} best route(s), unique_edges={uniqueEdges.Count}, recolored_lines={recoloredCount}.");
    }

    private Dictionary<MapCoord, NMapPoint> GetNodeByCoord()
    {
        var result = new Dictionary<MapCoord, NMapPoint>();
        foreach (var point in GetAllMapPoints())
        {
            result[point.Point.coord] = point;
        }

        return result;
    }

    private IEnumerable<NMapPoint> GetAllMapPoints()
    {
        var pointsLayer = _mapScreen.GetNodeOrNull<Node>("TheMap/Points");
        if (pointsLayer == null)
        {
            yield break;
        }

        foreach (var child in pointsLayer.GetChildren())
        {
            if (child is NMapPoint point)
            {
                yield return point;
            }
        }
    }

    private static Vector2 GetNodeCenter(NMapPoint point)
    {
        // Matches map path rendering semantics for normal vs boss/ancient map points.
        return point is NNormalMapPoint ? point.Position : point.Position + point.Size * 0.5f;
    }

    private static Vector2 ToDrawingSpace(NMapDrawings drawings, NMapPoint point)
    {
        var localMapEndpoint = GetNodeCenter(point);
        var pointsLayer = point.GetParent<Control>();
        if (pointsLayer == null)
        {
            return point.Position;
        }

        // Mirror the native mouse conversion path:
        // drawing_pos = drawings.GetGlobalTransform().Inverse() * global_map_pos
        var globalCenter = pointsLayer.GetGlobalTransform() * localMapEndpoint;
        return drawings.GetGlobalTransform().Inverse() * globalCenter;
    }

    private static IEnumerable<Line2D> GetAllDrawnLines(NMapDrawings drawings)
    {
        foreach (var node in GetDescendants(drawings))
        {
            if (node is Line2D line)
            {
                yield return line;
            }
        }
    }

    private static IEnumerable<Node> GetDescendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Node node)
            {
                yield return node;
                foreach (var descendant in GetDescendants(node))
                {
                    yield return descendant;
                }
            }
        }
    }

    private static int RecolorNewLines(NMapDrawings drawings, Color tint, ISet<Line2D> existingLines)
    {
        var count = 0;
        foreach (var line in GetAllDrawnLines(drawings))
        {
            if (existingLines.Contains(line))
            {
                continue;
            }

            line.DefaultColor = tint;
            count++;
        }

        return count;
    }

    private readonly record struct RouteEdge(MapCoord From, MapCoord To);
}
