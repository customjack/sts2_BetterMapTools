using System;
using Godot;

namespace RoutingHelper.Features.MapRouting.Buttons;

internal abstract class MapToolButtonFeatureBase
{
    protected static Button CreateButton(string name, string text, string tooltip, Vector2 size)
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

    protected static void BindPlacement(Control host, Control mapScreen, Action place)
    {
        place();
        Callable.From(place).CallDeferred();
        host.Connect(Control.SignalName.Resized, Callable.From(place));
        mapScreen.Connect(Control.SignalName.Resized, Callable.From(place));
    }
}
