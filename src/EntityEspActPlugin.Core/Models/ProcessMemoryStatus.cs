using System.Collections.Generic;

namespace EntityEspActPlugin.Core.Models;

public sealed class ProcessMemoryStatus
{
    public bool ProcessFound { get; set; }
    public int ProcessId { get; set; }
    public bool ModuleFound { get; set; }
    public long ModuleBase { get; set; }
    public int ModuleSize { get; set; }
    public bool HasHandle { get; set; }
    public List<PeSectionInfo> Sections { get; set; } = new List<PeSectionInfo>();
    public string LastError { get; set; } = string.Empty;
}
