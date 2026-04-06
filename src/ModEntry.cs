using BetterMapTools.Core;
using MegaCrit.Sts2.Core.Modding;

namespace BetterMapTools;

[ModInitializer(nameof(OnModLoaded))]
public static class ModEntry
{
    public static void OnModLoaded()
    {
        ModBootstrap.Initialize();
    }
}
