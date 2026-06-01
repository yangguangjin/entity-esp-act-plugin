using System.Collections.Generic;

namespace EntityEspActPlugin.Core.Models;

public sealed class EspConfig
{
    public EnvironmentPaths Paths { get; set; } = new();
    public OverlayStyleConfig Style { get; set; } = new OverlayStyleConfig();

    public bool Enabled { get; set; } = true;
    public bool ShowUntargetable { get; set; } = true;
    public bool FilterSelf { get; set; } = true;
    public bool FilterPartyPlayers { get; set; } = true;
    public bool FilterPartyOwned { get; set; } = true;

    public float MaxDistance { get; set; } = 100f;
    public int MaxDisplayedEntities { get; set; } = 30;
    public int EntityScanHz { get; set; } = 45;
    public int RenderFps { get; set; } = 90;
    public bool UseActLogActivityLifetime { get; set; }
    public float EntityActivityLifetimeSeconds { get; set; } = 15f;
    public bool PreserveShortLivedEntitiesAfterTerminal { get; set; } = true;
    public float ShortLivedEntityMaxAgeSeconds { get; set; } = 2f;
    public float ShortLivedEntityHoldSeconds { get; set; } = 10f;

    public float FontSize { get; set; } = 15f;
    public float Opacity { get; set; } = 0.001f;
    public bool ShowCastBar { get; set; } = true;
    public bool ShowActCastProgressBar { get; set; } = true;
    public bool ShowVfxMonitorPanel { get; set; }
    public float VfxMaxDistance { get; set; } = 100f;
    /// <summary>功能：active VFX 离开 Scene.World 后仍按日志式面板保留的默认秒数；默认 30 秒便于打本后回看。</summary>
    public float VfxDisplaySeconds { get; set; } = 30f;
    /// <summary>功能：active VFX 面板每次最多显示的实时/历史条数。</summary>
    public int VfxMaxRows { get; set; } = 12;

    /// <summary>功能：Scene.World active VFX 内存采样频率；与 RenderFps 解耦，避免按 UI 帧率执行重型内存遍历。</summary>
    public int VfxSampleHz { get; set; } = 10;

    /// <summary>功能：短命 active VFX 判定阈值 N；从首次到末次仍在 Scene.World 的时间小于等于该秒数时，可在普通显示窗口后续显。</summary>
    public float VfxShortLivedMaxAgeSeconds { get; set; } = 2f;

    /// <summary>功能：短命 active VFX 在普通显示窗口结束后的额外续显秒数 M；默认 15 秒用于看清一闪而过的效果，设置为 0 可关闭短命续显。</summary>
    public float VfxShortLivedHoldSeconds { get; set; } = 15f;

    public VfxAnchorMode VfxAnchorMode { get; set; } = VfxAnchorMode.Self;
    public uint VfxAnchorEntityId { get; set; }
    public bool VfxShowPathScanFallback { get; set; } = true;
    public bool ShowRecentVfxPanel { get; set; }
    public float RecentVfxWindowSeconds { get; set; } = 30f;
    public float RecentVfxDisplaySeconds { get; set; } = 12f;
    public int RecentVfxMaxLines { get; set; } = 12;
    public bool ShowFilteredEntitiesForDebug { get; set; }
    public bool DebugOverlayStyle { get; set; }
    public LabelFieldConfig LabelFields { get; set; } = new LabelFieldConfig();
    public bool ShowRelatedActLogs { get; set; } = true;
    public bool ShowRelatedActLogsNearEntity { get; set; } = true;
    public bool ShowRelatedActLogPanel { get; set; } = false;
    public float RelatedActLogPanelSeconds { get; set; } = 6f;
    public int RelatedActLogPanelMaxLines { get; set; } = 8;
    public bool FilterPlayerAndPartyLog14 { get; set; } = true;
    public bool FilterPlayerAndPartyLog1A { get; set; } = true;
    public float RelatedActLogSeconds { get; set; } = 6f;
    public int RelatedActLogMaxLinesPerEntity { get; set; } = 8;
    public RelatedActLogFilterConfig RelatedActLogFilters { get; set; } = new RelatedActLogFilterConfig();
    public RelatedActLogSimplifyConfig RelatedActLogSimplify { get; set; } = new RelatedActLogSimplifyConfig();
    public DataSourceMode DataSourceMode { get; set; } = DataSourceMode.Real;
    public List<uint> EntityIdBlacklist { get; set; } = new List<uint>();
    public List<uint> BNpcBlacklist { get; set; } = new List<uint> { 13961, 10489, 1008, 10487, 7245, 10490, 13498, 16926, 13505, 13507, 13506, 6982, 952 };
    public List<uint> BNpcNameBlacklist { get; set; } = new List<uint>();
    public List<uint> RelatedActLogCasterEntityIdBlacklist { get; set; } = new List<uint>();
    public List<uint> RelatedActLogCasterBNpcBlacklist { get; set; } = new List<uint> { 13961, 10489, 1008, 10487, 7245, 10490, 13498, 16926, 13505, 13507, 13506, 6982, 952 };
    public List<uint> RelatedActLogCasterBNpcNameBlacklist { get; set; } = new List<uint>();
}
