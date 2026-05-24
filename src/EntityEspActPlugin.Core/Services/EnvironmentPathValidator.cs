using System.Collections.Generic;
using System.IO;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public static class EnvironmentPathValidator
{
    public static IReadOnlyList<string> Validate(EnvironmentPaths paths)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(paths.ActDirectory) || !Directory.Exists(paths.ActDirectory))
        {
            errors.Add("ACT directory does not exist");
        }

        var actExe = Path.Combine(paths.ActDirectory, "Advanced Combat Tracker.exe");
        if (!File.Exists(actExe))
        {
            errors.Add("Advanced Combat Tracker.exe was not found");
        }

        if (string.IsNullOrWhiteSpace(paths.FfxivDirectory) || !Directory.Exists(paths.FfxivDirectory))
        {
            errors.Add("FF14 directory does not exist");
        }

        if (!File.Exists(paths.FfxivDx11Path))
        {
            errors.Add("ffxiv_dx11.exe was not found");
        }

        return errors;
    }
}
