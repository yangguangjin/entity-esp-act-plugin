using System.Collections.Generic;

namespace EntityEspActPlugin.Core.Models;

public sealed class PointerQwordProbe
{
    public int Index { get; set; }
    public long Value { get; set; }
    public string TargetSection { get; set; } = string.Empty;
    public string AsciiPreview { get; set; } = string.Empty;
    public List<PointerQwordProbe> TableQwords { get; set; } = new List<PointerQwordProbe>();
}
