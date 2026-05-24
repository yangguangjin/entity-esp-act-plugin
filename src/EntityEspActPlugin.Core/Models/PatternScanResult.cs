namespace EntityEspActPlugin.Core.Models;

public sealed class PatternScanResult
{
    public string Name { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string SectionName { get; set; } = ".text";
    public int HitCount { get; set; }
    public long FirstHitAddress { get; set; }
    public long ResolvedAddress { get; set; }
    public int ResolveOffset { get; set; } = 3;
    public int InstructionLength { get; set; } = 7;
    public bool Success => HitCount > 0;
    public bool IsCached { get; set; }
    public string Error { get; set; } = string.Empty;
    public string ResolveError { get; set; } = string.Empty;
}
