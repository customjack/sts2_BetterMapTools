using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace RoutingHelper.Features.Settings;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class NMainMenuReadyRoutingSettingsHydrationPatch
{
    public static void Postfix()
    {
        try
        {
            RoutingSettingsRegistration.EnsureHydratedAndRefreshIfNeeded();
        }
        catch (Exception ex)
        {
            Log.Error($"[RoutingHelper] Failed late settings hydration on NMainMenu._Ready. {ex}");
        }
    }
}
