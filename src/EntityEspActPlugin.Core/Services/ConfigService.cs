using System.IO;
using System.Web.Script.Serialization;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class ConfigService
{
    private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

    public EspConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            var fresh = new EspConfig();
            fresh.Paths = EnvironmentPathResolver.Resolve(fresh.Paths);
            return fresh;
        }

        var json = File.ReadAllText(path);
        var config = _serializer.Deserialize<EspConfig>(json) ?? new EspConfig();
        MigrateLegacyRelatedActLogFilters(json, config);
        MigrateLegacyVfxMonitorSettings(json, config);
        ApplyMissingDefaults(config);
        config.Paths = EnvironmentPathResolver.Resolve(config.Paths);
        return config;
    }

    private static void ApplyMissingDefaults(EspConfig config)
    {
        if (config.RecentVfxMaxLines <= 0)
        {
            config.RecentVfxMaxLines = 12;
        }

        if (config.VfxMaxRows <= 0)
        {
            config.VfxMaxRows = config.RecentVfxMaxLines > 0 ? config.RecentVfxMaxLines : 12;
        }

        if (config.VfxDisplaySeconds <= 0)
        {
            // 功能：旧配置缺失新 VFX 字段时，优先沿用旧窗口；否则使用当前更易回看的 30 秒默认保留。
            config.VfxDisplaySeconds = config.RecentVfxWindowSeconds > 0 ? config.RecentVfxWindowSeconds : 30f;
        }

        if (config.VfxMaxDistance <= 0)
        {
            config.VfxMaxDistance = 100f;
        }

        if (config.VfxSampleHz <= 0)
        {
            // 功能：旧配置缺失采样频率时，使用较低默认值，避免按 RenderFps 重型采样。
            config.VfxSampleHz = VfxSnapshotSampler.DefaultSampleHz;
        }

        if (config.EntityActivityLifetimeSeconds <= 0)
        {
            config.EntityActivityLifetimeSeconds = 15f;
        }
    }

    private static void MigrateLegacyVfxMonitorSettings(string json, EspConfig config)
    {
        if (json.Contains("\"ShowRecentVfxPanel\"") && !json.Contains("\"ShowVfxMonitorPanel\""))
        {
            config.ShowVfxMonitorPanel = config.ShowRecentVfxPanel;
        }

        if (json.Contains("\"RecentVfxWindowSeconds\"") && !json.Contains("\"VfxDisplaySeconds\""))
        {
            config.VfxDisplaySeconds = config.RecentVfxWindowSeconds;
        }
        else if (json.Contains("\"RecentVfxDisplaySeconds\"") && !json.Contains("\"VfxDisplaySeconds\""))
        {
            config.VfxDisplaySeconds = config.RecentVfxDisplaySeconds;
        }

        if (json.Contains("\"RecentVfxMaxLines\"") && !json.Contains("\"VfxMaxRows\""))
        {
            config.VfxMaxRows = config.RecentVfxMaxLines;
        }
    }

    private static void MigrateLegacyRelatedActLogFilters(string json, EspConfig config)
    {
        var filters = config.RelatedActLogFilters;
        if (filters == null)
        {
            config.RelatedActLogFilters = new RelatedActLogFilterConfig();
            return;
        }

        if (json.Contains("\"Log14\"") || !json.Contains("\"RelatedActLogFilters\""))
        {
            return;
        }

        filters.Log03 = filters.Combatant;
        filters.Log04 = filters.Combatant;
        filters.Log14 = filters.Cast;
        filters.Log17 = filters.Cast;
        filters.Log15 = filters.Ability;
        filters.Log16 = filters.Ability;
        filters.Log1A = filters.Status;
        filters.Log1E = filters.Status;
        filters.Log26 = filters.Status;
        filters.Log2A = filters.Status;
        filters.Log1B = filters.Headmarker;
        filters.Log1C = filters.Markers;
        filters.Log1D = filters.Markers;
        filters.Log21 = filters.ActorControl;
        filters.Log111 = filters.ActorControl;
        filters.Log112 = filters.ActorControl;
        filters.Log23 = filters.Tether;
        filters.Log18 = filters.HpDeath;
        filters.Log19 = filters.HpDeath;
        filters.Log27 = filters.HpDeath;
        filters.Log105 = filters.ExtraPosition;
        filters.Log107 = filters.ExtraPosition;
        filters.Log108 = filters.ExtraPosition;
        filters.Log10F = filters.ExtraPosition;
        filters.Log110 = filters.ExtraPosition;
    }

    public void Save(string path, EspConfig config)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, _serializer.Serialize(config));
    }
}
