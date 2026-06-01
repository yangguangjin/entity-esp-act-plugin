using System.Collections.Generic;
using System.Linq;

namespace EntityEspActPlugin.Core.Models;

/// <summary>功能：承载 VFX 面板一次刷新中的实时存活区和历史日志区，避免两种语义混在同一列表。</summary>
public sealed class VfxMonitorSnapshot
{
    /// <summary>功能：提供空快照，供服务未运行、FF14 未就绪或签名失败时安全返回。</summary>
    public static readonly VfxMonitorSnapshot Empty = new VfxMonitorSnapshot(Enumerable.Empty<VfxMonitorEntry>(), Enumerable.Empty<VfxMonitorEntry>());

    /// <summary>功能：当前 Scene.World 中仍实时存活的 VFX；距离和坐标允许随每次刷新变化。</summary>
    public IReadOnlyList<VfxMonitorEntry> LiveEntries { get; }

    /// <summary>功能：日志式历史 VFX 记录；距离和坐标保持首次观测快照，离开 active graph 后仍可 REC/HOLD 显示。</summary>
    public IReadOnlyList<VfxMonitorEntry> HistoryEntries { get; }

    /// <summary>功能：复制传入条目，避免 UI 或测试修改返回快照时污染服务内部缓存。</summary>
    public VfxMonitorSnapshot(IEnumerable<VfxMonitorEntry> liveEntries, IEnumerable<VfxMonitorEntry> historyEntries)
    {
        LiveEntries = (liveEntries ?? Enumerable.Empty<VfxMonitorEntry>()).Select(entry => entry.Clone()).ToList();
        HistoryEntries = (historyEntries ?? Enumerable.Empty<VfxMonitorEntry>()).Select(entry => entry.Clone()).ToList();
    }
}
