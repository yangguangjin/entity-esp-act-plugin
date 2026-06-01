using System.Numerics;

namespace EntityEspActPlugin.Core.Models;

public readonly struct VfxAnchorContext
{
    public VfxAnchorContext(VfxAnchorMode mode, Vector3 position, uint entityId = 0)
    {
        Mode = mode;
        Position = position;
        EntityId = entityId;
        HasPosition = true;
    }

    public VfxAnchorContext(VfxAnchorMode mode)
    {
        Mode = mode;
        Position = Vector3.Zero;
        EntityId = 0;
        HasPosition = false;
    }

    public VfxAnchorMode Mode { get; }
    public Vector3 Position { get; }
    public uint EntityId { get; }
    public bool HasPosition { get; }
}
