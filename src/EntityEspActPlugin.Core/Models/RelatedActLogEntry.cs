using System;

namespace EntityEspActPlugin.Core.Models;

public sealed class RelatedActLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Line { get; set; } = string.Empty;
    public string DisplayLine { get; set; } = string.Empty;
    public AbilityVfxCandidate? VfxCandidate { get; set; }
}
