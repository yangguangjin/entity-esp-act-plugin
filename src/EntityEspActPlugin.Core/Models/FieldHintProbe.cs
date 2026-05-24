using System.Collections.Generic;

namespace EntityEspActPlugin.Core.Models;

public sealed class FieldHintProbe
{
    public string Owner { get; set; } = string.Empty;
    public int Offset { get; set; }
    public string Kind { get; set; } = string.Empty;
    public long RawQword { get; set; }
    public string TargetSection { get; set; } = string.Empty;
    public List<float> Floats { get; set; } = new List<float>();
}
