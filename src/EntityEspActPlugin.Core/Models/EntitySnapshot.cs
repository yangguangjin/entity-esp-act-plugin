using System;
using System.Numerics;

namespace EntityEspActPlugin.Core.Models;

public sealed class EntitySnapshot
{
    public ulong Address { get; set; }
    public uint EntityId { get; set; }
    public string EntityIdHex => EntityId.ToString("X8");
    public string Name { get; set; } = string.Empty;

    public EntityKind Kind { get; set; }
    public uint BNpcId { get; set; }
    public uint BNpcNameId { get; set; }
    public uint EObjNameId { get; set; }
    public uint OwnerId { get; set; }

    public Vector3 Position { get; set; }
    public float Heading { get; set; }
    public float HitboxRadius { get; set; }
    public float DistanceToPlayer { get; set; }

    public bool IsTargetable { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public bool IsPartyMember { get; set; }
    public bool IsSelf { get; set; }
    public bool IsCurrentTarget { get; set; }

    public bool IsCasting { get; set; }
    public uint CastId { get; set; }
    public float CastCurrent { get; set; }
    public float CastMax { get; set; }
    public uint CastTargetId { get; set; }

    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}
