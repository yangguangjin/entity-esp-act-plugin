using System;
using System.Collections.Generic;
using System.Linq;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

/// <summary>功能：把每帧 active VFX 快照转换成类似日志的显示缓存，支持默认保留和短命续显。</summary>
public sealed class ActiveVfxDisplayCache
{
    private readonly Dictionary<string, CachedVfxEntry> _entriesByKey = new Dictionary<string, CachedVfxEntry>(StringComparer.OrdinalIgnoreCase);

    /// <summary>功能：清空缓存，通常在插件关闭或 VFX 面板停用时调用。</summary>
    public void Clear()
    {
        _entriesByKey.Clear();
    }

    /// <summary>功能：合并本帧 active VFX，并返回仍需显示的日志式 VFX 条目。</summary>
    public IReadOnlyList<VfxMonitorEntry> Update(
        IEnumerable<VfxMonitorEntry> activeEntries,
        float displaySeconds,
        float shortLivedMaxAgeSeconds,
        float shortLivedHoldSeconds,
        DateTime now)
    {
        var safeDisplaySeconds = Math.Max(0.1f, displaySeconds);
        var safeShortLivedMaxAgeSeconds = Math.Max(0f, shortLivedMaxAgeSeconds);
        var safeShortLivedHoldSeconds = Math.Max(0f, shortLivedHoldSeconds);
        var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var activeEntry in activeEntries.Where(entry => entry.Address != 0))
        {
            var key = BuildCacheKey(activeEntry);
            activeKeys.Add(key);
            UpsertActiveEntry(key, activeEntry, now);
        }

        foreach (var key in _entriesByKey.Keys.ToList())
        {
            if (activeKeys.Contains(key))
            {
                continue;
            }

            if (!ShouldKeepInactive(_entriesByKey[key], safeDisplaySeconds, safeShortLivedMaxAgeSeconds, safeShortLivedHoldSeconds, now))
            {
                _entriesByKey.Remove(key);
            }
        }

        return _entriesByKey.Values
            .Select(entry => BuildDisplayEntry(entry, activeKeys.Contains(entry.Key), safeDisplaySeconds, safeShortLivedMaxAgeSeconds, safeShortLivedHoldSeconds, now))
            .OrderByDescending(entry => entry.FirstSeenAt)
            .ThenBy(entry => entry.Distance ?? float.MaxValue)
            .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>功能：用地址 + path 组成日志缓存键，避免同一地址切换不同 VFX path 时覆盖旧记录。</summary>
    private static string BuildCacheKey(VfxMonitorEntry entry)
    {
        return entry.Address.ToString("X") + "|" + (entry.Path ?? string.Empty).ToLowerInvariant();
    }

    /// <summary>功能：新增或刷新本帧仍 active 的 VFX 缓存条目，同时保留首次观测到的日志快照。</summary>
    private void UpsertActiveEntry(string key, VfxMonitorEntry activeEntry, DateTime now)
    {
        if (!_entriesByKey.TryGetValue(key, out var cached))
        {
            var firstSeen = activeEntry.FirstSeenAt == default ? now : activeEntry.FirstSeenAt;
            cached = new CachedVfxEntry
            {
                Key = key,
                Address = activeEntry.Address,
                FirstSeenAt = firstSeen,
                LastSeenAt = firstSeen,
                DisplayEntry = activeEntry.Clone(),
            };
            cached.DisplayEntry.FirstSeenAt = firstSeen;
            cached.DisplayEntry.LastSeenAt = firstSeen;
            _entriesByKey[key] = cached;
        }

        cached.LastSeenAt = now;
        cached.DisplayEntry.LastSeenAt = now;
        cached.DisplayEntry.IsCurrentlyActive = true;
        cached.DisplayEntry.IsHeldShortLived = false;
        cached.DisplayEntry.IsNew = (now - cached.FirstSeenAt).TotalSeconds <= 1.5;
    }

    /// <summary>功能：判断已经离开 active graph 的条目是否仍应按普通窗口或短命续显保留。</summary>
    private static bool ShouldKeepInactive(
        CachedVfxEntry cached,
        float displaySeconds,
        float shortLivedMaxAgeSeconds,
        float shortLivedHoldSeconds,
        DateTime now)
    {
        var ageSinceLastSeen = Math.Max(0, (now - cached.LastSeenAt).TotalSeconds);
        if (ageSinceLastSeen <= displaySeconds)
        {
            return true;
        }

        return IsShortLived(cached, shortLivedMaxAgeSeconds)
            && shortLivedHoldSeconds > 0
            && ageSinceLastSeen <= displaySeconds + shortLivedHoldSeconds;
    }

    /// <summary>功能：生成展示用条目，并标记 ACTIVE/RECENT/HOLD 所需状态字段。</summary>
    private static VfxMonitorEntry BuildDisplayEntry(
        CachedVfxEntry cached,
        bool isCurrentlyActive,
        float displaySeconds,
        float shortLivedMaxAgeSeconds,
        float shortLivedHoldSeconds,
        DateTime now)
    {
        var display = cached.DisplayEntry.Clone();
        var ageSinceLastSeen = Math.Max(0, (now - cached.LastSeenAt).TotalSeconds);
        display.FirstSeenAt = cached.FirstSeenAt;
        display.LastSeenAt = cached.LastSeenAt;
        display.IsCurrentlyActive = isCurrentlyActive;
        display.IsHeldShortLived = !isCurrentlyActive
            && ageSinceLastSeen > displaySeconds
            && IsShortLived(cached, shortLivedMaxAgeSeconds)
            && shortLivedHoldSeconds > 0
            && ageSinceLastSeen <= displaySeconds + shortLivedHoldSeconds;
        display.IsNew = isCurrentlyActive && (now - cached.FirstSeenAt).TotalSeconds <= 1.5;
        return display;
    }

    /// <summary>功能：判断一个 VFX 记录从首次到末次 active 是否小于短命阈值 N。</summary>
    private static bool IsShortLived(CachedVfxEntry cached, float shortLivedMaxAgeSeconds)
    {
        if (shortLivedMaxAgeSeconds <= 0)
        {
            return false;
        }

        return Math.Max(0, (cached.LastSeenAt - cached.FirstSeenAt).TotalSeconds) <= shortLivedMaxAgeSeconds;
    }

    /// <summary>功能：保存单个 active VFX 日志记录的首次快照与生命周期时间。</summary>
    private sealed class CachedVfxEntry
    {
        public string Key { get; set; } = string.Empty;
        public long Address { get; set; }
        public DateTime FirstSeenAt { get; set; }
        public DateTime LastSeenAt { get; set; }
        public VfxMonitorEntry DisplayEntry { get; set; } = new VfxMonitorEntry();
    }
}
