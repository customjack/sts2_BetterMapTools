using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using BetterMapTools.Features.MapDrawing.Multiplayer;

namespace BetterMapTools.Features.MapDrawing;

internal static class MapDrawingColorOverrideService
{
    private static readonly Dictionary<ulong, Color> OverridesByPlayer = [];

    public static bool TryGetOverride(ulong playerId, out Color color)
    {
        return OverridesByPlayer.TryGetValue(playerId, out color);
    }

    public static bool TryGetLocalOverride(out Color color)
    {
        color = Colors.White;
        return TryGetLocalPlayer(out var player) && TryGetOverride(player.NetId, out color);
    }

    public static Color GetLocalEffectiveColor()
    {
        if (TryGetLocalOverride(out var overrideColor))
        {
            return overrideColor;
        }

        return GetLocalDefaultColor();
    }

    public static Color GetLocalDefaultColor()
    {
        return TryGetLocalPlayer(out var player)
            ? player.Character.MapDrawingColor
            : Colors.White;
    }

    public static void ApplyLocalOverride(Color? colorOverride)
    {
        if (!TryGetLocalPlayer(out var player))
        {
            return;
        }

        // Don't re-color existing drawings — only future lines will use the new color.
        ApplyOverride(player.NetId, colorOverride, recolorExisting: false);
    }

    public static void ApplyRemoteOverride(ulong playerId, Color? colorOverride)
    {
        // Changing color should affect future strokes only. Existing lines keep their authored color.
        ApplyOverride(playerId, colorOverride, recolorExisting: false);
    }

    public static IReadOnlyList<MapDrawingColorWireValue> BuildSnapshot()
    {
        return OverridesByPlayer
            .Select(pair => new MapDrawingColorWireValue
            {
                PlayerId = pair.Key,
                HasOverride = true,
                ColorRaw = ToColorRaw(pair.Value)
            })
            .ToList();
    }

    public static void ClearRemoteOverridesExceptLocal()
    {
        var localPlayerId = TryGetLocalPlayer(out var player) ? player.NetId : ulong.MaxValue;
        var drawings = NMapScreen.Instance?.Drawings;
        foreach (var playerId in OverridesByPlayer.Keys.ToList())
        {
            if (playerId != localPlayerId)
            {
                if (drawings != null)
                {
                    RestoreDefaultColorToLines(drawings, playerId);
                }

                OverridesByPlayer.Remove(playerId);
            }
        }
    }

    public static void ReapplyVisibleOverrides(NMapDrawings drawings)
    {
        // Loaded drawings already carry their saved line colors. Re-applying the current
        // override here would incorrectly repaint older strokes after a color change.
    }

    public static string ToColorRaw(Color color)
    {
        var r = Mathf.Clamp((int)Math.Round(color.R * 255f), 0, 255);
        var g = Mathf.Clamp((int)Math.Round(color.G * 255f), 0, 255);
        var b = Mathf.Clamp((int)Math.Round(color.B * 255f), 0, 255);
        var a = Mathf.Clamp((int)Math.Round(color.A * 255f), 0, 255);
        return $"#{r:X2}{g:X2}{b:X2}{a:X2}";
    }

    public static Color NormalizeDrawingColor(Color color)
    {
        color.A = 1f;
        return color;
    }

    private static void ApplyOverride(ulong playerId, Color? colorOverride, bool recolorExisting)
    {
        if (colorOverride.HasValue)
        {
            OverridesByPlayer[playerId] = NormalizeDrawingColor(colorOverride.Value);
        }
        else
        {
            OverridesByPlayer.Remove(playerId);
        }

        if (!recolorExisting)
        {
            return;
        }

        var drawings = NMapScreen.Instance?.Drawings;
        if (drawings == null)
        {
            return;
        }

        if (colorOverride.HasValue)
        {
            ApplyOverrideToLines(drawings, playerId, colorOverride.Value);
        }
        else
        {
            RestoreDefaultColorToLines(drawings, playerId);
        }
    }

    private static void ApplyOverrideToLines(NMapDrawings drawings, ulong playerId, Color color)
    {
        foreach (var line in MapDrawingReflection.GetPlayerLines(drawings, playerId))
        {
            if (MapDrawingReflection.IsEraserLine(drawings, line) || line.HasMeta(MapDrawingLineMetadata.RouteOverlayMetaKey))
            {
                continue;
            }

            line.DefaultColor = color;
        }
    }

    private static void RestoreDefaultColorToLines(NMapDrawings drawings, ulong playerId)
    {
        var defaultColor = ResolveDefaultColor(playerId);
        foreach (var line in MapDrawingReflection.GetPlayerLines(drawings, playerId))
        {
            if (MapDrawingReflection.IsEraserLine(drawings, line) || line.HasMeta(MapDrawingLineMetadata.RouteOverlayMetaKey))
            {
                continue;
            }

            line.DefaultColor = defaultColor;
        }
    }

    private static Color ResolveDefaultColor(ulong playerId)
    {
        var runState = RunManager.Instance.DebugOnlyGetState();
        return runState?.GetPlayer(playerId)?.Character.MapDrawingColor ?? Colors.White;
    }

    private static bool TryGetLocalPlayer(out Player player)
    {
        player = null!;
        var runState = RunManager.Instance.DebugOnlyGetState();
        if (runState == null)
        {
            return false;
        }

        var localPlayer = LocalContext.GetMe(runState);
        if (localPlayer == null)
        {
            return false;
        }

        player = localPlayer;
        return true;
    }
}
