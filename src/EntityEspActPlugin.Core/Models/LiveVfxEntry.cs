using System;

namespace EntityEspActPlugin.Core.Models;

public sealed class LiveVfxEntry
{
    /// <summary>功能：记录扫描到的 VFX 资源路径，用于面板显示候选 .avfx。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>功能：记录本次扫描命中的字符串地址，用于后续诊断 path 来源。</summary>
    public long Address { get; set; }

    /// <summary>功能：记录该 VFX 路径第一次被观察到的时间，用于判断 NEW 和短存活。</summary>
    public DateTime FirstSeenAt { get; set; }

    /// <summary>功能：记录该 VFX 路径最近一次被观察到的时间，用于显示年龄和过期。</summary>
    public DateTime LastSeenAt { get; set; }

    /// <summary>功能：标记该路径是否是 baseline 后新出现的候选 VFX。</summary>
    public bool IsNew { get; set; }

    /// <summary>功能：标记该路径已经未再次扫描到，但因短存活续显规则临时保留。</summary>
    public bool IsHeldShortLived { get; set; }
}
