using System.Collections.Generic;

namespace EntityEspActPlugin.Core.Models;

public sealed class EntityTableChildPointerProbe
{
    public int Offset { get; set; }
    public long PointerValue { get; set; }
    public string PointerTargetSection { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public List<PointerQwordProbe> Qwords { get; set; } = new List<PointerQwordProbe>();
    public List<float> Floats { get; set; } = new List<float>();
}
