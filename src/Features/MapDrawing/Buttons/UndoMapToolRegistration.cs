using BetterMapTools.Api.MapTools;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using BetterMapTools.Features.MapDrawing.Multiplayer;
using BetterMapTools.Features.MapRouting.Buttons;

namespace BetterMapTools.Features.MapDrawing.Buttons;

internal static class UndoMapToolRegistration
{
    private const string UndoIconResourceName = "BetterMapTools.UndoIconPng";
    private const string UndoIconGlowResourceName = "BetterMapTools.UndoIconGlowPng";

    private static readonly System.Reflection.FieldInfo? DrawingInputField =
        AccessTools.Field(typeof(NMapScreen), "_drawingInput");

    private static bool _registered;
    private static Texture2D? _undoIconTexture;
    private static Texture2D? _undoIconGlowTexture;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        MapToolButtonRegistry.Register(new MapToolButtonDefinition
        {
            Id = "undo_drawing",
            Order = 10,
            TooltipTitle = "Undo Drawing (Ctrl+Z)",
            TooltipDescription = "Remove the most recent drawing action.",
            IconFactory = LoadUndoIcon,
            HoverIconFactory = LoadUndoGlowIcon,
            OnPressed = mapScreen => UndoLastOperation(mapScreen)
        });
    }

    public static void UndoLastOperation(NMapScreen mapScreen)
    {
        var drawings = mapScreen.Drawings;
        if (drawings == null)
        {
            return;
        }

        // If drawing mode is active, stop it cleanly via NMapDrawingInput.StopDrawing()
        // so NMapScreen resets its _drawingInput field and updates button states.
        StopActiveDrawingInput(mapScreen, drawings);

        if (!MapDrawingOperationHistoryService.TryPopLocalOperation(out var lineCount) || lineCount <= 0)
        {
            return;
        }

        var localPlayerId = RunManager.Instance.NetService.NetId;
        var removed = MapDrawingReflection.RemoveLastLines(drawings, localPlayerId, lineCount);
        if (removed <= 0)
        {
            return;
        }

        MapDrawingSyncService.BroadcastUndo(removed, "Undo map tool");
    }

    private static void StopActiveDrawingInput(NMapScreen mapScreen, NMapDrawings drawings)
    {
        // Prefer stopping via NMapDrawingInput.StopDrawing() so NMapScreen cleans up properly.
        if (DrawingInputField?.GetValue(mapScreen) is NMapDrawingInput drawingInput &&
            GodotObject.IsInstanceValid(drawingInput))
        {
            drawingInput.StopDrawing();
        }
        else
        {
            if (!drawings.IsLocalDrawing())
            {
                return;
            }

            // Fallback: stop just the in-progress line without changing drawing mode.
            drawings.StopLineLocal();
        }
    }

    private static Texture2D? LoadUndoIcon()
    {
        _undoIconTexture ??= MapToolButtonFeatureBase.LoadEmbeddedPngTexture(UndoIconResourceName);
        if (_undoIconTexture == null)
        {
            Log.Warn($"[BetterMapTools] Could not load undo icon resource '{UndoIconResourceName}'.");
        }

        return _undoIconTexture;
    }

    private static Texture2D? LoadUndoGlowIcon()
    {
        _undoIconGlowTexture ??= MapToolButtonFeatureBase.LoadEmbeddedPngTexture(UndoIconGlowResourceName);
        if (_undoIconGlowTexture == null)
        {
            Log.Warn($"[BetterMapTools] Could not load undo glow icon resource '{UndoIconGlowResourceName}'.");
        }

        return _undoIconGlowTexture;
    }
}
