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

    public float FontSize { get; set; } = 15f;
    public float Opacity { get; set; } = 0.001f;
    public bool ShowCastBar { get; set; } = true;
    public bool ShowActCastProgressBar { get; set; } = true;
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
