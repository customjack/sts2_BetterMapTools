using System;
using System.Globalization;
using Godot;

namespace BetterMapTools.Features.Settings;

internal static partial class RoutingSettings
{
    private static bool TryParseColor(string raw, out Color color)
    {
        color = Colors.White;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var text = raw.Trim();
        if (text.StartsWith("#", StringComparison.Ordinal))
        {
            var hex = text[1..];
            if (hex.Length != 6 && hex.Length != 8)
            {
                return false;
            }

            if (!TryParseHexByte(hex, 0, out var r) || !TryParseHexByte(hex, 2, out var g) || !TryParseHexByte(hex, 4, out var b))
            {
                return false;
            }

            byte a = 255;
            if (hex.Length == 8 && !TryParseHexByte(hex, 6, out a))
            {
                return false;
            }

            color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
            return true;
        }

        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 && parts.Length != 4)
        {
            return false;
        }

        if (!TryParseChannel(parts[0], out var rf) || !TryParseChannel(parts[1], out var gf) || !TryParseChannel(parts[2], out var bf))
        {
            return false;
        }

        var af = 1f;
        if (parts.Length == 4 && !TryParseChannel(parts[3], out af))
        {
            return false;
        }

        color = new Color(rf, gf, bf, af);
        return true;
    }

    private static bool TryParseHexByte(string hex, int start, out byte value)
    {
        return byte.TryParse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseChannel(string value, out float channel)
    {
        channel = 0f;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        if (parsed > 1d)
        {
            parsed /= 255d;
        }

        parsed = Math.Clamp(parsed, 0d, 1d);
        channel = (float)parsed;
        return true;
    }
}
