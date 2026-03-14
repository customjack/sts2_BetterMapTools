using RoutingHelper.Core;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace RoutingHelper;

[ModInitializer(nameof(OnModLoaded))]
public static class ModEntry
{
    public static void OnModLoaded()
    {
        Log.Info("[RoutingHelper] ModEntry.OnModLoaded invoked by STS2.");
        ModBootstrap.Initialize();
    }
}
