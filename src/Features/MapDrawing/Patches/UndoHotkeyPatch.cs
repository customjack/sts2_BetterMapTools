using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using BetterMapTools.Features.MapDrawing.Buttons;

namespace BetterMapTools.Features.MapDrawing.Patches;

/// <summary>
/// Intercepts keyboard input on the map screen and triggers undo on Ctrl+Z.
/// </summary>
[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen._Input))]
internal static class NMapScreenUndoHotkeyPatch
{
    public static void Prefix(NMapScreen __instance, InputEvent inputEvent)
    {
        if (UndoInputHelpers.TryHandleUndoHotkey(__instance, inputEvent))
        {
            __instance.AcceptEvent();
        }
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen._GuiInput))]
internal static class NMapScreenGuiUndoHotkeyPatch
{
    public static void Prefix(NMapScreen __instance, InputEvent inputEvent)
    {
        if (UndoInputHelpers.TryHandleUndoHotkey(__instance, inputEvent))
        {
            __instance.AcceptEvent();
        }
    }
}

[HarmonyPatch(typeof(NMouseModeMapDrawingInput), nameof(NMouseModeMapDrawingInput._Input))]
internal static class NMouseModeMapDrawingInputUndoHotkeyPatch
{
    public static bool Prefix(InputEvent inputEvent)
    {
        var mapScreen = NMapScreen.Instance;
        if (mapScreen == null)
        {
            return true;
        }

        if (UndoInputHelpers.TryHandleUndoHotkey(mapScreen, inputEvent))
        {
            mapScreen.GetViewport()?.SetInputAsHandled();
            return false;
        }

        return !UndoInputHelpers.ShouldIgnoreToolbarMousePress(mapScreen, inputEvent);
    }
}

[HarmonyPatch(typeof(NMouseHeldMapDrawingInput), nameof(NMouseHeldMapDrawingInput._Input))]
internal static class NMouseHeldMapDrawingInputUndoHotkeyPatch
{
    public static bool Prefix(InputEvent inputEvent)
    {
        var mapScreen = NMapScreen.Instance;
        if (mapScreen == null)
        {
            return true;
        }

        if (UndoInputHelpers.TryHandleUndoHotkey(mapScreen, inputEvent))
        {
            mapScreen.GetViewport()?.SetInputAsHandled();
            return false;
        }

        return !UndoInputHelpers.ShouldIgnoreToolbarMousePress(mapScreen, inputEvent);
    }
}

internal static class UndoInputHelpers
{
    private static ulong _lastUndoHandledMsec;

    public static bool TryHandleUndoHotkey(NMapScreen mapScreen, InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            return false;
        }

        var isUndoKey = keyEvent.Keycode == Key.Z || keyEvent.PhysicalKeycode == Key.Z;
        if (!isUndoKey ||
            !keyEvent.IsCommandOrControlPressed() ||
            keyEvent.ShiftPressed ||
            keyEvent.AltPressed)
        {
            return false;
        }

        var now = Time.GetTicksMsec();
        if (now - _lastUndoHandledMsec < 100)
        {
            return true;
        }

        _lastUndoHandledMsec = now;
        UndoMapToolRegistration.UndoLastOperation(mapScreen);
        return true;
    }

    public static bool ShouldIgnoreToolbarMousePress(NMapScreen mapScreen, InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            return false;
        }

        var hoveredControl = mapScreen.GetViewport()?.GuiGetHoveredControl();
        if (hoveredControl == null)
        {
            return false;
        }

        var drawingTools = mapScreen.GetNodeOrNull<Control>("%DrawingTools");
        for (Control? current = hoveredControl; current != null; current = current.GetParentOrNull<Control>())
        {
            if (drawingTools != null && current == drawingTools)
            {
                return true;
            }

            var name = current.Name.ToString();
            if (name.StartsWith("BetterMapTools", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
