namespace EntityEspActPlugin.Core.Models;

public sealed class PeSectionInfo
{
    public string Name { get; set; } = string.Empty;
    public int VirtualAddress { get; set; }
    public int VirtualSize { get; set; }
    public int RawSize { get; set; }
    public long StartAddress { get; set; }
    public int ScanSize => VirtualSize > 0 ? VirtualSize : RawSize;
}
