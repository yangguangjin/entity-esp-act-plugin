using System.Collections.Generic;
using System.Numerics;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class MockEntitySource : IEntitySource
{
    public IReadOnlyList<EntitySnapshot> GetEntities()
    {
        return new[]
        {
            CreateEntity(0x40001001, 20101, -0.45f, -1.8f),
            CreateEntity(0x40001002, 20102, 0.45f, -1.8f),
            CreateEntity(0x40001003, 20103, 0.0f, -1.8f),
            CreateEntity(0x40001004, 20104, -0.25f, -1.4f, isCasting: true),
            CreateEntity(0x40001005, 20105, 0.25f, -1.4f),
            CreateEntity(0x10000010, 20106, 0.65f, -1.6f, isPartyMember: true),
        };
    }

    private static EntitySnapshot CreateEntity(uint entityId, uint bnpcId, float x, float y, bool isCasting = false, bool isPartyMember = false)
    {
        return new EntitySnapshot
        {
            EntityId = entityId,
            BNpcId = bnpcId,
            BNpcNameId = bnpcId + 10000,
            Position = new Vector3(x, y, 0f),
            HitboxRadius = 0.1f,
            DistanceToPlayer = 10f,
            CurrentHp = 100000u + bnpcId,
            MaxHp = 200000u + bnpcId,
            IsVisible = true,
            IsTargetable = true,
            IsPartyMember = isPartyMember,
            IsCasting = isCasting,
            CastId = isCasting ? 0xC341u : 0u,
            CastCurrent = isCasting ? 1.2f : 0f,
            CastMax = isCasting ? 4.0f : 0f,
        };
    }
}
