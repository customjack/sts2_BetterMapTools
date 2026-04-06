using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace BetterMapTools.Features.MapDrawing;

internal static class MapDrawingReflection
{
    private static readonly FieldInfo? DrawingStatesField = AccessTools.Field(typeof(NMapDrawings), "_drawingStates");
    private static readonly FieldInfo? EraserMaterialField = AccessTools.Field(typeof(NMapDrawings), "_eraserMaterial");
    private static readonly Type? DrawingStateType = AccessTools.Inner(typeof(NMapDrawings), "DrawingState");
    private static readonly FieldInfo? PlayerIdField = DrawingStateType != null
        ? AccessTools.Field(DrawingStateType, "playerId")
        : null;
    private static readonly FieldInfo? DrawViewportField = DrawingStateType != null
        ? AccessTools.Field(DrawingStateType, "drawViewport")
        : null;

    public static IReadOnlyList<Line2D> GetPlayerLines(NMapDrawings drawings, ulong playerId)
    {
        if (!TryGetPlayerViewport(drawings, playerId, out var viewport))
        {
            return Array.Empty<Line2D>();
        }

        return viewport.GetChildren()
            .OfType<Line2D>()
            .ToList();
    }

    public static int RemoveLastLines(NMapDrawings drawings, ulong playerId, int count)
    {
        if (count <= 0 || !TryGetPlayerViewport(drawings, playerId, out var viewport))
        {
            return 0;
        }

        var lines = viewport.GetChildren().OfType<Line2D>().ToList();
        var removed = 0;
        for (var index = lines.Count - 1; index >= 0 && removed < count; index--)
        {
            lines[index].QueueFreeSafely();
            removed++;
        }

        return removed;
    }

    public static bool IsEraserLine(NMapDrawings drawings, Line2D line)
    {
        var eraserMaterial = EraserMaterialField?.GetValue(drawings) as Material;
        if (eraserMaterial == null)
        {
            return false;
        }

        return Equals(line.Material, eraserMaterial);
    }

    private static bool TryGetPlayerViewport(NMapDrawings drawings, ulong playerId, out SubViewport viewport)
    {
        viewport = null!;
        if (DrawingStatesField?.GetValue(drawings) is not System.Collections.IEnumerable states ||
            PlayerIdField == null ||
            DrawViewportField == null)
        {
            return false;
        }

        foreach (var state in states)
        {
            if (state == null)
            {
                continue;
            }

            if (PlayerIdField.GetValue(state) is not ulong statePlayerId || statePlayerId != playerId)
            {
                continue;
            }

            if (DrawViewportField.GetValue(state) is SubViewport drawViewport)
            {
                viewport = drawViewport;
                return true;
            }
        }

        return false;
    }
}
