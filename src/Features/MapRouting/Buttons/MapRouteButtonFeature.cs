using System;
using HarmonyLib;
using Godot;
using RoutingHelper.Features.MapRouting.Modals;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace RoutingHelper.Features.MapRouting.Buttons;

internal sealed class MapRouteButtonFeature : MapToolButtonFeatureBase
{
    private const string RouteButtonName = "RoutingHelperRouteButton";
    private const float ButtonWidth = 90f;
    private const float ButtonHeight = 28f;
    private const float ButtonGapY = 6f;

    public static MapRouteButtonFeature Instance { get; } = new();

    public void Attach(NMapScreen mapScreen)
    {
        var tools = mapScreen.GetNodeOrNull<Control>("%DrawingTools");
        if (tools == null)
        {
            Log.Warn("[RoutingHelper] Could not find %DrawingTools on NMapScreen.");
            return;
        }

        var drawButton = mapScreen.GetNodeOrNull<Control>("%DrawButton");
        var eraseButton = mapScreen.GetNodeOrNull<Control>("%EraseButton");
        if (drawButton == null || eraseButton == null)
        {
            Log.Warn("[RoutingHelper] Could not find Draw/Erase map buttons for RoutingHelper placement.");
            return;
        }

        if (tools.FindChild(RouteButtonName, recursive: false, owned: false) != null)
        {
            return;
        }

        var routeButton = CreateButton(
            RouteButtonName,
            "Route",
            "Open route planner",
            new Vector2(ButtonWidth, ButtonHeight));

        routeButton.Pressed += () => RoutePopupController.Toggle(mapScreen);

        tools.AddChild(routeButton);
        BindPlacement(tools, mapScreen, () => PlaceButton(routeButton, drawButton, eraseButton));

        Log.Info("[RoutingHelper] Added Route button to map drawing tools.");
    }

    private static void PlaceButton(Control routeButton, Control drawButton, Control eraseButton)
    {
        var topY = Math.Min(drawButton.Position.Y, eraseButton.Position.Y) - ButtonHeight - ButtonGapY;
        var leftX = drawButton.Position.X;
        routeButton.Position = new Vector2(leftX, topY);
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen._Ready))]
internal static class NMapScreenReadyRouteButtonPatch
{
    public static void Postfix(NMapScreen __instance)
    {
        try
        {
            MapRouteButtonFeature.Instance.Attach(__instance);
        }
        catch (Exception ex)
        {
            Log.Error($"[RoutingHelper] Failed to attach Route button. {ex}");
        }
    }
}
