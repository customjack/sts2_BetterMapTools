using System.Text;
using BetterMapTools.Api.MapTools;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using BetterMapTools.Features.MapDrawing;
using BetterMapTools.Features.MapDrawing.Multiplayer;
using BetterMapTools.Features.MapRouting.Buttons;
using BetterMapTools.Features.Settings;

namespace BetterMapTools.Features.MapTools;

internal static class MapToolButtonHostFeature
{
    private const string ButtonPrefix = "BetterMapToolsMapTool_";
    private const float FallbackButtonSize = 32f;
    private const float FallbackButtonGap = 8f;
    private const float HoverTipOffsetX = 10f;
    private const float HoverTipOffsetY = -132f;

    private static readonly HashSet<string> LocalizedTooltipIds = new(StringComparer.OrdinalIgnoreCase);

    public static void Attach(NMapScreen mapScreen)
    {
        var definitions = MapToolButtonRegistry.GetAllOrdered();
        if (definitions.Count == 0)
        {
            return;
        }

        var tools = mapScreen.GetNodeOrNull<Control>("%DrawingTools");
        var drawButton = mapScreen.GetNodeOrNull<Control>("%DrawButton");
        var eraseButton = mapScreen.GetNodeOrNull<Control>("%EraseButton");
        var clearButton = mapScreen.GetNodeOrNull<Control>("%ClearButton");
        if (tools == null || drawButton == null || eraseButton == null || clearButton == null)
        {
            Log.Warn("[BetterMapTools] Could not resolve map drawing toolbar nodes.");
            return;
        }

        var buttonHost = clearButton.GetParent<Control>() ?? tools;
        var createdButtons = new List<Control>(definitions.Count);
        foreach (var definition in definitions)
        {
            var buttonName = ButtonPrefix + SanitizeId(definition.Id);
            var existing = buttonHost.FindChild(buttonName, recursive: false, owned: false) as Button;
            var button = existing ?? CreateButton(definition, buttonName, ResolveButtonSize(clearButton), buttonHost, mapScreen);
            if (existing == null)
            {
                buttonHost.AddChild(button);
            }

            createdButtons.Add(button);
        }

        void PlaceButtons()
        {
            if (!GodotObject.IsInstanceValid(buttonHost) || !GodotObject.IsInstanceValid(tools))
            {
                return;
            }

            if (buttonHost is BoxContainer boxContainer)
            {
                for (var i = 0; i < createdButtons.Count; i++)
                {
                    var targetIndex = Math.Min(clearButton.GetIndex() + i + 1, boxContainer.GetChildCount() - 1);
                    boxContainer.MoveChild(createdButtons[i], targetIndex);
                    var size = ResolveControlSize(clearButton, createdButtons[i].CustomMinimumSize);
                    createdButtons[i].CustomMinimumSize = size;
                    createdButtons[i].Size = size;
                    createdButtons[i].PivotOffset = size * 0.5f;
                }
            }
            else
            {
                var size = ResolveControlSize(clearButton, new Vector2(FallbackButtonSize, FallbackButtonSize));
                var eraseSize = ResolveControlSize(eraseButton, size);
                var eraseRight = eraseButton.Position.X + eraseSize.X;
                var computedGap = clearButton.Position.X - eraseRight;
                var gap = computedGap > 2f ? computedGap : FallbackButtonGap;
                var x = clearButton.Position.X + size.X + gap;
                for (var i = 0; i < createdButtons.Count; i++)
                {
                    var button = createdButtons[i];
                    var position = new Vector2(x + i * (size.X + gap), clearButton.Position.Y);
                    button.Position = position;
                    button.Size = size;
                    button.CustomMinimumSize = size;
                    button.PivotOffset = size * 0.5f;
                }
            }

            EnsureToolbarLayout(tools, buttonHost);
        }

        MapToolButtonFeatureBase.BindPlacement(buttonHost, mapScreen, PlaceButtons);
        EnsureToolbarLayout(tools, buttonHost);
        Callable.From(PlaceButtons).CallDeferred();

    }

    private static Button CreateButton(
        MapToolButtonDefinition definition,
        string buttonName,
        Vector2 size,
        Control buttonHost,
        NMapScreen mapScreen)
    {
        var button = MapToolButtonFeatureBase.CreateIconButton(buttonName, definition.IconFactory(), definition.TooltipDescription, size);
        button.Scale = Vector2.One * definition.NormalScale;
        button.SelfModulate = definition.InactiveColor;

        void SetHovered(bool hovered)
        {
            var icon = hovered
                ? (definition.HoverIconFactory?.Invoke() ?? definition.IconFactory())
                : definition.IconFactory();
            button.Icon = icon;
            button.Scale = hovered ? Vector2.One * definition.HoverScale : Vector2.One * definition.NormalScale;
            button.SelfModulate = hovered ? definition.ActiveColor : definition.InactiveColor;
        }

        button.MouseEntered += () =>
        {
            SetHovered(true);
            ShowHoverTip(buttonHost, definition);
        };
        button.MouseExited += () =>
        {
            SetHovered(false);
            HideHoverTip(buttonHost);
        };
        button.FocusEntered += () =>
        {
            SetHovered(true);
            ShowHoverTip(buttonHost, definition);
        };
        button.FocusExited += () =>
        {
            SetHovered(false);
            HideHoverTip(buttonHost);
        };
        button.Pressed += () =>
        {
            HideHoverTip(buttonHost);
            definition.OnPressed(mapScreen);
        };

        return button;
    }

    private static void ShowHoverTip(Control buttonHost, MapToolButtonDefinition definition)
    {
        EnsureTooltipLocalization(definition);

        var isUsingController = NControllerManager.Instance?.IsUsingController ?? false;
        var titleKey = isUsingController
            ? BuildTooltipKey(definition.Id, "title_controller")
            : BuildTooltipKey(definition.Id, "title_mkb");
        var descriptionKey = BuildTooltipKey(definition.Id, "description");

        NHoverTipSet.Remove(buttonHost);
        var hoverTipSet = NHoverTipSet.CreateAndShow(
            buttonHost,
            new HoverTip(new LocString("map", titleKey), new LocString("map", descriptionKey)));
        hoverTipSet.GlobalPosition = buttonHost.GlobalPosition + new Vector2(HoverTipOffsetX, HoverTipOffsetY);
    }

    private static void HideHoverTip(Control buttonHost)
    {
        NHoverTipSet.Remove(buttonHost);
    }

    private static void EnsureTooltipLocalization(MapToolButtonDefinition definition)
    {
        if (!LocalizedTooltipIds.Add(definition.Id))
        {
            return;
        }

        LocManager.Instance.GetTable("map").MergeWith(new Dictionary<string, string>
        {
            [BuildTooltipKey(definition.Id, "title_mkb")] = definition.TooltipTitle,
            [BuildTooltipKey(definition.Id, "title_controller")] = definition.TooltipTitleController ?? definition.TooltipTitle,
            [BuildTooltipKey(definition.Id, "description")] = definition.TooltipDescription
        });
    }

    private static string BuildTooltipKey(string id, string suffix)
    {
        return $"BETTER_MAP_TOOLS_{SanitizeId(id).ToUpperInvariant()}.{suffix}";
    }

    private static string SanitizeId(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return builder.ToString();
    }

    private static void EnsureToolbarLayout(Control tools, Control buttonHost)
    {
        if (!GodotObject.IsInstanceValid(tools) || !GodotObject.IsInstanceValid(buttonHost))
        {
            return;
        }

        var buttonControls = buttonHost
            .GetChildren()
            .OfType<Control>()
            .Where(child => child.Visible)
            .ToList();
        if (buttonControls.Count == 0)
        {
            return;
        }

        var left = buttonControls.Min(GetLeftEdge);
        var right = buttonControls.Max(GetRightEdge);
        var width = MathF.Max(0f, right - left);
        if (buttonHost is BoxContainer boxContainer)
        {
            var separation = boxContainer.GetThemeConstant("separation");
            var summedWidths = buttonControls.Sum(control => ResolveControlSize(control, control.CustomMinimumSize).X) +
                               separation * Math.Max(0, buttonControls.Count - 1);
            width = MathF.Max(width, summedWidths);
        }

        if (width <= 0f)
        {
            return;
        }

        ExpandControlWidth(buttonHost, width);

        var hostRightInTools = buttonHost.Position.X + MathF.Max(buttonHost.Size.X, buttonHost.CustomMinimumSize.X);
        ExpandControlWidth(tools, hostRightInTools + FallbackButtonGap);

        foreach (var sibling in tools.GetChildren().OfType<Control>())
        {
            if (sibling == buttonHost)
            {
                continue;
            }

            if (sibling is not TextureRect &&
                sibling is not NinePatchRect &&
                sibling is not Panel &&
                sibling is not PanelContainer &&
                sibling is not ColorRect)
            {
                continue;
            }

            var startsNearHost = MathF.Abs(sibling.Position.X - buttonHost.Position.X) <= 24f;
            var hostHeight = MathF.Max(buttonHost.Size.Y, buttonHost.CustomMinimumSize.Y);
            var overlapsVertically =
                sibling.Position.Y <= buttonHost.Position.Y + hostHeight + 8f &&
                sibling.Position.Y + sibling.Size.Y >= buttonHost.Position.Y - 8f;
            if (!startsNearHost || !overlapsVertically)
            {
                continue;
            }

            var desiredWidth = hostRightInTools - sibling.Position.X + FallbackButtonGap;
            ExpandControlWidth(sibling, desiredWidth);
        }
    }

    private static Vector2 ResolveButtonSize(Control referenceButton)
    {
        var resolved = ResolveControlSize(referenceButton, new Vector2(FallbackButtonSize, FallbackButtonSize));
        if (resolved.X <= 0f || resolved.Y <= 0f)
        {
            return new Vector2(FallbackButtonSize, FallbackButtonSize);
        }

        return resolved;
    }

    private static Vector2 ResolveControlSize(Control control, Vector2 fallback)
    {
        if (control.Size.X > 0f && control.Size.Y > 0f)
        {
            return control.Size;
        }

        if (control.CustomMinimumSize.X > 0f && control.CustomMinimumSize.Y > 0f)
        {
            return control.CustomMinimumSize;
        }

        return fallback;
    }

    private static float GetLeftEdge(Control control)
    {
        return control.Position.X;
    }

    private static float GetRightEdge(Control control)
    {
        var size = ResolveControlSize(control, control.Size);
        return control.Position.X + size.X;
    }

    private static void ExpandControlWidth(Control control, float desiredWidth)
    {
        if (desiredWidth <= 0f)
        {
            return;
        }

        var min = control.CustomMinimumSize;
        if (desiredWidth > min.X)
        {
            min.X = desiredWidth;
            control.CustomMinimumSize = min;
        }

        if (control is not Container && desiredWidth > control.Size.X)
        {
            control.Size = new Vector2(desiredWidth, control.Size.Y);
        }
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen._Ready))]
internal static class NMapScreenReadyBetterMapToolsPatch
{
    public static void Postfix(NMapScreen __instance)
    {
        try
        {
            MapToolButtonHostFeature.Attach(__instance);
        }
        catch (Exception ex)
        {
            Log.Error($"[BetterMapTools] Failed to attach map tool buttons. {ex}");
        }

        try
        {
            ApplySavedDrawingColor();
        }
        catch (Exception ex)
        {
            Log.Error($"[BetterMapTools] Failed to apply saved drawing color on map load. {ex}");
        }
    }

    private static void ApplySavedDrawingColor()
    {
        var saved = RoutingSettings.SavedDrawingColorRaw;
        if (string.IsNullOrWhiteSpace(saved))
        {
            return;
        }

        if (!RoutingSettings.TryResolveColor(saved, out var color))
        {
            return;
        }

        MapDrawingColorOverrideService.ApplyLocalOverride(color);
        MapDrawingSyncService.BroadcastLocalColor("Map load saved drawing color");
    }
}
