using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security;
using System.Windows.Forms;
using Advanced_Combat_Tracker;
using EntityEspActPlugin.Core.Models;
using EntityEspActPlugin.Core.Services;

namespace EntityEspActPlugin.Act;

public sealed class PluginMain : IActPluginV1
{
    private readonly ConfigService _configService;
    private readonly string _configPath;
    private readonly RelatedActLogStore _relatedLogStore = new RelatedActLogStore();
    private readonly PartyListTracker _partyListTracker = new PartyListTracker();
    private readonly ActCastProgressStore _actCastProgressStore = new ActCastProgressStore();
    private readonly LiveVfxMonitorService _liveVfxMonitor = new LiveVfxMonitorService();
    private readonly EntityActivityTracker _entityActivityTracker = new EntityActivityTracker();

    private Label? _statusText;
    private TabPage? _pluginScreenSpace;
    private EspConfig _config;
    private TextBox? _diagnosticsTextBox;
    private TestOverlayForm? _testOverlay;

    public PluginMain()
    {
        _configService = new ConfigService();
        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Advanced Combat Tracker",
            "Config",
            "EntityEspPlugin.json");
        _config = new EspConfig();
    }

    public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
    {
        _statusText = pluginStatusText;
        _pluginScreenSpace = pluginScreenSpace;
        _config = _configService.Load(_configPath);
        pluginScreenSpace.Text = "Entity ESP";
        pluginScreenSpace.Controls.Add(BuildConfigPanel());
        ActGlobals.oFormActMain.OnLogLineRead += OnLogLineRead;
        ApplyVfxMonitorState();
        SetStatus("Entity ESP loaded; " + GetVfxMonitorStatusText());
    }

    public void DeInitPlugin()
    {
        ActGlobals.oFormActMain.OnLogLineRead -= OnLogLineRead;
        _testOverlay?.Close();
        _testOverlay = null;
        _liveVfxMonitor.Stop();
        _configService.Save(_configPath, _config);
        SetStatus("Entity ESP unloaded");
    }

    private void OnLogLineRead(bool isImport, LogLineEventArgs logInfo)
    {
        if (isImport || logInfo == null)
        {
            return;
        }

        var line = logInfo.logLine ?? string.Empty;
        _partyListTracker.ObserveLogLine(line);
        _actCastProgressStore.ObserveLogLine(line);
        _entityActivityTracker.ObserveLogLine(
            line,
            _config.EntityActivityLifetimeSeconds,
            _config.PreserveShortLivedEntitiesAfterTerminal,
            _config.ShortLivedEntityMaxAgeSeconds,
            _config.ShortLivedEntityHoldSeconds);
        if (!_config.ShowRelatedActLogs)
        {
            return;
        }

        _relatedLogStore.AddLine(
            line,
            _config.RelatedActLogFilters,
            _config.FilterPlayerAndPartyLog14,
            _config.FilterPlayerAndPartyLog1A,
            _config.RelatedActLogSimplify,
            _config.RelatedActLogCasterEntityIdBlacklist,
            _config.RelatedActLogCasterBNpcBlacklist,
            _config.RelatedActLogCasterBNpcNameBlacklist);
    }

    private Control BuildConfigPanel()
    {
        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            FixedPanel = FixedPanel.None,
        };
        splitContainer.SplitterDistance = 520;

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        splitContainer.Panel1.Controls.Add(panel);

        var enabled = CreateCheckbox("启用 overlay", _config.Enabled, value => _config.Enabled = value);
        var dataSourceInput = CreateDataSourceInput(_config.DataSourceMode, value => _config.DataSourceMode = value);
        var signatureHint = new Label
        {
            AutoSize = true,
            Text = "实体/相机签名已内置；游戏更新后请把 Diagnostics 发给维护者，逆向定位请使用 EntityEspProbe。",
        };
        var authorContact = new Label
        {
            AutoSize = true,
            Text = "作者联系方式：QQ1115284886  光进不出",
        };
        var showCastBar = CreateCheckbox("显示内存读条条形", _config.ShowCastBar, value => _config.ShowCastBar = value);
        var showActCastProgressBar = CreateCheckbox("显示 14 日志读条进度条", _config.ShowActCastProgressBar, value => _config.ShowActCastProgressBar = value);
        var showRecentVfxPanel = CreateCheckbox("启用 VFX 监控与左上角列表", _config.ShowRecentVfxPanel, value =>
        {
            _config.ShowRecentVfxPanel = value;
            ApplyVfxMonitorState();
            RestartTestOverlayIfOpen();
            SetStatus("VFX switch changed; " + GetVfxMonitorStatusText());
        });
        var recentVfxWindowInput = CreateTextInput("VFX 保留窗口秒数(只影响显示)", _config.RecentVfxWindowSeconds.ToString("0"), value =>
        {
            if (float.TryParse(value, out var parsed))
            {
                _config.RecentVfxWindowSeconds = OverlayStyleService.Clamp(parsed, 1f, 300f);
            }
        });
        var recentVfxDisplayInput = CreateTextInput("VFX NEW高亮秒数(不影响扫描)", _config.RecentVfxDisplaySeconds.ToString("0"), value =>
        {
            if (float.TryParse(value, out var parsed))
            {
                _config.RecentVfxDisplaySeconds = OverlayStyleService.Clamp(parsed, 1f, 120f);
            }
        });
        var recentVfxMaxLinesInput = CreateTextInput("VFX 最大显示行数", _config.RecentVfxMaxLines.ToString(), value =>
        {
            if (int.TryParse(value, out var parsed))
            {
                _config.RecentVfxMaxLines = (int)OverlayStyleService.Clamp(parsed, 1f, 30f);
            }
        });
        var copyTrnVfxSnippet = new Button { Text = "复制最近 VFX 的 TRN 复现片段", AutoSize = true };
        copyTrnVfxSnippet.Click += delegate { CopyLatestVfxTrnSnippet(); };
        var showUntargetable = CreateCheckbox("显示不可选中实体", _config.ShowUntargetable, value => _config.ShowUntargetable = value);
        var filterSelf = CreateCheckbox("过滤自己", _config.FilterSelf, value => _config.FilterSelf = value);
        var filterParty = CreateCheckbox("过滤队友玩家", _config.FilterPartyPlayers, value => _config.FilterPartyPlayers = value);
        var filterOwned = CreateCheckbox("过滤自己/队友随从实体(跟随队友开关)", _config.FilterPartyOwned, value => _config.FilterPartyOwned = value);
        var showFilteredDebug = CreateCheckbox("调试显示已过滤实体及原因", _config.ShowFilteredEntitiesForDebug, value => _config.ShowFilteredEntitiesForDebug = value);
        var debugStyle = CreateCheckbox("调试高亮样式", _config.DebugOverlayStyle, value => _config.DebugOverlayStyle = value);
        _config.LabelFields ??= new LabelFieldConfig();
        var showLabelEntityId = CreateCheckbox("标签:EntityId", _config.LabelFields.EntityId, value => _config.LabelFields.EntityId = value);
        var showLabelKind = CreateCheckbox("标签:Kind", _config.LabelFields.Kind, value => _config.LabelFields.Kind = value);
        var showLabelDistance = CreateCheckbox("标签:距离", _config.LabelFields.Distance, value => _config.LabelFields.Distance = value);
        var showLabelHp = CreateCheckbox("标签:HP", _config.LabelFields.Hp, value => _config.LabelFields.Hp = value);
        var showLabelPosition = CreateCheckbox("标签:坐标", _config.LabelFields.Position, value => _config.LabelFields.Position = value);
        var showLabelBNpcId = CreateCheckbox("标签:BNpcId", _config.LabelFields.BNpcId, value => _config.LabelFields.BNpcId = value);
        var showLabelNameId = CreateCheckbox("标签:NameId", _config.LabelFields.BNpcNameId, value => _config.LabelFields.BNpcNameId = value);
        var showLabelBNpcName = CreateCheckbox("标签:BNpcName", _config.LabelFields.BNpcName, value => _config.LabelFields.BNpcName = value);
        var showLabelEObjNameId = CreateCheckbox("标签:EObjNameId", _config.LabelFields.EObjNameId, value => _config.LabelFields.EObjNameId = value);
        _config.LabelFields.Cast = false;
        _config.LabelFields.CastTargetId = false;
        var showRelatedLogs = CreateCheckbox("启用实体相关 ACT 日志采集", _config.ShowRelatedActLogs, value => _config.ShowRelatedActLogs = value);
        var showRelatedLogsNearEntity = CreateCheckbox("实体旁显示 ACT 日志", _config.ShowRelatedActLogsNearEntity, value => _config.ShowRelatedActLogsNearEntity = value);
        var showRelatedLogPanel = CreateCheckbox("右侧固定日志面板(录屏分析)", _config.ShowRelatedActLogPanel, value => _config.ShowRelatedActLogPanel = value);
        var useActLogActivityLifetime = CreateCheckbox("按 ACT 日志活动隐藏静止实体", _config.UseActLogActivityLifetime, value => _config.UseActLogActivityLifetime = value);
        var entityActivityLifetimeInput = CreateTextInput("实体 ACT 活动续期秒数", _config.EntityActivityLifetimeSeconds.ToString("0.0"), value =>
        {
            if (float.TryParse(value, out var parsed))
            {
                _config.EntityActivityLifetimeSeconds = OverlayStyleService.Clamp(parsed, 1f, 120f);
            }
        });
        var preserveShortLivedEntities = CreateCheckbox("短命实体删除/死亡后续显", _config.PreserveShortLivedEntitiesAfterTerminal, value => _config.PreserveShortLivedEntitiesAfterTerminal = value);
        var shortLivedEntityMaxAgeInput = CreateTextInput("短命判定秒数 N", _config.ShortLivedEntityMaxAgeSeconds.ToString("0.0"), value =>
        {
            if (float.TryParse(value, out var parsed))
            {
                _config.ShortLivedEntityMaxAgeSeconds = OverlayStyleService.Clamp(parsed, 0.1f, 30f);
            }
        });
        var shortLivedEntityHoldInput = CreateTextInput("短命后续显秒数 M", _config.ShortLivedEntityHoldSeconds.ToString("0.0"), value =>
        {
            if (float.TryParse(value, out var parsed))
            {
                _config.ShortLivedEntityHoldSeconds = OverlayStyleService.Clamp(parsed, 0.5f, 60f);
            }
        });
        var entityActivityLifetimeHint = new Label
        {
            AutoSize = true,
            Text = "实体生命：开启后，新看到的实体先显示活动续期秒数；03/105 Add、14/15/16/1A/27、10F/110 坐标等 ACT 日志续期；死亡、HP=0、04/105 Remove 或超时会隐藏。短命后续显开启后，若实体从出生/创建到删除/死亡小于 N 秒，会用最后一次实体快照继续显示 M 秒，方便回看瞬删机制物。此逻辑在日志类型显示过滤之前执行。",
        };
        var relatedLogPanelSecondsInput = CreateTextInput("右侧日志保留秒数", _config.RelatedActLogPanelSeconds.ToString("0.0"), value =>
        {
            if (float.TryParse(value, out var parsed))
            {
                _config.RelatedActLogPanelSeconds = OverlayStyleService.Clamp(parsed, 0.5f, 60f);
            }
        });
        var relatedLogPanelMaxLinesInput = CreateTextInput("右侧日志最大条数", _config.RelatedActLogPanelMaxLines.ToString(), value =>
        {
            if (int.TryParse(value, out var parsed))
            {
                _config.RelatedActLogPanelMaxLines = Math.Max(1, Math.Min(80, parsed));
            }
        });
        var filterPlayerAndPartyLog14 = CreateCheckbox("过滤自己/队友 14 读条日志", _config.FilterPlayerAndPartyLog14, value => _config.FilterPlayerAndPartyLog14 = value);
        var filterPlayerAndPartyLog1A = CreateCheckbox("过滤自己/队友 1A 状态日志", _config.FilterPlayerAndPartyLog1A, value => _config.FilterPlayerAndPartyLog1A = value);
        var relatedLogSecondsInput = CreateTextInput("实体旁日志保留秒数", _config.RelatedActLogSeconds.ToString("0.0"), value =>
        {
            if (float.TryParse(value, out var parsed))
            {
                _config.RelatedActLogSeconds = OverlayStyleService.Clamp(parsed, 0.5f, 30f);
            }
        });
        _config.RelatedActLogFilters ??= new RelatedActLogFilterConfig();
        _config.RelatedActLogSimplify ??= new RelatedActLogSimplifyConfig();
        var relatedLogMaxLinesInput = CreateTextInput("实体旁日志最大条数", _config.RelatedActLogMaxLinesPerEntity.ToString(), value =>
        {
            if (int.TryParse(value, out var parsed))
            {
                _config.RelatedActLogMaxLinesPerEntity = Math.Max(1, Math.Min(20, parsed));
            }
        });
        var relatedLogControls = new[]
        {
            CreateRelatedLogControl("日志:03 生成 AddCombatant", _config.RelatedActLogFilters.Log03, value => _config.RelatedActLogFilters.Log03 = value, _config.RelatedActLogSimplify.Log03, value => _config.RelatedActLogSimplify.Log03 = value),
            CreateRelatedLogControl("日志:04 消失 RemoveCombatant", _config.RelatedActLogFilters.Log04, value => _config.RelatedActLogFilters.Log04 = value, _config.RelatedActLogSimplify.Log04, value => _config.RelatedActLogSimplify.Log04 = value),
            CreateRelatedLog14Control("日志:14 读条开始 StartsCasting", _config.RelatedActLogFilters.Log14, value => _config.RelatedActLogFilters.Log14 = value),
            CreateRelatedLogControl("日志:17 读条取消/中断", _config.RelatedActLogFilters.Log17, value => _config.RelatedActLogFilters.Log17 = value, _config.RelatedActLogSimplify.Log17, value => _config.RelatedActLogSimplify.Log17 = value),
            CreateRelatedLogControl("日志:15 命中 Ability", _config.RelatedActLogFilters.Log15, value => _config.RelatedActLogFilters.Log15 = value, _config.RelatedActLogSimplify.Log15, value => _config.RelatedActLogSimplify.Log15 = value),
            CreateRelatedLogControl("日志:16 AOE 命中", _config.RelatedActLogFilters.Log16, value => _config.RelatedActLogFilters.Log16 = value, _config.RelatedActLogSimplify.Log16, value => _config.RelatedActLogSimplify.Log16 = value),
            CreateRelatedLog1AControl("日志:1A 状态获得 StatusAdd", _config.RelatedActLogFilters.Log1A, value => _config.RelatedActLogFilters.Log1A = value),
            CreateRelatedLogControl("日志:1E 状态移除 StatusRemove", _config.RelatedActLogFilters.Log1E, value => _config.RelatedActLogFilters.Log1E = value, _config.RelatedActLogSimplify.Log1E, value => _config.RelatedActLogSimplify.Log1E = value),
            CreateRelatedLogControl("日志:26 状态列表 StatusList", _config.RelatedActLogFilters.Log26, value => _config.RelatedActLogFilters.Log26 = value, _config.RelatedActLogSimplify.Log26, value => _config.RelatedActLogSimplify.Log26 = value),
            CreateRelatedLogControl("日志:2A 状态列表扩展", _config.RelatedActLogFilters.Log2A, value => _config.RelatedActLogFilters.Log2A = value, _config.RelatedActLogSimplify.Log2A, value => _config.RelatedActLogSimplify.Log2A = value),
            CreateRelatedLogControl("日志:1B 头标 HeadMarker", _config.RelatedActLogFilters.Log1B, value => _config.RelatedActLogFilters.Log1B = value, _config.RelatedActLogSimplify.Log1B, value => _config.RelatedActLogSimplify.Log1B = value),
            CreateRelatedLogControl("日志:1C 场地标点 Waymark", _config.RelatedActLogFilters.Log1C, value => _config.RelatedActLogFilters.Log1C = value, _config.RelatedActLogSimplify.Log1C, value => _config.RelatedActLogSimplify.Log1C = value),
            CreateRelatedLogControl("日志:1D 目标标点 Sign", _config.RelatedActLogFilters.Log1D, value => _config.RelatedActLogFilters.Log1D = value, _config.RelatedActLogSimplify.Log1D, value => _config.RelatedActLogSimplify.Log1D = value),
            CreateRelatedLogControl("日志:21 ActorControl", _config.RelatedActLogFilters.Log21, value => _config.RelatedActLogFilters.Log21 = value, _config.RelatedActLogSimplify.Log21, value => _config.RelatedActLogSimplify.Log21 = value),
            CreateRelatedLogControl("日志:111 ActorControlSelf", _config.RelatedActLogFilters.Log111, value => _config.RelatedActLogFilters.Log111 = value, _config.RelatedActLogSimplify.Log111, value => _config.RelatedActLogSimplify.Log111 = value),
            CreateRelatedLogControl("日志:112 ActorControlTarget", _config.RelatedActLogFilters.Log112, value => _config.RelatedActLogFilters.Log112 = value, _config.RelatedActLogSimplify.Log112, value => _config.RelatedActLogSimplify.Log112 = value),
            CreateRelatedLogControl("日志:23 拉线 Tether", _config.RelatedActLogFilters.Log23, value => _config.RelatedActLogFilters.Log23 = value, _config.RelatedActLogSimplify.Log23, value => _config.RelatedActLogSimplify.Log23 = value),
            CreateRelatedLogControl("日志:18 DoT/HoT Tick", _config.RelatedActLogFilters.Log18, value => _config.RelatedActLogFilters.Log18 = value, _config.RelatedActLogSimplify.Log18, value => _config.RelatedActLogSimplify.Log18 = value),
            CreateRelatedLogControl("日志:19 死亡 Death", _config.RelatedActLogFilters.Log19, value => _config.RelatedActLogFilters.Log19 = value, _config.RelatedActLogSimplify.Log19, value => _config.RelatedActLogSimplify.Log19 = value),
            CreateRelatedLogControl("日志:27 HP 更新", _config.RelatedActLogFilters.Log27, value => _config.RelatedActLogFilters.Log27 = value, _config.RelatedActLogSimplify.Log27, value => _config.RelatedActLogSimplify.Log27 = value),
            CreateRelatedLogControl("日志:105 CombatantMemory", _config.RelatedActLogFilters.Log105, value => _config.RelatedActLogFilters.Log105 = value, _config.RelatedActLogSimplify.Log105, value => _config.RelatedActLogSimplify.Log105 = value),
            CreateRelatedLogControl("日志:107 精确读条坐标", _config.RelatedActLogFilters.Log107, value => _config.RelatedActLogFilters.Log107 = value, _config.RelatedActLogSimplify.Log107, value => _config.RelatedActLogSimplify.Log107 = value),
            CreateRelatedLogControl("日志:108 技能落点", _config.RelatedActLogFilters.Log108, value => _config.RelatedActLogFilters.Log108 = value, _config.RelatedActLogSimplify.Log108, value => _config.RelatedActLogSimplify.Log108 = value),
            CreateRelatedLogControl("日志:10F 瞬移/位置", _config.RelatedActLogFilters.Log10F, value => _config.RelatedActLogFilters.Log10F = value, _config.RelatedActLogSimplify.Log10F, value => _config.RelatedActLogSimplify.Log10F = value),
            CreateRelatedLogControl("日志:110 出生/位置扩展", _config.RelatedActLogFilters.Log110, value => _config.RelatedActLogFilters.Log110 = value, _config.RelatedActLogSimplify.Log110, value => _config.RelatedActLogSimplify.Log110 = value),
        };
        var maxDistanceInput = CreateTextInput("最大距离", _config.MaxDistance.ToString("0"), value =>
        {
            if (float.TryParse(value, out var parsed))
            {
                _config.MaxDistance = OverlayStyleService.Clamp(parsed, 0f, 500f);
            }
        });
        var maxDisplayedInput = CreateTextInput("最大显示实体数", _config.MaxDisplayedEntities.ToString(), value =>
        {
            if (int.TryParse(value, out var parsed))
            {
                _config.MaxDisplayedEntities = Math.Max(0, Math.Min(200, parsed));
            }
        });
        var scanHzInput = CreateTextInput("实体扫描 Hz", _config.EntityScanHz.ToString(), value =>
        {
            if (int.TryParse(value, out var parsed))
            {
                _config.EntityScanHz = Math.Max(1, Math.Min(60, parsed));
            }
        });
        var renderFpsInput = CreateTextInput("渲染 FPS", _config.RenderFps.ToString(), value =>
        {
            if (int.TryParse(value, out var parsed))
            {
                _config.RenderFps = Math.Max(15, Math.Min(144, parsed));
            }
        });
        var opacityInput = CreateTextInput("背景透明度 0-1", _config.Opacity.ToString("0.00"), value =>
        {
            if (float.TryParse(value, out var parsed))
            {
                _config.Opacity = OverlayStyleService.Clamp(parsed, 0f, 1f);
            }
        });
        var fontSizeInput = CreateTextInput("字号", _config.FontSize.ToString("0"), value =>
        {
            if (float.TryParse(value, out var parsed))
            {
                _config.FontSize = OverlayStyleService.Clamp(parsed, 8f, 24f);
            }
        });
        var textColorInput = CreateColorInput("文字颜色", _config.Style.TextColor, value => _config.Style.TextColor = value);
        var castingColorInput = CreateColorInput("读条条形颜色", _config.Style.CastingTextColor, value => _config.Style.CastingTextColor = value);
        var relatedLogColorInput = CreateColorInput("实体旁日志文字颜色", _config.Style.RelatedLogTextColor, value => _config.Style.RelatedLogTextColor = value);
        var relatedLogPanelColorInput = CreateColorInput("右侧日志面板文字颜色", _config.Style.RelatedLogPanelTextColor, value => _config.Style.RelatedLogPanelTextColor = value);
        var relatedLogPanelBackColorInput = CreateColorInput("右侧日志面板背景颜色", _config.Style.RelatedLogPanelBackgroundColor, value => _config.Style.RelatedLogPanelBackgroundColor = value);
        var markerColorInput = CreateColorInput("锚点十字颜色", _config.Style.MarkerColor, value => _config.Style.MarkerColor = value);
        var backgroundColorInput = CreateColorInput("背景块颜色", _config.Style.BackgroundColor, value => _config.Style.BackgroundColor = value);
        var entityIdBlacklistInput = CreateUIntListInput("EntityId 黑名单", _config.EntityIdBlacklist, values => _config.EntityIdBlacklist = values);
        var bnpcBlacklistInput = CreateUIntListInput("BNpc 黑名单", _config.BNpcBlacklist, values => _config.BNpcBlacklist = values);
        var bnpcNameBlacklistInput = CreateUIntListInput("BNpcName 黑名单", _config.BNpcNameBlacklist, values => _config.BNpcNameBlacklist = values);
        var relatedLogCasterBlacklistInput = CreateUIntListInput("读条施法者 EntityId 黑名单", _config.RelatedActLogCasterEntityIdBlacklist, values => _config.RelatedActLogCasterEntityIdBlacklist = values);
        var relatedLogCasterBNpcBlacklistInput = CreateUIntListInput("读条施法者 BNpc 黑名单", _config.RelatedActLogCasterBNpcBlacklist, values => _config.RelatedActLogCasterBNpcBlacklist = values);
        var relatedLogCasterBNpcNameBlacklistInput = CreateUIntListInput("读条施法者 BNpcName 黑名单", _config.RelatedActLogCasterBNpcNameBlacklist, values => _config.RelatedActLogCasterBNpcNameBlacklist = values);
        var listHint = new Label
        {
            AutoSize = true,
            Text = "名单支持十进制或 0x 十六进制；可用逗号、空格或换行分隔。BNpcName 指 BNpcNameId。",
        };
        var resetDefaults = new Button { Text = "一键还原默认配置", AutoSize = true };
        resetDefaults.Click += delegate
        {
            _config = new EspConfig();
            _configService.Save(_configPath, _config);
            RebuildConfigPanel();
            RestartTestOverlayIfOpen();
            SetStatus("Entity ESP config reset to defaults");
        };

        var toggleOverlay = new Button { Text = "切换测试 Overlay", AutoSize = true };
        toggleOverlay.Click += delegate { ToggleTestOverlay(); };

        var diagnostics = new Button { Text = "刷新 Diagnostics", AutoSize = true };
        diagnostics.Click += delegate { RefreshDiagnosticsWithoutCandidateScan(); };
        var diagnosticsHint = new Label
        {
            AutoSize = true,
            Text = "Diagnostics 只刷新下方环境检测信息：ACT/FF14 路径、进程状态、游戏窗口坐标。",
        };
        var filterReasonHint = new Label
        {
            AutoSize = true,
            Text = "调试显示已过滤实体及原因：临时画出被过滤对象，并在标签中标注 Filtered: party / distance / blacklist 等原因。",
        };

        var save = new Button { Text = "保存配置", AutoSize = true };
        save.Click += delegate
        {
            NormalizeRelatedLogSimplifyConfig();
            _configService.Save(_configPath, _config);
            RefreshDiagnosticsWithoutCandidateScan();
            RestartTestOverlayIfOpen();
            SetStatus("Entity ESP config saved and applied; " + _liveVfxMonitor.StatusText);
        };

        var diagnosticsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var diagnosticsHeader = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = "Diagnostics 日志：拖动上方分隔条可调整日志区域大小；刷新只更新环境、运行诊断和扫描信息。",
        };
        _diagnosticsTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font(FontFamily.GenericMonospace, 9f),
        };
        diagnosticsPanel.Controls.Add(_diagnosticsTextBox);
        diagnosticsPanel.Controls.Add(diagnosticsHeader);
        splitContainer.Panel2.Controls.Add(diagnosticsPanel);

        void AddSection(string title)
        {
            panel.Controls.Add(CreateSectionHeader(title));
        }

        AddSection("基础与数据源");
        panel.Controls.Add(enabled);
        panel.Controls.Add(dataSourceInput);
        panel.Controls.Add(signatureHint);
        panel.Controls.Add(authorContact);

        AddSection("实体显示与过滤");
        panel.Controls.Add(showUntargetable);
        panel.Controls.Add(filterSelf);
        panel.Controls.Add(filterParty);
        panel.Controls.Add(filterOwned);
        panel.Controls.Add(showFilteredDebug);
        panel.Controls.Add(filterReasonHint);
        panel.Controls.Add(debugStyle);

        AddSection("标签字段");
        panel.Controls.Add(showLabelEntityId);
        panel.Controls.Add(showLabelKind);
        panel.Controls.Add(showLabelDistance);
        panel.Controls.Add(showLabelHp);
        panel.Controls.Add(showLabelPosition);
        panel.Controls.Add(showLabelBNpcId);
        panel.Controls.Add(showLabelNameId);
        panel.Controls.Add(showLabelBNpcName);
        panel.Controls.Add(showLabelEObjNameId);

        AddSection("读条显示");
        panel.Controls.Add(showCastBar);
        panel.Controls.Add(showActCastProgressBar);

        AddSection("实体生命周期");
        panel.Controls.Add(useActLogActivityLifetime);
        panel.Controls.Add(entityActivityLifetimeInput);
        panel.Controls.Add(preserveShortLivedEntities);
        panel.Controls.Add(shortLivedEntityMaxAgeInput);
        panel.Controls.Add(shortLivedEntityHoldInput);
        panel.Controls.Add(entityActivityLifetimeHint);

        AddSection("ACT 日志采集与面板");
        panel.Controls.Add(showRelatedLogs);
        panel.Controls.Add(showRelatedLogsNearEntity);
        panel.Controls.Add(showRelatedLogPanel);
        panel.Controls.Add(relatedLogPanelSecondsInput);
        panel.Controls.Add(relatedLogPanelMaxLinesInput);
        panel.Controls.Add(filterPlayerAndPartyLog14);
        panel.Controls.Add(filterPlayerAndPartyLog1A);
        panel.Controls.Add(relatedLogSecondsInput);
        panel.Controls.Add(relatedLogMaxLinesInput);

        AddSection("ACT 日志类型与简化显示");
        foreach (var relatedLogControl in relatedLogControls)
        {
            panel.Controls.Add(relatedLogControl);
        }

        AddSection("VFX 监控");
        panel.Controls.Add(showRecentVfxPanel);
        panel.Controls.Add(recentVfxWindowInput);
        panel.Controls.Add(recentVfxDisplayInput);
        panel.Controls.Add(recentVfxMaxLinesInput);
        panel.Controls.Add(copyTrnVfxSnippet);
        panel.Controls.Add(new Label { AutoSize = true, Text = "VFX 提示：这些配置只控制面板保留/高亮/行数；扫描延迟取决于内存扫描耗时，已改为增量热点扫描。ActorVfx/Channeling 可用于快速复现，钢铁/月环/踩踏/点名 AOE 这类几何范围优先用 PictoACT Omen。" });

        AddSection("性能与数量限制");
        panel.Controls.Add(maxDistanceInput);
        panel.Controls.Add(maxDisplayedInput);
        panel.Controls.Add(scanHzInput);
        panel.Controls.Add(renderFpsInput);

        AddSection("样式与颜色");
        panel.Controls.Add(opacityInput);
        panel.Controls.Add(fontSizeInput);
        panel.Controls.Add(textColorInput);
        panel.Controls.Add(castingColorInput);
        panel.Controls.Add(relatedLogColorInput);
        panel.Controls.Add(relatedLogPanelColorInput);
        panel.Controls.Add(relatedLogPanelBackColorInput);
        panel.Controls.Add(markerColorInput);
        panel.Controls.Add(backgroundColorInput);

        AddSection("黑名单与读条噪声过滤");
        panel.Controls.Add(listHint);
        panel.Controls.Add(entityIdBlacklistInput);
        panel.Controls.Add(bnpcBlacklistInput);
        panel.Controls.Add(bnpcNameBlacklistInput);
        panel.Controls.Add(relatedLogCasterBlacklistInput);
        panel.Controls.Add(relatedLogCasterBNpcBlacklistInput);
        panel.Controls.Add(relatedLogCasterBNpcNameBlacklistInput);

        AddSection("操作与诊断");
        panel.Controls.Add(resetDefaults);
        panel.Controls.Add(toggleOverlay);
        panel.Controls.Add(diagnostics);
        panel.Controls.Add(save);
        panel.Controls.Add(diagnosticsHint);
        RefreshDiagnosticsWithoutCandidateScan();
        return splitContainer;
    }

    private void NormalizeRelatedLogSimplifyConfig()
    {
        _config.RelatedActLogSimplify ??= new RelatedActLogSimplifyConfig();
        if (_config.RelatedActLogSimplify.Log14 && _config.RelatedActLogSimplify.Log14Alt)
        {
            _config.RelatedActLogSimplify.Log14Alt = false;
        }

        if (_config.RelatedActLogSimplify.Log1A)
        {
            _config.RelatedActLogSimplify.Log1AAlt = false;
            _config.RelatedActLogSimplify.Log1AAlt2 = false;
        }
        else if (_config.RelatedActLogSimplify.Log1AAlt)
        {
            _config.RelatedActLogSimplify.Log1AAlt2 = false;
        }
    }

    private void ApplyVfxMonitorState()
    {
        if (_config.ShowRecentVfxPanel)
        {
            if (!_liveVfxMonitor.IsRunning)
            {
                _liveVfxMonitor.EnsureRunning();
            }
        }
        else if (_liveVfxMonitor.IsRunning)
        {
            _liveVfxMonitor.Stop();
        }
    }

    private string GetVfxMonitorStatusText()
    {
        if (!_config.ShowRecentVfxPanel)
        {
            return "VFX monitor: disabled by VFX switch";
        }

        return _liveVfxMonitor.StatusText;
    }

    private void CopyLatestVfxTrnSnippet()
    {
        var latest = _liveVfxMonitor.Snapshot(_config.RecentVfxWindowSeconds, _config.RecentVfxDisplaySeconds, 1).FirstOrDefault();
        if (latest == null || string.IsNullOrWhiteSpace(latest.Path))
        {
            SetStatus("No recent VFX path available for TRN snippet");
            MessageBox.Show("左上角 VFX 列表里还没有可复制的 .avfx。先进本/传送/触发机制，让监控抓到路径后再点。", "Entity ESP", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var text = BuildTrnVfxSnippet(latest.Path);
        Clipboard.SetText(text);
        SetStatus("Copied TRN VFX snippet for " + latest.Path);
        MessageBox.Show("已复制 TRN 复现片段。\n\n路径：" + latest.Path + "\n\n优先粘贴 ActorVfx/Channeling 参数测试；几何范围用 PictoACT Omen 更稳定。", "Entity ESP", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string BuildTrnVfxSnippet(string path)
    {
        var escapedPath = SecurityElement.Escape(path) ?? path;
        var sourceTarget = "${_me.Address}, ${_me.Address}, " + path;
        var xmlSourceTarget = "${_me.Address}, ${_me.Address}, " + escapedPath;
        var fileName = Path.GetFileNameWithoutExtension(path);
        var channelName = path.StartsWith("vfx/channeling/eff/", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : string.Empty;

        var channelBlock = string.IsNullOrWhiteSpace(channelName)
            ? ""
            : "\r\n\r\nChanneling 快捷参数(只适合 vfx/channeling/eff)：\r\nCallbackName: Channeling\r\nCallbackParam: ${_me.Address}, ${_me.Address}, " + channelName + "\r\n\r\nTRN XML:\r\n<Action ActionType=\"NamedCallback\" NamedCallbackName=\"Channeling\"\r\n        NamedCallbackParam=\"${_me.Address}, ${_me.Address}, " + SecurityElement.Escape(channelName) + "\" />";

        return "TRN VFX 快速复现\r\n"
            + "================\r\n"
            + "Path: " + path + "\r\n\r\n"
            + "ActorVfx 参数(截图里的用法，推荐先测这个)：\r\n"
            + "CallbackName: ActorVfx\r\n"
            + "CallbackParam: " + sourceTarget + "\r\n\r\n"
            + "TRN XML:\r\n"
            + "<Action ActionType=\"NamedCallback\" NamedCallbackName=\"ActorVfx\"\r\n"
            + "        NamedCallbackParam=\"" + xmlSourceTarget + "\" />"
            + channelBlock
            + "\r\n\r\nPictoACT StaticVfx 复现(适合需要指定场地坐标/持续时间时测试)：\r\n"
            + "CallbackName: PictoACT\r\n"
            + "CallbackParam:\r\n"
            + "StaticVfx: " + path + "\r\n"
            + "Tag: TestVfx_${_timestamp}\r\n"
            + "Pos: ${_me.x}, ${_me.y}, ${_me.z}\r\n"
            + "t: 5\r\n\r\n"
            + "说明：\r\n"
            + "- ActorVfx/Channeling 是复现游戏特效，不等于稳定画范围。\r\n"
            + "- vfx/channeling/eff 通常是连线/分摊/大圈这类频道特效，可用 Channeling 或 ActorVfx 测。\r\n"
            + "- 钢铁/月环/踩踏/点名 AOE 更像 Omen/几何范围，TRN 中优先用 PictoACT 的 Circle/Donut/Rect/Fan/Omen 画。\r\n";
    }

    private void RebuildConfigPanel()
    {
        if (_pluginScreenSpace == null)
        {
            return;
        }

        _pluginScreenSpace.Controls.Clear();
        _pluginScreenSpace.Controls.Add(BuildConfigPanel());
    }

    private void RefreshDiagnosticsWithoutCandidateScan()
    {
        if (_diagnosticsTextBox == null)
        {
            return;
        }

        var runtime = _testOverlay != null && !_testOverlay.IsDisposed ? CloneRuntimeDiagnostics(_testOverlay.RuntimeDiagnostics) : null;
        _diagnosticsTextBox.Text = DiagnosticsService.BuildReport(_config.Paths, runtime)
            + Environment.NewLine
            + Win32.GetFfxivClientRectText()
            + Environment.NewLine
            + _liveVfxMonitor.StatusText;
    }

    private static RuntimeDiagnosticsSnapshot CloneRuntimeDiagnostics(RuntimeDiagnosticsSnapshot source)
    {
        return new RuntimeDiagnosticsSnapshot
        {
            EntitySourceName = source.EntitySourceName,
            CameraSourceName = source.CameraSourceName,
            CameraValid = source.CameraValid,
            ViewportWidth = source.ViewportWidth,
            ViewportHeight = source.ViewportHeight,
            RawEntityCount = source.RawEntityCount,
            EntitySourceReady = source.EntitySourceReady,
            CameraSourceReady = source.CameraSourceReady,
            SourceStatus = source.SourceStatus,
            ProcessMemory = source.ProcessMemory,
            PatternScans = source.PatternScans,
            VisibleStateCount = source.VisibleStateCount,
            FilteredDebugCount = source.FilteredDebugCount,
            LastBuildMs = source.LastBuildMs,
            LastRenderMs = source.LastRenderMs,
            LastUpdatedAt = source.LastUpdatedAt,
        };
    }

    private void ToggleTestOverlay()
    {
        if (_testOverlay != null && !_testOverlay.IsDisposed)
        {
            _testOverlay.Close();
            _testOverlay = null;
            SetStatus("Entity ESP test overlay hidden");
            return;
        }

        ShowTestOverlay();
    }

    private void ShowTestOverlay()
    {
        _testOverlay = new TestOverlayForm(_config, _relatedLogStore, _partyListTracker, _actCastProgressStore, _liveVfxMonitor, _entityActivityTracker);
        _testOverlay.Show();
        SetStatus("Entity ESP test overlay shown");
    }

    private void RestartTestOverlayIfOpen()
    {
        if (_testOverlay == null || _testOverlay.IsDisposed)
        {
            return;
        }

        _testOverlay.Close();
        _testOverlay = null;
        ShowTestOverlay();
    }

    private static Control CreateSectionHeader(string title)
    {
        var header = new TableLayoutPanel
        {
            AutoSize = false,
            Width = 760,
            Height = 30,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 12, 0, 4),
            Padding = new Padding(0, 4, 0, 0),
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var label = new Label
        {
            AutoSize = true,
            Text = "◆ " + title,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 45, 45),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 4, 8, 0),
        };
        var line = new Label
        {
            AutoSize = false,
            BorderStyle = BorderStyle.Fixed3D,
            Dock = DockStyle.Fill,
            Height = 2,
            Margin = new Padding(4, 13, 0, 0),
        };

        header.Controls.Add(label, 0, 0);
        header.Controls.Add(line, 1, 0);
        return header;
    }

    private static CheckBox CreateCheckbox(string text, bool initialValue, Action<bool> onChanged)
    {
        var checkbox = new CheckBox { Text = text, Checked = initialValue, AutoSize = true };
        checkbox.CheckedChanged += delegate { onChanged(checkbox.Checked); };
        return checkbox;
    }

    private static void UncheckOthers(CheckBox checkedBox, params CheckBox[] others)
    {
        if (!checkedBox.Checked)
        {
            return;
        }

        foreach (var other in others)
        {
            if (other.Checked)
            {
                other.Checked = false;
            }
        }
    }

    private Control CreateRelatedLog14Control(string text, bool logInitialValue, Action<bool> onLogChanged)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var log = CreateCheckbox(text, logInitialValue, onLogChanged);
        var simplify = new CheckBox { Text = "简化显示", Checked = _config.RelatedActLogSimplify.Log14, AutoSize = true, Margin = new Padding(18, 3, 3, 3) };
        var simplifyAlt = new CheckBox { Text = "简化显示2", Checked = _config.RelatedActLogSimplify.Log14Alt, AutoSize = true, Margin = new Padding(18, 3, 3, 3) };
        simplify.CheckedChanged += delegate
        {
            UncheckOthers(simplify, simplifyAlt);
            _config.RelatedActLogSimplify.Log14 = simplify.Checked;
            _config.RelatedActLogSimplify.Log14Alt = simplifyAlt.Checked;
        };
        simplifyAlt.CheckedChanged += delegate
        {
            UncheckOthers(simplifyAlt, simplify);
            _config.RelatedActLogSimplify.Log14 = simplify.Checked;
            _config.RelatedActLogSimplify.Log14Alt = simplifyAlt.Checked;
        };
        panel.Controls.Add(log);
        panel.Controls.Add(simplify);
        panel.Controls.Add(simplifyAlt);
        return panel;
    }

    private Control CreateRelatedLog1AControl(string text, bool logInitialValue, Action<bool> onLogChanged)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var log = CreateCheckbox(text, logInitialValue, onLogChanged);
        var simplify = new CheckBox { Text = "简化显示", Checked = _config.RelatedActLogSimplify.Log1A, AutoSize = true, Margin = new Padding(18, 3, 3, 3) };
        var simplifyAlt = new CheckBox { Text = "简化显示2", Checked = _config.RelatedActLogSimplify.Log1AAlt, AutoSize = true, Margin = new Padding(18, 3, 3, 3) };
        var simplifyAlt2 = new CheckBox { Text = "简化显示3", Checked = _config.RelatedActLogSimplify.Log1AAlt2, AutoSize = true, Margin = new Padding(18, 3, 3, 3) };
        simplify.CheckedChanged += delegate
        {
            UncheckOthers(simplify, simplifyAlt, simplifyAlt2);
            UpdateLog1ASimplifyConfig(simplify, simplifyAlt, simplifyAlt2);
        };
        simplifyAlt.CheckedChanged += delegate
        {
            UncheckOthers(simplifyAlt, simplify, simplifyAlt2);
            UpdateLog1ASimplifyConfig(simplify, simplifyAlt, simplifyAlt2);
        };
        simplifyAlt2.CheckedChanged += delegate
        {
            UncheckOthers(simplifyAlt2, simplify, simplifyAlt);
            UpdateLog1ASimplifyConfig(simplify, simplifyAlt, simplifyAlt2);
        };
        panel.Controls.Add(log);
        panel.Controls.Add(simplify);
        panel.Controls.Add(simplifyAlt);
        panel.Controls.Add(simplifyAlt2);
        return panel;
    }

    private void UpdateLog1ASimplifyConfig(CheckBox simplify, CheckBox simplifyAlt, CheckBox simplifyAlt2)
    {
        _config.RelatedActLogSimplify.Log1A = simplify.Checked;
        _config.RelatedActLogSimplify.Log1AAlt = simplifyAlt.Checked;
        _config.RelatedActLogSimplify.Log1AAlt2 = simplifyAlt2.Checked;
    }

    private static Control CreateRelatedLogControl(string text, bool logInitialValue, Action<bool> onLogChanged, bool simplifyInitialValue, Action<bool> onSimplifyChanged)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var log = CreateCheckbox(text, logInitialValue, onLogChanged);
        var simplify = CreateCheckbox("简化显示", simplifyInitialValue, onSimplifyChanged);
        simplify.Margin = new Padding(18, 3, 3, 3);
        panel.Controls.Add(log);
        panel.Controls.Add(simplify);
        return panel;
    }

    private static Control CreateTextInput(string label, string initialValue, Action<string> onChanged)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var labelControl = new Label { Text = label, Width = 130, TextAlign = ContentAlignment.MiddleLeft };
        var textBox = new TextBox { Text = initialValue, Width = 95 };
        textBox.TextChanged += delegate { onChanged(textBox.Text); };
        panel.Controls.Add(labelControl);
        panel.Controls.Add(textBox);
        return panel;
    }

    private static Control CreateDataSourceInput(DataSourceMode initialValue, Action<DataSourceMode> onChanged)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var labelControl = new Label { Text = "数据源", Width = 130, TextAlign = ContentAlignment.MiddleLeft };
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        combo.Items.Add("Mock 模拟数据");
        combo.Items.Add("Real 真实源(诊断占位)");
        combo.SelectedIndex = initialValue == DataSourceMode.Real ? 1 : 0;
        combo.SelectedIndexChanged += delegate
        {
            onChanged(combo.SelectedIndex == 1 ? DataSourceMode.Real : DataSourceMode.Mock);
        };
        panel.Controls.Add(labelControl);
        panel.Controls.Add(combo);
        return panel;
    }

    private static Control CreateWideTextInput(string label, string initialValue, Action<string> onChanged)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var labelControl = new Label { Text = label, Width = 130, TextAlign = ContentAlignment.MiddleLeft };
        var textBox = new TextBox { Text = initialValue, Width = 520 };
        textBox.TextChanged += delegate { onChanged(textBox.Text); };
        panel.Controls.Add(labelControl);
        panel.Controls.Add(textBox);
        return panel;
    }

    private static Control CreateIntInput(string label, int initialValue, int min, int max, Action<int> onChanged)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var labelControl = new Label { Text = label, Width = 130, TextAlign = ContentAlignment.MiddleLeft };
        var textBox = new TextBox { Text = initialValue.ToString(), Width = 95 };
        textBox.TextChanged += delegate
        {
            if (int.TryParse(textBox.Text, out var parsed))
            {
                onChanged(Math.Max(min, Math.Min(max, parsed)));
            }
        };
        panel.Controls.Add(labelControl);
        panel.Controls.Add(textBox);
        return panel;
    }

    private static Control CreateColorInput(string label, string initialValue, Action<string> onChanged)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var labelControl = new Label { Text = label, Width = 130, TextAlign = ContentAlignment.MiddleLeft };
        var textBox = new TextBox { Text = initialValue, Width = 95 };
        var preview = new Panel { Width = 28, Height = 20, BorderStyle = BorderStyle.FixedSingle };
        var button = new Button { Text = "选择...", AutoSize = true };

        void ApplyPreview()
        {
            var parsed = OverlayStyleService.ParseHexColor(textBox.Text, 1f);
            preview.BackColor = Color.FromArgb(parsed.R, parsed.G, parsed.B);
        }

        textBox.TextChanged += delegate
        {
            onChanged(textBox.Text);
            ApplyPreview();
        };
        button.Click += delegate
        {
            using (var dialog = new ColorDialog { FullOpen = true })
            {
                var current = OverlayStyleService.ParseHexColor(textBox.Text, 1f);
                dialog.Color = Color.FromArgb(current.R, current.G, current.B);
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    textBox.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
                }
            }
        };

        ApplyPreview();
        panel.Controls.Add(labelControl);
        panel.Controls.Add(textBox);
        panel.Controls.Add(preview);
        panel.Controls.Add(button);
        return panel;
    }

    private static Control CreateUIntListInput(string label, System.Collections.Generic.IEnumerable<uint> initialValue, Action<System.Collections.Generic.List<uint>> onChanged)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var labelControl = new Label { Text = label, Width = 245, TextAlign = ContentAlignment.MiddleLeft };
        var textBox = new TextBox
        {
            Text = UIntListParser.Format(initialValue),
            Width = 440,
            Height = 46,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
        };
        textBox.TextChanged += delegate { onChanged(UIntListParser.Parse(textBox.Text)); };
        panel.Controls.Add(labelControl);
        panel.Controls.Add(textBox);
        return panel;
    }

    private void SetStatus(string text)
    {
        if (_statusText != null)
        {
            _statusText.Text = text;
        }
    }
}
