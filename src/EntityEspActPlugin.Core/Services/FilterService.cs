using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public static class FilterService
{
    public static FilterResult Evaluate(EntitySnapshot entity, EspConfig config, PartyContext party)
    {
        if (!config.Enabled)
        {
            return FilterResult.Reject("disabled");
        }

        if (config.FilterSelf && (entity.IsSelf || entity.EntityId == party.SelfEntityId))
        {
            return FilterResult.Reject("self");
        }

        if (config.FilterPartyPlayers && (entity.IsPartyMember || party.PartyEntityIds.Contains(entity.EntityId)))
        {
            return FilterResult.Reject("party");
        }

        if (config.FilterPartyOwned && entity.OwnerId != 0)
        {
            var ownedBySelf = party.SelfEntityId != 0 && entity.OwnerId == party.SelfEntityId;
            var ownedByParty = config.FilterPartyPlayers && party.PartyEntityIds.Contains(entity.OwnerId);
            if (ownedBySelf || ownedByParty)
            {
                return FilterResult.Reject("party-owned");
            }
        }

        if (!config.ShowUntargetable && !entity.IsTargetable)
        {
            return FilterResult.Reject("untargetable");
        }

        if (!entity.IsVisible)
        {
            return FilterResult.Reject("invisible");
        }

        if (IsBlacklisted(entity, config))
        {
            return FilterResult.Reject("blacklist");
        }

        if (config.MaxDistance > 0 && entity.DistanceToPlayer > config.MaxDistance)
        {
            return FilterResult.Reject("distance");
        }

        return FilterResult.Display();
    }

    private static bool IsBlacklisted(EntitySnapshot entity, EspConfig config) =>
        (entity.EntityId != 0 && config.EntityIdBlacklist.Contains(entity.EntityId)) ||
        (entity.BNpcId != 0 && config.BNpcBlacklist.Contains(entity.BNpcId)) ||
        (entity.BNpcNameId != 0 && config.BNpcNameBlacklist.Contains(entity.BNpcNameId));
}
