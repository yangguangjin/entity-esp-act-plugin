using System;
using System.Numerics;

namespace EntityEspActPlugin.Core.Models;

public sealed class EntityDisplayState
{
    public EntitySnapshot Snapshot { get; set; } = new();
    public Vector2 ScreenPosition { get; set; }
    public Vector2 CenterScreenPosition { get; set; }
    public bool HasCenterScreenPosition { get; set; }
    public bool OnScreen { get; set; }
    public string LabelText { get; set; } = string.Empty;
    public uint Color { get; set; }
    public bool Pinned { get; set; }
    public DateTime PinExpireAt { get; set; }
    public string FilterReason { get; set; } = string.Empty;
    public bool IsFilteredDebug { get; set; }
    public float CastProgress { get; set; }
}
