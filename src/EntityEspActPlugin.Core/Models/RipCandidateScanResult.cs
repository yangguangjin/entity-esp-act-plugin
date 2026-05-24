using System.Collections.Generic;

namespace EntityEspActPlugin.Core.Models;

public sealed class RipCandidateScanResult
{
    public string SectionName { get; set; } = ".text";
    public int ScannedBytes { get; set; }
    public int TotalGroups { get; set; }
    public int FilteredGroups { get; set; }
    public int MinRefs { get; set; }
    public int MaxRefs { get; set; }
    public bool IsCached { get; set; }
    public string Error { get; set; } = string.Empty;
    public List<RipCandidate> Candidates { get; set; } = new List<RipCandidate>();
}
