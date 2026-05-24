using System;
using System.Collections.Generic;
using System.Globalization;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public static class PatternScanner
{
    public static PatternScanResult Scan(byte[] data, long baseAddress, string name, string pattern, int resolveOffset = 3, int instructionLength = 7, string sectionName = ".text")
    {
        var result = new PatternScanResult
        {
            Name = name,
            Pattern = pattern,
            SectionName = sectionName,
            ResolveOffset = resolveOffset,
            InstructionLength = instructionLength,
        };
        try
        {
            var tokens = Parse(pattern);
            if (tokens.Count == 0)
            {
                result.Error = "empty pattern";
                return result;
            }

            if (data.Length < tokens.Count)
            {
                result.Error = "pattern is longer than data";
                return result;
            }

            for (var i = 0; i <= data.Length - tokens.Count; i++)
            {
                if (!IsMatch(data, i, tokens))
                {
                    continue;
                }

                result.HitCount++;
                if (result.FirstHitAddress == 0)
                {
                    result.FirstHitAddress = baseAddress + i;
                    if (RelativeAddressResolver.TryResolveRipRelative(data, i, result.FirstHitAddress, result.ResolveOffset, result.InstructionLength, out var resolved, out var resolveError))
                    {
                        result.ResolvedAddress = resolved;
                    }
                    else
                    {
                        result.ResolveError = resolveError;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    public static List<int?> Parse(string pattern)
    {
        var tokens = new List<int?>();
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return tokens;
        }

        foreach (var raw in pattern.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim();
            if (token == "?" || token == "??")
            {
                tokens.Add(null);
                continue;
            }

            if (token.Length != 2 || !int.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException("invalid pattern token: " + token);
            }

            tokens.Add(value & 0xFF);
        }

        return tokens;
    }

    private static bool IsMatch(byte[] data, int offset, IReadOnlyList<int?> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            var expected = tokens[i];
            if (expected.HasValue && data[offset + i] != expected.Value)
            {
                return false;
            }
        }

        return true;
    }
}
