using BetterMapTools.Api.MapTools;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using BetterMapTools.Features.MapDrawing.Modals;
using BetterMapTools.Features.MapRouting.Buttons;

namespace BetterMapTools.Features.MapDrawing.Buttons;

internal static class ColorPickerMapToolRegistration
{
    private const string ColorIconResourceName = "BetterMapTools.ColorIconPng";
    private const string ColorIconGlowResourceName = "BetterMapTools.ColorIconGlowPng";

    private static bool _registered;
    private static Texture2D? _colorIconTexture;
    private static Texture2D? _colorIconGlowTexture;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        MapToolButtonRegistry.Register(new MapToolButtonDefinition
        {
            Id = "drawing_color",
            Order = 20,
            TooltipTitle = "Drawing Color",
            TooltipDescription = "Choose the color used by your map drawing tool and sync it in multiplayer.",
            IconFactory = LoadColorIcon,
            HoverIconFactory = LoadColorGlowIcon,
            OnPressed = DrawingColorPopupController.Toggle
        });
    }

    private static Texture2D? LoadColorIcon()
    {
        _colorIconTexture ??= MapToolButtonFeatureBase.LoadEmbeddedPngTexture(ColorIconResourceName);
        if (_colorIconTexture == null)
        {
            Log.Warn($"[BetterMapTools] Could not load drawing color icon resource '{ColorIconResourceName}'.");
        }

        return _colorIconTexture;
    }

    private static Texture2D? LoadColorGlowIcon()
    {
        _colorIconGlowTexture ??= MapToolButtonFeatureBase.LoadEmbeddedPngTexture(ColorIconGlowResourceName);
        if (_colorIconGlowTexture == null)
        {
            Log.Warn($"[BetterMapTools] Could not load drawing color glow icon resource '{ColorIconGlowResourceName}'.");
        }

        return _colorIconGlowTexture;
    }
}
