using System.Collections.Generic;

namespace EntityEspActPlugin.Core.Models;

public readonly struct FilterResult
{
    public FilterResult(bool shouldDisplay, string reason)
    {
        ShouldDisplay = shouldDisplay;
        Reason = reason;
    }

    public bool ShouldDisplay { get; }
    public string Reason { get; }

    public static FilterResult Display(string reason = "display") => new FilterResult(true, reason);
    public static FilterResult Reject(string reason) => new FilterResult(false, reason);
}

public sealed class PartyContext
{
    public PartyContext(uint selfEntityId, ISet<uint> partyEntityIds)
    {
        SelfEntityId = selfEntityId;
        PartyEntityIds = partyEntityIds;
    }

    public uint SelfEntityId { get; }
    public ISet<uint> PartyEntityIds { get; }
}
