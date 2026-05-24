using System.Collections.Generic;

namespace EntityEspActPlugin.Core.Models;

public sealed class EntityTableProbeResult
{
    public string Name { get; set; } = "EntityTable";
    public long SlotAddress { get; set; }
    public bool PointerReadSuccess { get; set; }
    public long PointerValue { get; set; }
    public string PointerTargetSection { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public List<PointerQwordProbe> Qwords { get; set; } = new List<PointerQwordProbe>();
    public List<float> Floats { get; set; } = new List<float>();
    public List<EntityTableChildPointerProbe> ChildPointers { get; set; } = new List<EntityTableChildPointerProbe>();
    public List<ArrayCandidateProbe> ArrayCandidates { get; set; } = new List<ArrayCandidateProbe>();
    public List<FieldHintProbe> FieldHints { get; set; } = new List<FieldHintProbe>();
}
