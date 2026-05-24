using System;

namespace EntityEspActPlugin.Core.Models;

public sealed class LiveVfxEntry
{
    public string Path { get; set; } = string.Empty;
    public long Address { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsNew { get; set; }
}
