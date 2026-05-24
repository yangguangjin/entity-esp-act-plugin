using System;
using System.Globalization;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public static class OverlayStyleService
{
    public static RgbaColor ParseHexColor(string hex, float opacity)
    {
        var alpha = (int)(255f * Clamp(opacity, 0f, 1f));
        if (string.IsNullOrWhiteSpace(hex))
        {
            return new RgbaColor(alpha, 255, 255, 255);
        }

        var normalized = hex.Trim().TrimStart('#');
        if (normalized.Length != 6 || !int.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return new RgbaColor(alpha, 255, 255, 255);
        }

        return new RgbaColor(
            alpha,
            (rgb >> 16) & 0xFF,
            (rgb >> 8) & 0xFF,
            rgb & 0xFF);
    }

    public static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
