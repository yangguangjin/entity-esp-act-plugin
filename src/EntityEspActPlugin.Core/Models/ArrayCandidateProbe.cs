using System.Collections.Generic;

namespace EntityEspActPlugin.Core.Models;

public sealed class ArrayCandidateProbe
{
    public string Owner { get; set; } = string.Empty;
    public int Offset { get; set; }
    public string Kind { get; set; } = string.Empty;
    public long Begin { get; set; }
    public long End { get; set; }
    public long Capacity { get; set; }
    public int Count { get; set; }
    public int StrideGuess { get; set; }
    public string Error { get; set; } = string.Empty;
    public List<PointerQwordProbe> Samples { get; set; } = new List<PointerQwordProbe>();
}
