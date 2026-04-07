using System.Collections.Generic;
using BetterMapTools.Features.Settings;

namespace BetterMapTools.Features.MapDrawing;

/// <summary>
/// Shared state for drawing color — recent colors list used by both the
/// drawing color popup and the color quick bar.
/// </summary>
internal static class DrawingColorState
{
    private const int MaxRecentColors = 10;

    public static IReadOnlyList<string> RecentColorRaws => _recentColorRaws;
    private static readonly List<string> _recentColorRaws = [];

    public static event System.Action? RecentColorsChanged;

    public static void Remember(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !RoutingSettings.TryResolveColor(raw, out var parsed))
        {
            return;
        }

        var normalized = MapDrawingColorOverrideService.ToColorRaw(MapDrawingColorOverrideService.NormalizeDrawingColor(parsed));
        _recentColorRaws.RemoveAll(existing => string.Equals(existing, normalized, System.StringComparison.OrdinalIgnoreCase));
        _recentColorRaws.Insert(0, normalized);
        if (_recentColorRaws.Count > MaxRecentColors)
        {
            _recentColorRaws.RemoveRange(MaxRecentColors, _recentColorRaws.Count - MaxRecentColors);
        }

        RecentColorsChanged?.Invoke();
    }
}
