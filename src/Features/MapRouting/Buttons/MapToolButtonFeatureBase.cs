using System;
using System.IO;
using System.Reflection;
using Godot;

namespace BetterMapTools.Features.MapRouting.Buttons;

internal abstract class MapToolButtonFeatureBase
{
    internal static Button CreateButton(string name, string text, string tooltip, Vector2 size)
    {
        return new Button
        {
            Name = name,
            Text = text,
            TooltipText = tooltip,
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = size,
            Size = size
        };
    }

    internal static Button CreateIconButton(string name, Texture2D? icon, string tooltip, Vector2 size)
    {
        var button = new Button
        {
            Name = name,
            Text = string.Empty,
            TooltipText = string.Empty, // Suppress Godot native tooltip; we use NHoverTipSet instead
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = size,
            Size = size,
            ExpandIcon = true
        };

        var emptyStyle = new StyleBoxEmpty();
        button.Flat = true;
        button.AddThemeStyleboxOverride("normal", emptyStyle);
        button.AddThemeStyleboxOverride("hover", emptyStyle);
        button.AddThemeStyleboxOverride("pressed", emptyStyle);
        button.AddThemeStyleboxOverride("focus", emptyStyle);
        button.AddThemeStyleboxOverride("disabled", emptyStyle);
        button.AddThemeColorOverride("icon_normal_color", Colors.White);
        button.AddThemeColorOverride("icon_hover_color", Colors.White);
        button.AddThemeColorOverride("icon_pressed_color", Colors.White);
        button.AddThemeColorOverride("icon_focus_color", Colors.White);
        button.AddThemeColorOverride("icon_disabled_color", new Color(1f, 1f, 1f, 0.6f));

        if (icon != null)
        {
            button.Icon = icon;
            button.IconAlignment = HorizontalAlignment.Center;
        }

        return button;
    }

    internal static Texture2D? LoadEmbeddedPngTexture(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return null;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        var image = new Image();
        var error = image.LoadPngFromBuffer(memory.ToArray());
        if (error != Error.Ok)
        {
            return null;
        }

        return ImageTexture.CreateFromImage(image);
    }

    internal static void BindPlacement(Control host, Control mapScreen, Action place)
    {
        place();
        Callable.From(place).CallDeferred();
        host.Connect(Control.SignalName.Resized, Callable.From(place));
        mapScreen.Connect(Control.SignalName.Resized, Callable.From(place));
    }
}
