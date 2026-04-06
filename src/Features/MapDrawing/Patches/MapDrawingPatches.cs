using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace BetterMapTools.Features.MapDrawing.Patches;

[HarmonyPatch(typeof(NMapDrawings), nameof(NMapDrawings.StopLineLocal))]
internal static class NMapDrawingsStopLineLocalHistoryPatch
{
    [HarmonyPrefix]
    public static void Prefix(NMapDrawings __instance, out bool __state)
    {
        __state = __instance.IsLocalDrawing();
    }

    [HarmonyPostfix]
    public static void Postfix(bool __state)
    {
        if (__state)
        {
            MapDrawingOperationHistoryService.RecordLocalOperation(1);
        }
    }
}

[HarmonyPatch(typeof(NMapDrawings), nameof(NMapDrawings.ClearDrawnLinesLocal))]
internal static class NMapDrawingsClearDrawnLinesLocalHistoryPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        MapDrawingOperationHistoryService.ClearLocalHistory();
    }
}

[HarmonyPatch(typeof(NMapDrawings), nameof(NMapDrawings.ClearAllLines))]
internal static class NMapDrawingsClearAllLinesHistoryPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        MapDrawingOperationHistoryService.ClearAllHistory();
    }
}

[HarmonyPatch(typeof(NMapDrawings), "CreateLineForPlayer")]
internal static class NMapDrawingsCreateLineForPlayerColorPatch
{
    [HarmonyPostfix]
    public static void Postfix(Player player, bool isErasing, Line2D __result)
    {
        if (isErasing || __result == null)
        {
            return;
        }

        if (MapDrawingColorOverrideService.TryGetOverride(player.NetId, out var overrideColor))
        {
            __result.DefaultColor = overrideColor;
            // Native erasing appears to leave anti-aliased edge pixels behind on highly
            // saturated custom colors. Use hard edges for overridden colors so the eraser
            // clears the stroke in one pass.
            __result.Antialiased = false;
        }
    }
}

[HarmonyPatch(typeof(NMapDrawings), nameof(NMapDrawings.LoadDrawings))]
internal static class NMapDrawingsLoadDrawingsColorPatch
{
    [HarmonyPostfix]
    public static void Postfix(NMapDrawings __instance)
    {
        MapDrawingColorOverrideService.ReapplyVisibleOverrides(__instance);
    }
}
