using Godot;
using BetterMapTools.Features.MapDrawing;
using BetterMapTools.Features.Settings;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace BetterMapTools.Features.MapRouting;

internal sealed class RouteOverlayRenderer
{
    private readonly NMapScreen _mapScreen;

    public RouteOverlayRenderer(NMapScreen mapScreen)
    {
        _mapScreen = mapScreen;
    }

    public void Clear()
    {
        var drawings = _mapScreen.Drawings;
        if (drawings == null)
        {
            return;
        }

        ClearDrawingsSafely(drawings);
    }

    public void Render(IReadOnlyList<IReadOnlyList<MapPoint>> routes)
    {
        var drawings = _mapScreen.Drawings;
        if (drawings == null)
        {
            Log.Warn("[BetterMapTools] NMapScreen.Drawings is null; skipping route render.");
            return;
        }

        var lineTint = MapDrawingColorOverrideService.GetLocalEffectiveColor();
        var coordToNode = GetNodeByCoord();
        var uniqueEdges = new HashSet<RouteEdge>();
        var existingLines = GetAllDrawnLines(drawings).ToHashSet();

        using (MapDrawingOperationHistoryService.SuppressLocalStrokeRecording())
        {
            foreach (var route in routes)
            {
                for (var i = 0; i + 1 < route.Count; i++)
                {
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
        }

        var recoloredCount = RecolorNewLines(drawings, lineTint, existingLines);
        MapDrawingOperationHistoryService.RecordLocalOperation(recoloredCount);
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
            line.SetMeta(MapDrawingLineMetadata.RouteOverlayMetaKey, true);
            count++;
        }

        return count;
    }

    private static void ClearDrawingsSafely(NMapDrawings drawings)
    {
        var priorMode = drawings.GetLocalDrawingMode(useOverride: false);
        var wasActivelyDrawing = drawings.IsLocalDrawing();

        if (wasActivelyDrawing)
        {
            drawings.StopLineLocal();
        }

        drawings.ClearDrawnLinesLocal();

        if (priorMode != DrawingMode.None)
        {
            drawings.SetDrawingModeLocal(priorMode);
        }
    }

    private readonly record struct RouteEdge(MapCoord From, MapCoord To);
}
