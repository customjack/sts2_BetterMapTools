using Godot;

namespace BetterMapTools.Features.MapRouting.Modals;

internal static class MapModalLayout
{
    public const float PanelWidth = 840f;
    public const float PanelHeight = 620f;
    public static readonly Vector2 PanelPosition = new(-PanelWidth * 0.5f, -PanelHeight * 0.5f);

    public static void ApplyCenteredPanelLayout(Control target)
    {
        target.AnchorLeft = 0.5f;
        target.AnchorTop = 0.5f;
        target.AnchorRight = 0.5f;
        target.AnchorBottom = 0.5f;
        target.OffsetLeft = -PanelWidth * 0.5f;
        target.OffsetTop = -PanelHeight * 0.5f;
        target.OffsetRight = PanelWidth * 0.5f;
        target.OffsetBottom = PanelHeight * 0.5f;
    }
}
