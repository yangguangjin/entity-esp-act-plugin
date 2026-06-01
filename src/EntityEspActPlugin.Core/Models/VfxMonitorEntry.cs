using System;
using System.Numerics;

namespace EntityEspActPlugin.Core.Models;

public sealed class VfxMonitorEntry
{
    public string Path { get; set; } = string.Empty;
    public long Address { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsNew { get; set; }

    /// <summary>功能：标记该条目本次刷新仍在 Scene.World active graph 中。</summary>
    public bool IsCurrentlyActive { get; set; } = true;

    /// <summary>功能：标记该条目已经离开 active graph，但因短存活 N/M 规则额外续显。</summary>
    public bool IsHeldShortLived { get; set; }

    public VfxEntrySource Source { get; set; } = VfxEntrySource.PathScanFallback;
    /// <summary>功能：记录 VFX 世界坐标；实时区使用当前内存值，历史区使用首次观测快照。</summary>
    public Vector3? Position { get; set; }

    /// <summary>功能：记录相对距离；实时区每次刷新可更新，历史区保留首次观测距离。</summary>
    public float? Distance { get; set; }
    public uint CasterEntityId { get; set; }
    public uint TargetEntityId { get; set; }
    public string Kind { get; set; } = "Unknown";

    public VfxMonitorEntry Clone()
    {
        return new VfxMonitorEntry
        {
            Path = Path,
            Address = Address,
            FirstSeenAt = FirstSeenAt,
            LastSeenAt = LastSeenAt,
            IsNew = IsNew,
            IsCurrentlyActive = IsCurrentlyActive,
            IsHeldShortLived = IsHeldShortLived,
            Source = Source,
            Position = Position,
            Distance = Distance,
            CasterEntityId = CasterEntityId,
            TargetEntityId = TargetEntityId,
            Kind = Kind,
        };
    }
}
