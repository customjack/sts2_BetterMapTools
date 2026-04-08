using HarmonyLib;
using BetterMapTools.Features.MapDrawing.Buttons;
using BetterMapTools.Features.MapRouting.Metrics;
using BetterMapTools.Features.MapRouting;
using BetterMapTools.Features.MapRouting.Buttons;
using BetterMapTools.Features.Settings;
using MegaCrit.Sts2.Core.Logging;

namespace BetterMapTools.Core;

public static class ModBootstrap
{
    private const string HarmonyId = "bettermaptools.harmony";
    private const string BuildMarker = "2026-04-08-release-a";

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        Log.Info($"[BetterMapTools] Mod loaded. build={BuildMarker}");

        RouteMetricRegistry.RegisterDefaults();

        UndoMapToolRegistration.Register();
        ColorPickerMapToolRegistration.Register();
        RouteSolverMapToolRegistration.Register();
        RoutingSettingsRegistration.Register();

        var harmony = new Harmony(HarmonyId);
        harmony.PatchAll();
    }
}
