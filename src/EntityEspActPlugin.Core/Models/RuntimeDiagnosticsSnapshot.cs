using System;

namespace EntityEspActPlugin.Core.Models;

public sealed class RuntimeDiagnosticsSnapshot
{
    public string EntitySourceName { get; set; } = "unknown";
    public string CameraSourceName { get; set; } = "unknown";
    public bool CameraValid { get; set; }
    public int ViewportWidth { get; set; }
    public int ViewportHeight { get; set; }
    public int RawEntityCount { get; set; }
    public bool EntitySourceReady { get; set; } = true;
    public bool CameraSourceReady { get; set; } = true;
    public string SourceStatus { get; set; } = string.Empty;
    public ProcessMemoryStatus? ProcessMemory { get; set; }
    public System.Collections.Generic.List<PatternScanResult> PatternScans { get; set; } = new System.Collections.Generic.List<PatternScanResult>();
    public int VisibleStateCount { get; set; }
    public int FilteredDebugCount { get; set; }
    public int NormalVisibleCount => Math.Max(0, VisibleStateCount - FilteredDebugCount);
    public long LastBuildMs { get; set; }
    public long LastRenderMs { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.MinValue;

    public static RuntimeDiagnosticsSnapshot Empty { get; } = new RuntimeDiagnosticsSnapshot();
}
