namespace EntityEspActPlugin.Core.Models;

public sealed class LabelFieldConfig
{
    public bool EntityId { get; set; } = true;
    public bool Kind { get; set; }
    public bool Distance { get; set; }
    public bool Hp { get; set; }
    public bool Position { get; set; }
    public bool BNpcId { get; set; } = true;
    public bool BNpcNameId { get; set; }
    public bool BNpcName { get; set; }
    public bool EObjNameId { get; set; }
    public bool Cast { get; set; }
    public bool CastTargetId { get; set; }
}
