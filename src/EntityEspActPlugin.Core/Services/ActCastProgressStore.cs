using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class ActCastProgressStore
{
    private readonly object _syncRoot = new object();
    private readonly Dictionary<uint, ActCastProgressEntry> _castsBySource = new Dictionary<uint, ActCastProgressEntry>();
    private readonly Func<DateTime> _clock;

    public ActCastProgressStore()
        : this(() => DateTime.UtcNow)
    {
    }

    public ActCastProgressStore(Func<DateTime> clock)
    {
        _clock = clock;
    }

    public void ObserveLogLine(string line)
    {
        if (!TryParseCastStart(line, _clock(), out var entry))
        {
            return;
        }

        lock (_syncRoot)
        {
            _castsBySource[entry.SourceEntityId] = entry;
        }
    }

    public ActCastProgressEntry? GetActive(uint sourceEntityId)
    {
        if (sourceEntityId == 0)
        {
            return null;
        }

        var now = _clock();
        lock (_syncRoot)
        {
            PruneExpiredLocked(now);
            return _castsBySource.TryGetValue(sourceEntityId, out var entry) ? entry : null;
        }
    }

    public IReadOnlyList<ActCastProgressEntry> GetActive(IEnumerable<uint> sourceEntityIds)
    {
        var ids = sourceEntityIds?.Where(id => id != 0).Distinct().ToArray() ?? Array.Empty<uint>();
        if (ids.Length == 0)
        {
            return Array.Empty<ActCastProgressEntry>();
        }

        var now = _clock();
        lock (_syncRoot)
        {
            PruneExpiredLocked(now);
            return ids.Where(id => _castsBySource.ContainsKey(id)).Select(id => _castsBySource[id]).ToArray();
        }
    }

    private void PruneExpiredLocked(DateTime now)
    {
        var expired = _castsBySource
            .Where(pair => now > pair.Value.EndsAt.AddSeconds(0.35))
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var key in expired)
        {
            _castsBySource.Remove(key);
        }
    }

    public static bool TryParseCastStart(string line, DateTime now, out ActCastProgressEntry entry)
    {
        entry = new ActCastProgressEntry();
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var payload = StripPrefix(line);
        if (payload.Contains("|") && TryParseNetwork20(payload, now, out entry))
        {
            return true;
        }

        return TryParseParsed14(payload, now, out entry);
    }

    private static bool TryParseNetwork20(string payload, DateTime now, out ActCastProgressEntry entry)
    {
        entry = new ActCastProgressEntry();
        var parts = payload.Split('|');
        if (parts.Length < 9 || parts[0] != "20")
        {
            return false;
        }

        if (!TryParseHexUint(parts[2], out var sourceId) || !TryParseHexUint(parts[4], out var abilityId) || !TryParseFloat(parts[8], out var castSeconds))
        {
            return false;
        }

        if (!IsValidCastDuration(castSeconds))
        {
            return false;
        }

        entry = new ActCastProgressEntry
        {
            SourceEntityId = sourceId,
            SourceName = parts[3],
            AbilityId = abilityId,
            AbilityName = parts[5],
            CastSeconds = castSeconds,
            StartedAt = now,
        };
        return true;
    }

    private static bool TryParseParsed14(string payload, DateTime now, out ActCastProgressEntry entry)
    {
        entry = new ActCastProgressEntry();
        var typeIndex = payload.IndexOf("14:", StringComparison.Ordinal);
        if (typeIndex > 0)
        {
            payload = payload.Substring(typeIndex);
        }

        var parts = payload.Split(':');
        if (parts.Length < 8 || parts[0] != "14")
        {
            return false;
        }

        if (!TryParseHexUint(parts[1], out var sourceId) || !TryParseHexUint(parts[3], out var abilityId) || !TryParseFloat(parts[7], out var castSeconds))
        {
            return false;
        }

        if (!IsValidCastDuration(castSeconds))
        {
            return false;
        }

        entry = new ActCastProgressEntry
        {
            SourceEntityId = sourceId,
            SourceName = parts[2],
            AbilityId = abilityId,
            AbilityName = parts[4],
            CastSeconds = castSeconds,
            StartedAt = now,
        };
        return true;
    }

    private static string StripPrefix(string line)
    {
        var payload = line.Trim();
        if (payload.StartsWith("log ", StringComparison.OrdinalIgnoreCase))
        {
            payload = payload.Substring(4).TrimStart();
        }

        var timestampEnd = payload.IndexOf(']');
        if (timestampEnd >= 0 && timestampEnd + 1 < payload.Length)
        {
            payload = payload.Substring(timestampEnd + 1).TrimStart();
        }

        return payload;
    }

    private static bool TryParseHexUint(string value, out uint parsed)
    {
        return uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed);
    }

    private static bool IsValidCastDuration(float seconds)
    {
        return seconds > 0f && seconds <= 3600f && !float.IsNaN(seconds) && !float.IsInfinity(seconds);
    }

    private static bool TryParseFloat(string value, out float parsed)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
    }
}
