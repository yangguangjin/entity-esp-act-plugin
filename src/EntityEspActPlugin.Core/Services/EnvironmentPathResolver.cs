using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public static class EnvironmentPathResolver
{
    private const string LegacyActMarker = "ACT.DieMoe";
    private const string LegacyFfxivMarker = @"WeGameApps\rail_apps\ffxiv(2000340)";

    public static EnvironmentPaths Resolve(EnvironmentPaths? configured = null)
    {
        var paths = configured ?? new EnvironmentPaths();
        var act = IsMissingOrLegacyAct(paths.ActDirectory) ? FindActDirectory() : paths.ActDirectory;
        var ffxiv = IsMissingOrLegacyFfxiv(paths.FfxivDirectory) ? FindFfxivDirectory() : paths.FfxivDirectory;
        return new EnvironmentPaths
        {
            ActDirectory = act,
            FfxivDirectory = ffxiv,
        };
    }

    public static string FindActDirectory()
    {
        var processPath = TryGetMainModulePath("Advanced Combat Tracker");
        if (File.Exists(processPath))
        {
            return Path.GetDirectoryName(processPath) ?? string.Empty;
        }

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        if (File.Exists(Path.Combine(baseDir, "Advanced Combat Tracker.exe")))
        {
            return baseDir;
        }

        var current = new DirectoryInfo(baseDir);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Advanced Combat Tracker.exe")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return string.Empty;
    }

    public static string FindFfxivDirectory()
    {
        var processPath = TryGetMainModulePath("ffxiv_dx11");
        if (File.Exists(processPath))
        {
            var gameDir = Path.GetDirectoryName(processPath);
            var installDir = Directory.GetParent(gameDir ?? string.Empty)?.FullName;
            if (!string.IsNullOrWhiteSpace(installDir))
            {
                return installDir ?? string.Empty;
            }
        }

        foreach (var candidate in EnumerateCommonFfxivDirectories())
        {
            if (File.Exists(Path.Combine(candidate, "game", "ffxiv_dx11.exe")))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static string TryGetMainModulePath(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName)
                .Select(process =>
                {
                    try
                    {
                        return process.MainModule?.FileName ?? string.Empty;
                    }
                    catch
                    {
                        return string.Empty;
                    }
                })
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string[] EnumerateCommonFfxivDirectories()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new[]
        {
            Path.Combine(programFiles, "SquareEnix", "FINAL FANTASY XIV - A Realm Reborn"),
            Path.Combine(programFilesX86, "SquareEnix", "FINAL FANTASY XIV - A Realm Reborn"),
            Path.Combine(programFiles, "WeGameApp", "WeGameApps", "rail_apps", "ffxiv(2000340)"),
            Path.Combine(localAppData, "Programs", "FFXIV", "FINAL FANTASY XIV - A Realm Reborn"),
        };
    }

    private static bool IsMissingOrLegacyAct(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            || value.IndexOf(LegacyActMarker, StringComparison.OrdinalIgnoreCase) >= 0
            || !File.Exists(Path.Combine(value, "Advanced Combat Tracker.exe"));
    }

    private static bool IsMissingOrLegacyFfxiv(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            || value.IndexOf(LegacyFfxivMarker, StringComparison.OrdinalIgnoreCase) >= 0
            || !File.Exists(Path.Combine(value, "game", "ffxiv_dx11.exe"));
    }
}
