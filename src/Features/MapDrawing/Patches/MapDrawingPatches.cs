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
        if (isErasing)
        {
            // The eraser material uses subtract blending. DefaultColor controls how much is
            // subtracted per channel — white subtracts fully and erases in one pass regardless
            // of what color the lines were drawn in. The native code sets DefaultColor to the
            // character's map color, which only subtracts partially for non-white colors.
            __result.DefaultColor = Colors.White;
            return;
        }

        if (MapDrawingColorOverrideService.TryGetOverride(player.NetId, out var overrideColor))
        {
            __result.DefaultColor = overrideColor;
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
