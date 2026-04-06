using BetterMapTools.Api.MapTools;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using BetterMapTools.Features.MapRouting.Modals;

namespace BetterMapTools.Features.MapRouting.Buttons;

internal static class RouteSolverMapToolRegistration
{
    private const string RouteIconResourceName = "BetterMapTools.RouteIconPng";
    private const string RouteIconGlowResourceName = "BetterMapTools.RouteIconGlowPng";

    private static bool _registered;
    private static Texture2D? _routeIconTexture;
    private static Texture2D? _routeIconGlowTexture;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        MapToolButtonRegistry.Register(new MapToolButtonDefinition
        {
            Id = "route_solver",
            Order = 30,
            TooltipTitle = "Route Solver",
            TooltipDescription = "Open the BetterMapTools route solver and compare optimized routes.",
            IconFactory = LoadRouteIcon,
            HoverIconFactory = LoadRouteGlowIcon,
            OnPressed = RoutePopupController.Toggle
        });
    }

    private static Texture2D? LoadRouteIcon()
    {
        _routeIconTexture ??= MapToolButtonFeatureBase.LoadEmbeddedPngTexture(RouteIconResourceName);
        if (_routeIconTexture == null)
        {
            Log.Warn($"[BetterMapTools] Could not load route icon resource '{RouteIconResourceName}'.");
        }

        return _routeIconTexture;
    }

    private static Texture2D? LoadRouteGlowIcon()
    {
        _routeIconGlowTexture ??= MapToolButtonFeatureBase.LoadEmbeddedPngTexture(RouteIconGlowResourceName);
        if (_routeIconGlowTexture == null)
        {
            Log.Warn($"[BetterMapTools] Could not load route glow icon resource '{RouteIconGlowResourceName}'.");
        }

        return _routeIconGlowTexture;
    }
}
