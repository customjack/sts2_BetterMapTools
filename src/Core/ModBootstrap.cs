using HarmonyLib;
using RoutingHelper.Features.MapRouting;
using RoutingHelper.Features.MapRouting.Metrics;
using RoutingHelper.Features.Settings;
using MegaCrit.Sts2.Core.Logging;

namespace RoutingHelper.Core;

public static class ModBootstrap
{
    private const string HarmonyId = "routinghelper.harmony";
    private const string BuildMarker = "2026-03-14-structure-routinghelper-a";

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            Log.Info("[RoutingHelper] ModBootstrap.Initialize skipped (already initialized).");
            return;
        }

        _initialized = true;
        Log.Info($"[RoutingHelper] Mod bootstrap starting. build={BuildMarker}");

        RouteMetricRegistry.RegisterDefaults();
        Log.Info($"[RoutingHelper] Registered route metrics: {string.Join(", ", RouteMetricRegistry.MetricOrder.Select(RouteSolver.MetricLabel))}.");

        RoutingSettingsRegistration.Register();
        Log.Info("[RoutingHelper] Registered ModManagerSettings provider.");

        var harmony = new Harmony(HarmonyId);
        harmony.PatchAll();
        Log.Info($"[RoutingHelper] Harmony patches applied with id '{HarmonyId}'.");
    }
}
