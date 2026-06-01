using System;
using System.Collections.Generic;
using System.Linq;

namespace EntityEspActPlugin.Core.Services;

/// <summary>功能：管理 Scene.World VFX 探测时的 vtable 缓存，避免已知 vtable 过度收窄导致新副本 VFX 类型被跳过。</summary>
public sealed class VfxObjectProbeCache
{
    private readonly HashSet<long> _confirmedVTables = new HashSet<long>();
    private readonly Dictionary<long, DateTime> _rejectedUntilByVTable = new Dictionary<long, DateTime>();

    /// <summary>功能：返回已成功解析出 .avfx path 的 VFX vtable 数量，用于面板状态诊断。</summary>
    public int ConfirmedCount => _confirmedVTables.Count;

    /// <summary>功能：清空所有 vtable 探测缓存，通常在服务停止或 FF14 进程重连时调用。</summary>
    public void Clear()
    {
        _confirmedVTables.Clear();
        _rejectedUntilByVTable.Clear();
    }

    /// <summary>功能：判断当前 vtable 是否应该尝试按 VfxObject 解析；未知 vtable 必须允许探测以捕获副本内新类型 VFX。</summary>
    public bool ShouldProbe(long vtable, DateTime now)
    {
        if (vtable == 0)
        {
            return false;
        }

        if (_confirmedVTables.Contains(vtable))
        {
            return true;
        }

        if (_rejectedUntilByVTable.TryGetValue(vtable, out var rejectedUntil) && rejectedUntil > now)
        {
            return false;
        }

        return true;
    }

    /// <summary>功能：记录一次 vtable 探测结果；成功则永久加入确认集，失败只短暂抑制，避免以后同类 VFX path 可读时永远错过。</summary>
    public void MarkProbeResult(long vtable, bool resolvedPath, DateTime now, float rejectionTtlSeconds)
    {
        if (vtable == 0)
        {
            return;
        }

        if (resolvedPath)
        {
            _confirmedVTables.Add(vtable);
            _rejectedUntilByVTable.Remove(vtable);
            return;
        }

        if (_confirmedVTables.Contains(vtable))
        {
            return;
        }

        var safeTtlSeconds = Math.Max(0.1f, rejectionTtlSeconds);
        _rejectedUntilByVTable[vtable] = now.AddSeconds(safeTtlSeconds);
    }

    /// <summary>功能：删除已经过期的失败 vtable 抑制记录，并返回仍处于抑制窗口的数量。</summary>
    public int PruneAndCountRejected(DateTime now)
    {
        foreach (var stale in _rejectedUntilByVTable.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToList())
        {
            _rejectedUntilByVTable.Remove(stale);
        }

        return _rejectedUntilByVTable.Count;
    }
}
