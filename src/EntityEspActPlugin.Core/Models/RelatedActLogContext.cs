using System.Collections.Generic;

namespace EntityEspActPlugin.Core.Models;

public sealed class RelatedActLogContext
{
    public HashSet<uint> PlayerEntityIds { get; } = new HashSet<uint>();
    public HashSet<uint> PlayerOrOwnedEntityIds { get; } = new HashSet<uint>();
    public Dictionary<uint, uint> OwnerIdsByEntityId { get; } = new Dictionary<uint, uint>();
    public Dictionary<uint, uint> BNpcIdsByEntityId { get; } = new Dictionary<uint, uint>();
    public Dictionary<uint, uint> BNpcNameIdsByEntityId { get; } = new Dictionary<uint, uint>();

    public static RelatedActLogContext FromEntities(IEnumerable<EntitySnapshot> entities) => FromEntities(entities, null);

    public static RelatedActLogContext FromEntities(IEnumerable<EntitySnapshot> entities, IEnumerable<uint>? trackedPartyEntityIds)
    {
        var context = new RelatedActLogContext();
        if (trackedPartyEntityIds != null)
        {
            foreach (var id in trackedPartyEntityIds)
            {
                if (id != 0)
                {
                    context.PlayerEntityIds.Add(id);
                }
            }
        }
        foreach (var entity in entities)
        {
            if (entity.EntityId == 0)
            {
                continue;
            }

            if (entity.IsSelf || entity.IsPartyMember)
            {
                context.PlayerEntityIds.Add(entity.EntityId);
            }

            if (entity.BNpcId != 0)
            {
                context.BNpcIdsByEntityId[entity.EntityId] = entity.BNpcId;
            }

            if (entity.BNpcNameId != 0)
            {
                context.BNpcNameIdsByEntityId[entity.EntityId] = entity.BNpcNameId;
            }

            if (entity.OwnerId != 0)
            {
                context.OwnerIdsByEntityId[entity.EntityId] = entity.OwnerId;
            }
        }

        foreach (var entity in entities)
        {
            if (entity.EntityId == 0)
            {
                continue;
            }

            if (context.PlayerEntityIds.Contains(entity.EntityId) ||
                (entity.OwnerId != 0 && context.PlayerEntityIds.Contains(entity.OwnerId)))
            {
                context.PlayerOrOwnedEntityIds.Add(entity.EntityId);
            }
        }

        return context;
    }
}
