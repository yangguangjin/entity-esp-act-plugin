using System;
using System.Collections.Generic;
using System.Linq;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class RelatedActLogStore
{
    /// <summary>功能：默认只保留最近 120 秒缓存，覆盖当前 UI 最大显示窗口并防止长时间打本无限增长。</summary>
    public const double DefaultCacheRetentionSeconds = 120d;

    private readonly object _syncRoot = new object();
    private readonly Dictionary<uint, List<RelatedActLogEntry>> _logsByEntity = new Dictionary<uint, List<RelatedActLogEntry>>();
    private readonly List<KeyValuePair<uint, RelatedActLogEntry>> _panelLogs = new List<KeyValuePair<uint, RelatedActLogEntry>>();
    private readonly Func<DateTime> _clock;
    private readonly AbilityVfxCandidateProvider? _vfxCandidateProvider;
    private RelatedActLogContext? _context;

    public RelatedActLogStore()
        : this(() => DateTime.UtcNow, new AbilityVfxCandidateProvider())
    {
    }

    public RelatedActLogStore(Func<DateTime> clock)
        : this(clock, null)
    {
    }

    public RelatedActLogStore(Func<DateTime> clock, AbilityVfxCandidateProvider? vfxCandidateProvider)
    {
        _clock = clock;
        _vfxCandidateProvider = vfxCandidateProvider;
    }

    public void UpdateContext(RelatedActLogContext context)
    {
        lock (_syncRoot)
        {
            _context = context;
        }
    }

    public void AddLine(string line, RelatedActLogFilterConfig? filters = null, bool filterPlayerAndPartyLog14 = true, bool filterPlayerAndPartyLog1A = true, RelatedActLogSimplifyConfig? simplify = null, IEnumerable<uint>? casterEntityIdBlacklist = null, IEnumerable<uint>? casterBNpcBlacklist = null, IEnumerable<uint>? casterBNpcNameBlacklist = null, RelatedActLogContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(line) || !IsAllowedByFilter(line, filters ?? new RelatedActLogFilterConfig()))
        {
            return;
        }

        var effectiveContext = context ?? _context;
        if ((filterPlayerAndPartyLog14 && IsLog14FromPlayerOrOwnedEntity(line, effectiveContext))
            || (filterPlayerAndPartyLog1A && IsLog1AFromPlayerOrOwnedEntity(line, effectiveContext))
            || IsCastLineFromBlacklistedCasterEntityId(line, casterEntityIdBlacklist)
            || IsCastLineFromBlacklistedCasterBNpc(line, casterBNpcBlacklist, effectiveContext)
            || IsCastLineFromBlacklistedCasterBNpcName(line, casterBNpcNameBlacklist, effectiveContext))
        {
            return;
        }

        var entityIds = ExtractRelatedEntityIds(line);
        if (entityIds.Count == 0)
        {
            return;
        }

        var now = _clock();
        var entry = new RelatedActLogEntry
        {
            Timestamp = now,
            Line = line,
            DisplayLine = RelatedActLogFormatter.Format(line, simplify),
            VfxCandidate = FindVfxCandidate(line),
        };
        lock (_syncRoot)
        {
            var panelEntityId = ExtractPanelEntityId(line, entityIds);
            if (panelEntityId != 0)
            {
                _panelLogs.Add(new KeyValuePair<uint, RelatedActLogEntry>(panelEntityId, entry));
            }

            foreach (var entityId in entityIds)
            {
                if (!_logsByEntity.TryGetValue(entityId, out var list))
                {
                    list = new List<RelatedActLogEntry>();
                    _logsByEntity[entityId] = list;
                }

                list.Add(entry);
            }

            PruneExpiredLocked(now, DefaultCacheRetentionSeconds);
        }
    }

    public IReadOnlyList<RelatedActLogEntry> GetRecent(uint entityId, double seconds, int maxLines)
    {
        if (entityId == 0 || seconds <= 0 || maxLines <= 0)
        {
            return Array.Empty<RelatedActLogEntry>();
        }

        var cutoff = _clock().AddSeconds(-seconds);
        lock (_syncRoot)
        {
            if (!_logsByEntity.TryGetValue(entityId, out var list))
            {
                return Array.Empty<RelatedActLogEntry>();
            }

            var recent = list.Where(entry => entry.Timestamp >= cutoff).ToArray();
            return recent.Skip(Math.Max(0, recent.Length - maxLines)).ToArray();
        }
    }

    public IReadOnlyList<KeyValuePair<uint, RelatedActLogEntry>> GetRecentForEntities(IEnumerable<uint> entityIds, double seconds, int maxLines)
    {
        if (entityIds == null || seconds <= 0 || maxLines <= 0)
        {
            return Array.Empty<KeyValuePair<uint, RelatedActLogEntry>>();
        }

        var idSet = new HashSet<uint>(entityIds.Where(id => id != 0));
        if (idSet.Count == 0)
        {
            return Array.Empty<KeyValuePair<uint, RelatedActLogEntry>>();
        }

        var cutoff = _clock().AddSeconds(-seconds);
        var result = new List<KeyValuePair<uint, RelatedActLogEntry>>();
        lock (_syncRoot)
        {
            foreach (var entityId in idSet)
            {
                if (!_logsByEntity.TryGetValue(entityId, out var list))
                {
                    continue;
                }

                foreach (var entry in list.Where(entry => entry.Timestamp >= cutoff))
                {
                    result.Add(new KeyValuePair<uint, RelatedActLogEntry>(entityId, entry));
                }
            }
        }

        return result
            .OrderByDescending(pair => pair.Value.Timestamp)
            .Take(maxLines)
            .OrderBy(pair => pair.Value.Timestamp)
            .ToArray();
    }

    public IReadOnlyList<KeyValuePair<uint, RelatedActLogEntry>> GetRecentForPanel(double seconds, int maxLines)
    {
        if (seconds <= 0 || maxLines <= 0)
        {
            return Array.Empty<KeyValuePair<uint, RelatedActLogEntry>>();
        }

        var cutoff = _clock().AddSeconds(-seconds);
        lock (_syncRoot)
        {
            var recent = _panelLogs.Where(pair => pair.Value.Timestamp >= cutoff).ToArray();
            return recent.Skip(Math.Max(0, recent.Length - maxLines)).ToArray();
        }
    }

    /// <summary>功能：删除超过缓存保留窗口的 ACT 日志，防止长时间战斗后查询和渲染越来越慢。</summary>
    private void PruneExpiredLocked(DateTime now, double retentionSeconds)
    {
        var safeRetentionSeconds = Math.Max(1d, retentionSeconds);
        var cutoff = now.AddSeconds(-safeRetentionSeconds);
        foreach (var key in _logsByEntity.Keys.ToList())
        {
            var list = _logsByEntity[key];
            list.RemoveAll(entry => entry.Timestamp < cutoff);
            if (list.Count == 0)
            {
                _logsByEntity.Remove(key);
            }
        }

        _panelLogs.RemoveAll(pair => pair.Value.Timestamp < cutoff);
    }

    private AbilityVfxCandidate? FindVfxCandidate(string line)
    {
        if (_vfxCandidateProvider == null)
        {
            return null;
        }

        var abilityId = ExtractAbilityIdForVfx(line);
        return string.IsNullOrWhiteSpace(abilityId) ? null : _vfxCandidateProvider.GetTopCandidate(abilityId);
    }

    private static string ExtractAbilityIdForVfx(string line)
    {
        var type = GetLineType(line);
        var parts = SplitPayload(line);
        if (type == "14" && parts.Length > 3)
        {
            return parts[3];
        }

        if (type == "20" && parts.Length > 4)
        {
            return parts[4];
        }

        if ((type == "107" || type == "108") && parts.Length > 2)
        {
            return parts[2];
        }

        if ((type == "263" || type == "264") && parts.Length > 3)
        {
            return parts[3];
        }

        return string.Empty;
    }

    private static string[] SplitPayload(string line)
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

        var timestampEnd = trimmed.IndexOf(']');
        if (timestampEnd >= 0 && timestampEnd + 1 < trimmed.Length)
        {
            trimmed = trimmed.Substring(timestampEnd + 1).TrimStart();
        }

        var pipeParts = trimmed.Split('|');
        if (pipeParts.Length > 1)
        {
            return pipeParts;
        }

        var typeIndex = trimmed.IndexOf(" 14:", StringComparison.Ordinal);
        if (typeIndex < 0)
        {
            typeIndex = trimmed.IndexOf(" 107:", StringComparison.Ordinal);
        }

        if (typeIndex < 0)
        {
            typeIndex = trimmed.IndexOf(" 108:", StringComparison.Ordinal);
        }

        if (typeIndex >= 0 && typeIndex + 1 < trimmed.Length)
        {
            trimmed = trimmed.Substring(typeIndex + 1);
        }

        return trimmed.Split(':');
    }

    public static string GetLineType(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return string.Empty;
        }

        var searchStart = 0;
        var timestampEnd = line.IndexOf(']');
        if (timestampEnd >= 0 && timestampEnd + 1 < line.Length)
        {
            searchStart = timestampEnd + 1;
        }

        for (var i = searchStart; i < line.Length - 2; i++)
        {
            if (line[i] == ':' || !IsTypeStart(line[i]))
            {
                continue;
            }

            var end = i + 1;
            while (end < line.Length && IsHexChar(line[end]) && end - i < 3)
            {
                end++;
            }

            if (end < line.Length && line[end] == ':')
            {
                return line.Substring(i, end - i).ToUpperInvariant();
            }
        }

        return string.Empty;
    }

    public static bool IsAllowedByFilter(string line, RelatedActLogFilterConfig filters)
    {
        var type = GetLineType(line);
        switch (type)
        {
            case "03": return filters.Log03;
            case "04": return filters.Log04;
            case "14": return filters.Log14;
            case "15": return filters.Log15;
            case "16": return filters.Log16;
            case "17": return filters.Log17;
            case "18": return filters.Log18;
            case "19": return filters.Log19;
            case "1A": return filters.Log1A;
            case "1B": return filters.Log1B;
            case "1C": return filters.Log1C;
            case "1D": return filters.Log1D;
            case "1E": return filters.Log1E;
            case "21": return filters.Log21;
            case "23": return filters.Log23;
            case "26": return filters.Log26;
            case "27": return filters.Log27;
            case "2A": return filters.Log2A;
            case "105": return filters.Log105;
            case "107": return filters.Log107;
            case "108": return filters.Log108;
            case "10F": return filters.Log10F;
            case "110": return filters.Log110;
            case "111": return filters.Log111;
            case "112": return filters.Log112;
            default: return false;
        }
    }

    private static HashSet<uint> ExtractRelatedEntityIds(string line)
    {
        var type = GetLineType(line);
        if (type == "14" || type == "17")
        {
            var ids = new HashSet<uint>();
            var casterId = ExtractCastCasterEntityId(line);
            var targetId = ExtractCastTargetEntityId(line);
            if (casterId != 0)
            {
                ids.Add(casterId);
            }

            if (targetId != 0)
            {
                ids.Add(targetId);
            }

            return ids;
        }

        return ExtractEntityIds(line);
    }

    private static uint ExtractPanelEntityId(string line, HashSet<uint> entityIds)
    {
        var type = GetLineType(line);
        if (type == "14" || type == "17")
        {
            var casterId = ExtractCastCasterEntityId(line);
            if (casterId != 0)
            {
                return casterId;
            }
        }

        if (type == "1A")
        {
            var sourceId = ExtractStatusAddSourceEntityId(line);
            if (sourceId != 0)
            {
                return sourceId;
            }
        }

        var firstEntityId = ExtractFirstEntityId(line);
        if (firstEntityId != 0)
        {
            return firstEntityId;
        }

        foreach (var entityId in entityIds)
        {
            return entityId;
        }

        return 0;
    }

    private static uint ExtractFirstEntityId(string line)
    {
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

            if (uint.TryParse(token, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var id))
            {
                return id;
            }
        }

        return 0;
    }

    private static HashSet<uint> ExtractEntityIds(string line)
    {
        var ids = new HashSet<uint>();
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

            if (uint.TryParse(token, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static bool IsLog14FromPlayerOrOwnedEntity(string line, RelatedActLogContext? context)
    {
        if (context == null)
        {
            return false;
        }

        if (GetLineType(line) != "14")
        {
            return false;
        }

        var casterId = ExtractCastCasterEntityId(line);
        if (casterId == 0)
        {
            return false;
        }

        if (context.PlayerOrOwnedEntityIds.Contains(casterId) || context.PlayerEntityIds.Contains(casterId))
        {
            return true;
        }

        return context.OwnerIdsByEntityId.TryGetValue(casterId, out var ownerId) && context.PlayerEntityIds.Contains(ownerId);
    }

    private static bool IsLog1AFromPlayerOrOwnedEntity(string line, RelatedActLogContext? context)
    {
        if (context == null)
        {
            return false;
        }

        if (GetLineType(line) != "1A")
        {
            return false;
        }

        var sourceId = ExtractStatusAddSourceEntityId(line);
        if (sourceId == 0)
        {
            return false;
        }

        if (context.PlayerOrOwnedEntityIds.Contains(sourceId) || context.PlayerEntityIds.Contains(sourceId))
        {
            return true;
        }

        return context.OwnerIdsByEntityId.TryGetValue(sourceId, out var ownerId) && context.PlayerEntityIds.Contains(ownerId);
    }

    private static bool IsCastLineFromBlacklistedCasterEntityId(string line, IEnumerable<uint>? casterBlacklist)
    {
        if (casterBlacklist == null || !IsCastLine(line))
        {
            return false;
        }

        var casterId = ExtractCastCasterEntityId(line);
        if (casterId == 0)
        {
            return false;
        }

        return ContainsUInt(casterBlacklist, casterId);
    }

    private static bool IsCastLineFromBlacklistedCasterBNpc(string line, IEnumerable<uint>? casterBNpcBlacklist, RelatedActLogContext? context)
    {
        if (casterBNpcBlacklist == null || context == null || !IsCastLine(line))
        {
            return false;
        }

        var casterId = ExtractCastCasterEntityId(line);
        if (casterId == 0 || !context.BNpcIdsByEntityId.TryGetValue(casterId, out var bnpcId))
        {
            return false;
        }

        return ContainsUInt(casterBNpcBlacklist, bnpcId);
    }

    private static bool IsCastLineFromBlacklistedCasterBNpcName(string line, IEnumerable<uint>? casterBNpcNameBlacklist, RelatedActLogContext? context)
    {
        if (casterBNpcNameBlacklist == null || context == null || !IsCastLine(line))
        {
            return false;
        }

        var casterId = ExtractCastCasterEntityId(line);
        if (casterId == 0 || !context.BNpcNameIdsByEntityId.TryGetValue(casterId, out var bnpcNameId))
        {
            return false;
        }

        return ContainsUInt(casterBNpcNameBlacklist, bnpcNameId);
    }

    private static bool IsCastLine(string line)
    {
        var type = GetLineType(line);
        return type == "14" || type == "17";
    }

    private static bool ContainsUInt(IEnumerable<uint> values, uint target)
    {
        foreach (var value in values)
        {
            if (value == target)
            {
                return true;
            }
        }

        return false;
    }

    private static uint ExtractCastCasterEntityId(string line) => ExtractDelimitedEntityId(line, entityIdOrdinal: 0);

    private static uint ExtractCastTargetEntityId(string line) => ExtractDelimitedEntityId(line, entityIdOrdinal: 1);

    private static uint ExtractStatusAddSourceEntityId(string line) => ExtractDelimitedEntityId(line, entityIdOrdinal: 0);

    private static uint ExtractDelimitedEntityId(string line, int entityIdOrdinal)
    {
        var seen = 0;
        var tokenStart = 0;
        for (var i = 0; i <= line.Length; i++)
        {
            if (i < line.Length && line[i] != ':')
            {
                continue;
            }

            var tokenLength = i - tokenStart;
            if (tokenLength == 8)
            {
                var token = line.Substring(tokenStart, tokenLength);
                if (IsHexEntityIdStart(token[0]) && IsHex8(token) && uint.TryParse(token, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var id))
                {
                    if (seen == entityIdOrdinal)
                    {
                        return id;
                    }

                    seen++;
                }
            }

            tokenStart = i + 1;
        }

        return 0;
    }

    private static bool IsHexEntityIdStart(char value)
    {
        return value == '1' || value == '4' || value == 'E' || value == 'e';
    }

    private static bool IsTypeStart(char value)
    {
        return (value >= '0' && value <= '2') || value == 'F' || value == 'f';
    }

    private static bool IsHexChar(char value)
    {
        return (value >= '0' && value <= '9') || (value >= 'A' && value <= 'F') || (value >= 'a' && value <= 'f');
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
