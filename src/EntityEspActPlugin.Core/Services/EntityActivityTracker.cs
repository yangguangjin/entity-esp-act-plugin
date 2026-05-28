using System;
using System.Collections.Generic;
using System.Globalization;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class EntityActivityTracker
{
    private static readonly HashSet<string> ActivityLineTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "03", "04",
        "14", "15", "16", "17", "18", "19", "1A", "1B", "1C", "1D", "1E",
        "20", "21", "23", "26", "27", "2A",
        "105", "107", "108", "10F", "110", "111", "112",
    };

    private readonly object _syncRoot = new object();
    private readonly Dictionary<uint, DateTime> _expiresAtByEntityId = new Dictionary<uint, DateTime>();
    private readonly Dictionary<uint, DateTime> _lastSeenByEntityId = new Dictionary<uint, DateTime>();
    private readonly Func<DateTime> _clock;

    public EntityActivityTracker()
        : this(() => DateTime.UtcNow)
    {
    }

    public EntityActivityTracker(Func<DateTime> clock)
    {
        _clock = clock;
    }

    public void ObserveEntities(IEnumerable<EntitySnapshot> entities, double lifetimeSeconds)
    {
        if (entities == null || !TryNormalizeLifetime(lifetimeSeconds, out var lifetime))
        {
            return;
        }

        var now = _clock();
        var defaultExpiresAt = SafeAddSeconds(now, lifetime);
        lock (_syncRoot)
        {
            foreach (var entity in entities)
            {
                if (!IsTrackableEntityId(entity.EntityId))
                {
                    continue;
                }

                _lastSeenByEntityId[entity.EntityId] = now;
                if (!_expiresAtByEntityId.ContainsKey(entity.EntityId))
                {
                    _expiresAtByEntityId[entity.EntityId] = defaultExpiresAt;
                }
            }

            PruneOldGoneEntitiesLocked(now, lifetime);
        }
    }

    public void ObserveLogLine(string line, double lifetimeSeconds)
    {
        if (string.IsNullOrWhiteSpace(line) || !TryNormalizeLifetime(lifetimeSeconds, out var lifetime))
        {
            return;
        }

        var type = GetActivityLineType(line);
        if (!ActivityLineTypes.Contains(type))
        {
            return;
        }

        var terminalEntityIds = ExtractTerminalEntityIds(type, line);
        var entityIds = terminalEntityIds.Count > 0 ? terminalEntityIds : ExtractActivityEntityIds(line);
        if (entityIds.Count == 0)
        {
            return;
        }

        var now = _clock();
        var expiresAt = terminalEntityIds.Count > 0 ? DateTime.MinValue : SafeAddSeconds(now, lifetime);
        lock (_syncRoot)
        {
            foreach (var entityId in entityIds)
            {
                _expiresAtByEntityId[entityId] = expiresAt;
                _lastSeenByEntityId[entityId] = now;
            }

            PruneOldGoneEntitiesLocked(now, lifetime);
        }
    }

    public bool IsActive(EntitySnapshot entity) => IsActive(entity.EntityId);

    public bool IsActive(uint entityId)
    {
        if (!IsTrackableEntityId(entityId))
        {
            return false;
        }

        var now = _clock();
        lock (_syncRoot)
        {
            return _expiresAtByEntityId.TryGetValue(entityId, out var expiresAt) && expiresAt >= now;
        }
    }

    public DateTime GetExpiresAt(uint entityId)
    {
        lock (_syncRoot)
        {
            return _expiresAtByEntityId.TryGetValue(entityId, out var expiresAt) ? expiresAt : DateTime.MinValue;
        }
    }

    public static string GetActivityLineType(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        var trimmed = line.Trim();
        if (trimmed.StartsWith("log ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(4).TrimStart();
        }

        var firstPipe = trimmed.IndexOf('|');
        if (firstPipe > 0 && firstPipe <= 3)
        {
            var typeToken = trimmed.Substring(0, firstPipe);
            if (int.TryParse(typeToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var decimalType))
            {
                return decimalType.ToString("X", CultureInfo.InvariantCulture).ToUpperInvariant();
            }
        }

        return RelatedActLogStore.GetLineType(line);
    }

    public static HashSet<uint> ExtractActivityEntityIds(string line)
    {
        var ids = new HashSet<uint>();
        if (string.IsNullOrWhiteSpace(line))
        {
            return ids;
        }

        for (var i = 0; i <= line.Length - 8; i++)
        {
            if (!IsHexEntityIdStart(line[i]))
            {
                continue;
            }

            var token = line.Substring(i, 8);
            if (!IsHex8(token))
            {
                continue;
            }

            if (uint.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id) && IsTrackableEntityId(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    public static HashSet<uint> ExtractTerminalEntityIds(string type, string line)
    {
        var ids = new HashSet<uint>();
        var fields = SplitActivityPayload(line);
        if (fields.Length == 0)
        {
            return ids;
        }

        if (string.Equals(type, "04", StringComparison.OrdinalIgnoreCase))
        {
            AddEntityId(ids, fields, 0);
            return ids;
        }

        if (string.Equals(type, "19", StringComparison.OrdinalIgnoreCase))
        {
            AddEntityId(ids, fields, 0);
            return ids;
        }

        if (string.Equals(type, "27", StringComparison.OrdinalIgnoreCase))
        {
            if (fields.Length > 2 && IsZeroInteger(fields[2]))
            {
                AddEntityId(ids, fields, 0);
            }

            return ids;
        }

        if (!string.Equals(type, "105", StringComparison.OrdinalIgnoreCase))
        {
            return ids;
        }

        if (fields.Length > 1 && string.Equals(fields[0], "Remove", StringComparison.OrdinalIgnoreCase))
        {
            AddEntityId(ids, fields, 1);
            return ids;
        }

        if (fields.Length > 3
            && (string.Equals(fields[0], "Add", StringComparison.OrdinalIgnoreCase) || string.Equals(fields[0], "Change", StringComparison.OrdinalIgnoreCase))
            && ContainsZeroCurrentHp(fields))
        {
            AddEntityId(ids, fields, 1);
        }

        return ids;
    }

    private static string[] SplitActivityPayload(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return Array.Empty<string>();
        }

        var trimmed = line.Trim();
        if (trimmed.StartsWith("log ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(4).TrimStart();
        }

        var pipeParts = trimmed.Split('|');
        if (pipeParts.Length > 2 && int.TryParse(pipeParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            var payload = new string[pipeParts.Length - 2];
            Array.Copy(pipeParts, 2, payload, 0, payload.Length);
            return payload;
        }

        var timestampEnd = trimmed.IndexOf(']');
        if (timestampEnd >= 0 && timestampEnd + 1 < trimmed.Length)
        {
            trimmed = trimmed.Substring(timestampEnd + 1).TrimStart();
        }

        var type = GetActivityLineType(line);
        if (string.IsNullOrWhiteSpace(type))
        {
            return Array.Empty<string>();
        }

        var marker = type + ":";
        var typeIndex = trimmed.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (typeIndex < 0 || typeIndex + marker.Length >= trimmed.Length)
        {
            return Array.Empty<string>();
        }

        return trimmed.Substring(typeIndex + marker.Length).Split(':');
    }

    private static void AddEntityId(HashSet<uint> ids, string[] fields, int index)
    {
        if (index < 0 || index >= fields.Length)
        {
            return;
        }

        if (TryParseEntityId(fields[index], out var entityId))
        {
            ids.Add(entityId);
        }
    }

    private static bool TryParseEntityId(string token, out uint entityId)
    {
        entityId = 0;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        token = token.Trim();
        if (token.Length != 8 || !IsHexEntityIdStart(token[0]) || !IsHex8(token))
        {
            return false;
        }

        return uint.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out entityId) && IsTrackableEntityId(entityId);
    }

    private static bool ContainsZeroCurrentHp(string[] fields)
    {
        for (var i = 2; i < fields.Length - 1; i += 2)
        {
            if (string.Equals(fields[i], "CurrentHP", StringComparison.OrdinalIgnoreCase) && IsZeroInteger(fields[i + 1]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsZeroInteger(string value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed == 0;
    }

    private static bool TryNormalizeLifetime(double lifetimeSeconds, out double lifetime)
    {
        lifetime = 0;
        if (double.IsNaN(lifetimeSeconds) || double.IsInfinity(lifetimeSeconds) || lifetimeSeconds <= 0)
        {
            return false;
        }

        lifetime = Math.Min(600d, lifetimeSeconds);
        return true;
    }

    private static DateTime SafeAddSeconds(DateTime value, double seconds)
    {
        if (value > DateTime.MaxValue.AddSeconds(-seconds))
        {
            return DateTime.MaxValue;
        }

        return value.AddSeconds(seconds);
    }

    private void PruneOldGoneEntitiesLocked(DateTime now, double lifetimeSeconds)
    {
        var retentionSeconds = Math.Max(60d, lifetimeSeconds * 4d);
        var retentionCutoff = now.AddSeconds(-retentionSeconds);
        var toRemove = new List<uint>();
        foreach (var pair in _lastSeenByEntityId)
        {
            var expiresAt = _expiresAtByEntityId.TryGetValue(pair.Key, out var value) ? value : DateTime.MinValue;
            if (pair.Value < retentionCutoff && expiresAt < retentionCutoff)
            {
                toRemove.Add(pair.Key);
            }
        }

        foreach (var entityId in toRemove)
        {
            _lastSeenByEntityId.Remove(entityId);
            _expiresAtByEntityId.Remove(entityId);
        }
    }

    private static bool IsTrackableEntityId(uint entityId)
    {
        return entityId != 0 && entityId != 0xE0000000;
    }

    private static bool IsHexEntityIdStart(char value)
    {
        return value == '1' || value == '4' || value == 'E' || value == 'e';
    }

    private static bool IsHex8(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
            {
                return false;
            }
        }

        return true;
    }
}
