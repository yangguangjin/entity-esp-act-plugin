using System.Collections.Generic;

namespace EntityEspActPlugin.Core.Models;

public sealed class RipCandidate
{
    public string Opcode { get; set; } = string.Empty;
    public List<string> Opcodes { get; set; } = new List<string>();
    public long InstructionAddress { get; set; }
    public long ResolvedAddress { get; set; }
    public string TargetSection { get; set; } = string.Empty;
    public int ReferenceCount { get; set; }
    public bool PointerReadSuccess { get; set; }
    public long PointerValue { get; set; }
    public string PointerTargetSection { get; set; } = string.Empty;
    public string PointerReadError { get; set; } = string.Empty;
    public bool HeapProbeSuccess { get; set; }
    public string HeapProbeError { get; set; } = string.Empty;
    public List<PointerQwordProbe> HeapQwords { get; set; } = new List<PointerQwordProbe>();
    public string CandidateSignature { get; set; } = string.Empty;
    public int CandidateSignatureHits { get; set; }
    public int CandidateResolveOffset { get; set; } = 3;
    public int CandidateInstructionLength { get; set; } = 7;
}

