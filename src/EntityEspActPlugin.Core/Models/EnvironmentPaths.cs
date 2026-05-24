using System.IO;

namespace EntityEspActPlugin.Core.Models;

public sealed class EnvironmentPaths
{
    public string ActDirectory { get; set; } = string.Empty;
    public string FfxivDirectory { get; set; } = string.Empty;
    public string FfxivDx11Path => Path.Combine(FfxivDirectory ?? string.Empty, "game", "ffxiv_dx11.exe");
    public string FfxivGameVersionPath => Path.Combine(FfxivDirectory ?? string.Empty, "game", "ffxivgame.ver");

    public string TryReadFfxivGameVersion()
    {
        return File.Exists(FfxivGameVersionPath)
            ? File.ReadAllText(FfxivGameVersionPath).Trim()
            : string.Empty;
    }
}
