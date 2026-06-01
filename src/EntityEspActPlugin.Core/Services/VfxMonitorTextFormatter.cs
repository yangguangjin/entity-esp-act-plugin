using System;
using System.Collections.Generic;
using System.Numerics;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

/// <summary>功能：把 VFX 实时存活快照和历史日志快照格式化为面板可直接显示的多行文本。</summary>
public static class VfxMonitorTextFormatter
{
    private const string LiveHeader = "========== 当前实时存活 VFX ==========";
    private const string HistoryHeader = "========== 历史 VFX 日志 ==========";

    /// <summary>功能：生成带明显分隔符的 VFX 面板文本，实时区在前，历史日志区在后。</summary>
    public static string Format(VfxMonitorSnapshot snapshot, DateTime now, string statusText)
    {
        snapshot ??= VfxMonitorSnapshot.Empty;
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(statusText))
        {
            lines.Add(statusText);
        }

        lines.Add(LiveHeader);
        AppendSection(lines, snapshot.LiveEntries, now, isHistorySection: false);
        lines.Add(string.Empty);
        lines.Add(HistoryHeader);
        AppendSection(lines, snapshot.HistoryEntries, now, isHistorySection: true);
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>功能：生成适合复制到触发器备注/资料的去重 .avfx 路径列表，优先保留实时区再补历史区。</summary>
    public static string FormatDistinctPaths(VfxMonitorSnapshot snapshot)
    {
        snapshot ??= VfxMonitorSnapshot.Empty;
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in EnumerateCopyEntries(snapshot))
        {
            if (string.IsNullOrWhiteSpace(entry.Path) || !seen.Add(entry.Path))
            {
                continue;
            }

            paths.Add(entry.Path);
        }

        return string.Join(Environment.NewLine, paths);
    }

    /// <summary>功能：按复制优先级枚举 VFX 条目，确保当前实时结果排在历史记录前面。</summary>
    private static IEnumerable<VfxMonitorEntry> EnumerateCopyEntries(VfxMonitorSnapshot snapshot)
    {
        foreach (var entry in snapshot.LiveEntries)
        {
            yield return entry;
        }

        foreach (var entry in snapshot.HistoryEntries)
        {
            yield return entry;
        }
    }

    /// <summary>功能：追加单个分区内容，并在没有条目时给出明确空态说明。</summary>
    private static void AppendSection(List<string> lines, IReadOnlyList<VfxMonitorEntry> entries, DateTime now, bool isHistorySection)
    {
        if (entries.Count == 0)
        {
            lines.Add(isHistorySection ? "no history VFX log rows" : "no current active VFX instances");
            return;
        }

        foreach (var entry in entries)
        {
            lines.Add(FormatEntry(entry, now, isHistorySection));
        }
    }

    /// <summary>功能：格式化单条 VFX；实时区保留原实时语义，历史区显示首次观测快照与 REC/HOLD 状态。</summary>
    private static string FormatEntry(VfxMonitorEntry entry, DateTime now, bool isHistorySection)
    {
        var state = isHistorySection ? GetHistoryState(entry) : GetLiveState(entry);
        var ageStart = isHistorySection ? SafeTime(entry.FirstSeenAt, entry.LastSeenAt, now) : SafeTime(entry.LastSeenAt, entry.FirstSeenAt, now);
        var age = Math.Max(0, (int)Math.Ceiling((now - ageStart).TotalSeconds));
        var distance = entry.Distance.HasValue ? entry.Distance.Value.ToString("0.0") : "n/a";
        var position = entry.Position.HasValue ? " pos=" + FormatPosition(entry.Position.Value) : string.Empty;
        return state + " " + age.ToString("00") + "s dist=" + distance
            + " ActiveInstance caster=0x" + entry.CasterEntityId.ToString("X8")
            + " target=0x" + entry.TargetEntityId.ToString("X8")
            + position + " " + entry.Path;
    }

    /// <summary>功能：实时区只表达当前仍 active 的状态，保持原实时扫描列表语义。</summary>
    private static string GetLiveState(VfxMonitorEntry entry)
    {
        return entry.IsNew ? "NEW " : "LIVE";
    }

    /// <summary>功能：历史区表达日志记录状态，区分仍 active、普通保留和短命续显。</summary>
    private static string GetHistoryState(VfxMonitorEntry entry)
    {
        if (entry.IsCurrentlyActive)
        {
            return entry.IsNew ? "NEW " : "LIVE";
        }

        return entry.IsHeldShortLived ? "HOLD" : "REC ";
    }

    /// <summary>功能：格式化世界坐标，方便和游戏内方位/距离对照。</summary>
    private static string FormatPosition(Vector3 position)
    {
        return "(" + position.X.ToString("0.0") + "," + position.Y.ToString("0.0") + "," + position.Z.ToString("0.0") + ")";
    }

    /// <summary>功能：处理默认时间值，避免新建条目缺少时间戳时出现异常年龄。</summary>
    private static DateTime SafeTime(DateTime preferred, DateTime fallback, DateTime now)
    {
        if (preferred != default)
        {
            return preferred;
        }

        return fallback != default ? fallback : now;
    }
}
