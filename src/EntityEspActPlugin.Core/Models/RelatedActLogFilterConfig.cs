namespace EntityEspActPlugin.Core.Models;

public sealed class RelatedActLogFilterConfig
{
    public bool Log03 { get; set; }
    public bool Log04 { get; set; }
    public bool Log14 { get; set; } = true;
    public bool Log15 { get; set; }
    public bool Log16 { get; set; }
    public bool Log17 { get; set; }
    public bool Log18 { get; set; }
    public bool Log19 { get; set; }
    public bool Log1A { get; set; } = true;
    public bool Log1B { get; set; }
    public bool Log1C { get; set; }
    public bool Log1D { get; set; }
    public bool Log1E { get; set; }
    public bool Log21 { get; set; }
    public bool Log23 { get; set; }
    public bool Log26 { get; set; }
    public bool Log27 { get; set; }
    public bool Log2A { get; set; }
    public bool Log105 { get; set; }
    public bool Log107 { get; set; }
    public bool Log108 { get; set; }
    public bool Log10F { get; set; }
    public bool Log110 { get; set; }
    public bool Log111 { get; set; }
    public bool Log112 { get; set; }

    // Legacy aggregate fields kept for old JSON compatibility only.
    public bool Combatant { get; set; }
    public bool Cast { get; set; }
    public bool Ability { get; set; }
    public bool Status { get; set; }
    public bool Headmarker { get; set; }
    public bool Markers { get; set; }
    public bool ActorControl { get; set; }
    public bool Tether { get; set; }
    public bool HpDeath { get; set; }
    public bool ExtraPosition { get; set; }
}
