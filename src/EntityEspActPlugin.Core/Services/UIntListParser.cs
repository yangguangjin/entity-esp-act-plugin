using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EntityEspActPlugin.Core.Services;

public static class UIntListParser
{
    public static List<uint> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<uint>();
        }

        var values = new List<uint>();
        var seen = new HashSet<uint>();
        var parts = text.Split(new[] { ',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in parts)
        {
            if (!TryParseToken(raw.Trim(), out var value) || !seen.Add(value))
            {
                continue;
            }

            values.Add(value);
        }

        return values;
    }

    public static string Format(IEnumerable<uint> values)
    {
        return string.Join(", ", values ?? Enumerable.Empty<uint>());
    }

    private static bool TryParseToken(string token, out uint value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.TryParse(token.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        return uint.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
