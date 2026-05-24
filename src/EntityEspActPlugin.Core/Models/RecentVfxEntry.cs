using System;

namespace EntityEspActPlugin.Core.Models;

public sealed class RecentVfxEntry
{
    public DateTime SeenAt { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}
