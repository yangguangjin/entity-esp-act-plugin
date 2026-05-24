using System.Diagnostics;
using System.IO;
using System.Text;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public static class DiagnosticsService
{
    public static string BuildReport(EnvironmentPaths paths, RuntimeDiagnosticsSnapshot? runtime = null)
    {
        var builder = new StringBuilder();
        AppendPathStatus(builder, "ACT directory", paths.ActDirectory, Directory.Exists(paths.ActDirectory));
        AppendPathStatus(builder, "Advanced Combat Tracker.exe", Path.Combine(paths.ActDirectory, "Advanced Combat Tracker.exe"), File.Exists(Path.Combine(paths.ActDirectory, "Advanced Combat Tracker.exe")));
        AppendPathStatus(builder, "FF14 directory", paths.FfxivDirectory, Directory.Exists(paths.FfxivDirectory));
        AppendPathStatus(builder, "ffxiv_dx11.exe", paths.FfxivDx11Path, File.Exists(paths.FfxivDx11Path));

        var version = paths.TryReadFfxivGameVersion();
        builder.AppendLine("Game version: " + (string.IsNullOrWhiteSpace(version) ? "unknown" : version));
        builder.AppendLine("FF14 process: " + (IsProcessRunning("ffxiv_dx11") ? "running" : "not found"));
        AppendRuntimeStatus(builder, runtime);
        return builder.ToString();
    }

    private static void AppendRuntimeStatus(StringBuilder builder, RuntimeDiagnosticsSnapshot? runtime)
    {
        builder.AppendLine();
        builder.AppendLine("Runtime diagnostics:");
        if (runtime == null || runtime.LastUpdatedAt == System.DateTime.MinValue)
        {
            builder.AppendLine("  Overlay runtime: no samples yet");
            return;
        }

        builder.AppendLine("  Entity source: " + runtime.EntitySourceName);
        builder.AppendLine("  Camera source: " + runtime.CameraSourceName);
        builder.AppendLine("  Camera valid: " + (runtime.CameraValid ? "yes" : "no"));
        builder.AppendLine("  Viewport: " + runtime.ViewportWidth + "x" + runtime.ViewportHeight);
        builder.AppendLine("  Raw entities: " + runtime.RawEntityCount);
        builder.AppendLine("  Entity source ready: " + (runtime.EntitySourceReady ? "yes" : "no"));
        builder.AppendLine("  Camera source ready: " + (runtime.CameraSourceReady ? "yes" : "no"));
        if (!string.IsNullOrWhiteSpace(runtime.SourceStatus))
        {
            builder.AppendLine("  Source status: " + runtime.SourceStatus);
        }

        builder.AppendLine("  Visible states: " + runtime.VisibleStateCount);
        builder.AppendLine("  Normal visible: " + runtime.NormalVisibleCount);
        builder.AppendLine("  Filtered debug: " + runtime.FilteredDebugCount);
        AppendProcessMemoryStatus(builder, runtime.ProcessMemory);
        AppendPatternScans(builder, runtime.PatternScans);
        builder.AppendLine("  Last build: " + runtime.LastBuildMs + " ms");
        builder.AppendLine("  Last render: " + runtime.LastRenderMs + " ms");
        builder.AppendLine("  Last sample: " + runtime.LastUpdatedAt.ToString("HH:mm:ss"));
    }

    private static void AppendProcessMemoryStatus(StringBuilder builder, ProcessMemoryStatus? status)
    {
        if (status == null)
        {
            return;
        }

        builder.AppendLine("  Process found: " + (status.ProcessFound ? "yes" : "no"));
        if (status.ProcessId != 0)
        {
            builder.AppendLine("  Process id: " + status.ProcessId);
        }

        builder.AppendLine("  Module found: " + (status.ModuleFound ? "yes" : "no"));
        if (status.ModuleFound)
        {
            builder.AppendLine("  Module base: 0x" + status.ModuleBase.ToString("X"));
            builder.AppendLine("  Module size: " + status.ModuleSize);
            if (status.Sections.Count > 0)
            {
                builder.AppendLine("  Sections:");
                foreach (var section in status.Sections)
                {
                    builder.AppendLine("    " + section.Name + ": start=0x" + section.StartAddress.ToString("X") + " size=" + section.ScanSize);
                }
            }
        }

        builder.AppendLine("  Process handle: " + (status.HasHandle ? "yes" : "no"));
        if (!string.IsNullOrWhiteSpace(status.LastError))
        {
            builder.AppendLine("  Last memory error: " + status.LastError);
        }
    }

    private static void AppendPatternScans(StringBuilder builder, System.Collections.Generic.IReadOnlyList<PatternScanResult> scans)
    {
        if (scans == null || scans.Count == 0)
        {
            return;
        }

        builder.AppendLine("  Pattern scans:");
        foreach (var scan in scans)
        {
            builder.AppendLine("    " + scan.Name + ": hits=" + scan.HitCount + (scan.IsCached ? " (cached)" : string.Empty) + (scan.FirstHitAddress != 0 ? " first=0x" + scan.FirstHitAddress.ToString("X") : string.Empty) + (scan.ResolvedAddress != 0 ? " resolved=0x" + scan.ResolvedAddress.ToString("X") : string.Empty));
            builder.AppendLine("      section: " + (string.IsNullOrWhiteSpace(scan.SectionName) ? "<module>" : scan.SectionName));
            if (!string.IsNullOrWhiteSpace(scan.Pattern))
            {
                builder.AppendLine("      pattern: " + scan.Pattern);
            }

            if (!string.IsNullOrWhiteSpace(scan.Error))
            {
                builder.AppendLine("      error: " + scan.Error);
            }

            if (!string.IsNullOrWhiteSpace(scan.ResolveError))
            {
                builder.AppendLine("      resolve error: " + scan.ResolveError);
            }
        }
    }

    private static void AppendPathStatus(StringBuilder builder, string label, string path, bool ok)
    {
        builder.AppendLine(label + ": " + (ok ? "OK" : "missing"));
        builder.AppendLine("  " + path);
    }

    private static bool IsProcessRunning(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
