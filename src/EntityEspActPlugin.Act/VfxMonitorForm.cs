using System;
using System.Drawing;
using System.Windows.Forms;
using EntityEspActPlugin.Core.Models;
using EntityEspActPlugin.Core.Services;

namespace EntityEspActPlugin.Act;

public sealed class VfxMonitorForm : Form
{
    /// <summary>功能：提供当前 Scene.World active VFX 快照数据源。</summary>
    private readonly ActiveVfxMemoryService _service;

    /// <summary>功能：驱动面板按配置的 RenderFps 周期刷新。</summary>
    private readonly Timer _refreshTimer;

    /// <summary>功能：显示当前过滤条件、保留窗口和内存读取状态。</summary>
    private readonly Label _header;

    /// <summary>功能：承载复制/暂停按钮，避免用户必须在高频刷新的文本框里手动抢选中。</summary>
    private readonly FlowLayoutPanel _toolbar;

    /// <summary>功能：显示实时区和历史区文本内容，仍支持手动选中复制。</summary>
    private readonly TextBox _lines;

    /// <summary>功能：复制当前面板完整文本。</summary>
    private readonly Button _copyAllButton;

    /// <summary>功能：复制当前选中文本；没有选中时复制光标所在行。</summary>
    private readonly Button _copySelectionButton;

    /// <summary>功能：复制去重后的 .avfx 路径列表。</summary>
    private readonly Button _copyPathsButton;

    /// <summary>功能：暂停 UI 文本刷新，方便用户稳定选中和观察某一帧。</summary>
    private readonly CheckBox _pauseRefresh;

    private EspConfig _config;
    private VfxMonitorSnapshot _lastSnapshot = VfxMonitorSnapshot.Empty;
    private string _lastRenderedText = string.Empty;
    private string _lastHeaderText = string.Empty;

    public VfxMonitorForm(ActiveVfxMemoryService service, EspConfig config)
    {
        _service = service;
        _config = config;
        Text = "Entity ESP VFX Monitor";
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(32, 32, 980, 360);
        MinimumSize = new Size(560, 180);
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        BackColor = Color.FromArgb(24, 24, 24);
        ForeColor = Color.White;

        _header = new Label
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(8, 6, 8, 4),
            ForeColor = Color.FromArgb(225, 235, 255, 210),
            BackColor = Color.FromArgb(36, 36, 36),
        };
        _toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 32,
            Padding = new Padding(6, 3, 6, 2),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.FromArgb(28, 28, 28),
        };
        _copyAllButton = CreateToolbarButton("复制全部");
        _copyAllButton.Click += delegate { CopyToClipboard(GetFullPanelText(), "已复制全部 VFX 面板文本"); };
        _copySelectionButton = CreateToolbarButton("复制选中/当前行");
        _copySelectionButton.Click += delegate { CopyToClipboard(GetSelectionOrCurrentLineText(), "已复制选中或当前行"); };
        _copyPathsButton = CreateToolbarButton("复制路径列表");
        _copyPathsButton.Click += delegate { CopyToClipboard(VfxMonitorTextFormatter.FormatDistinctPaths(_lastSnapshot), "已复制去重 .avfx 路径"); };
        _pauseRefresh = new CheckBox
        {
            AutoSize = true,
            Text = "暂停刷新(便于选中复制)",
            ForeColor = Color.FromArgb(230, 210, 235, 255),
            BackColor = Color.FromArgb(28, 28, 28),
            Padding = new Padding(8, 4, 0, 0),
        };
        _pauseRefresh.CheckedChanged += delegate
        {
            if (_pauseRefresh.Checked)
            {
                ShowHeaderMessage("已暂停刷新；可选中文本后复制，取消勾选继续实时更新");
                return;
            }

            RefreshSnapshot();
        };
        _toolbar.Controls.Add(_copyAllButton);
        _toolbar.Controls.Add(_copySelectionButton);
        _toolbar.Controls.Add(_copyPathsButton);
        _toolbar.Controls.Add(_pauseRefresh);

        _lines = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(16, 16, 16),
            ForeColor = Color.FromArgb(230, 180, 255, 235),
            Font = new Font(FontFamily.GenericMonospace, 9f),
        };

        Controls.Add(_lines);
        Controls.Add(_toolbar);
        Controls.Add(_header);

        _refreshTimer = new Timer { Interval = GetRefreshInterval(config) };
        _refreshTimer.Tick += delegate { RefreshSnapshot(); };
        _refreshTimer.Start();
        RefreshSnapshot();
    }

    public void ApplyConfig(EspConfig config)
    {
        _config = config;
        _refreshTimer.Interval = GetRefreshInterval(config);
        RefreshSnapshot();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>功能：创建 VFX 面板工具栏按钮并统一暗色样式。</summary>
    private static Button CreateToolbarButton(string text)
    {
        return new Button
        {
            Text = text,
            AutoSize = true,
            Height = 24,
            Margin = new Padding(2, 1, 4, 1),
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Standard,
        };
    }

    /// <summary>功能：把 RenderFps 转为 WinForms Timer 间隔；文字面板最多 30 FPS，避免高刷配置导致 TextBox 高频重排。</summary>
    private static int GetRefreshInterval(EspConfig config)
    {
        var fps = Math.Max(1, Math.Min(30, config.RenderFps));
        return Math.Max(1, 1000 / fps);
    }

    /// <summary>功能：刷新 VFX 面板，先显示实时存活区，再显示带分隔符的历史日志区。</summary>
    private void RefreshSnapshot()
    {
        if (_pauseRefresh.Checked)
        {
            ShowHeaderMessage("已暂停刷新；可选中文本后复制，取消勾选继续实时更新");
            return;
        }

        var maxRows = Math.Max(1, _config.VfxMaxRows);
        var snapshot = _service.SnapshotSectionsCached(_config);
        _lastSnapshot = snapshot;
        _lastHeaderText = "VFX 监控：实时存活 + 历史日志 | 距离≤" + Math.Max(0f, _config.VfxMaxDistance).ToString("0")
            + " | 历史保留" + Math.Max(0.1f, _config.VfxDisplaySeconds).ToString("0.0") + "s"
            + " | 短命≤" + Math.Max(0f, _config.VfxShortLivedMaxAgeSeconds).ToString("0.0") + "s+续" + Math.Max(0f, _config.VfxShortLivedHoldSeconds).ToString("0.0") + "s"
            + " | UI FPS≤30/配置" + Math.Max(1, Math.Min(165, _config.RenderFps))
            + " | 采样≈" + VfxSnapshotSampler.NormalizeSampleHz(_config) + "Hz"
            + " | 每区最大" + maxRows + "条 | " + _service.StatusText;
        _header.Text = _lastHeaderText;
        var text = VfxMonitorTextFormatter.Format(
            snapshot,
            DateTime.UtcNow,
            "当前模式不做 .avfx 全内存扫描；上方区块是当前仍在 Scene.World 的实时存活 VFX，下方区块是首次观测快照保留的历史日志。复制可用上方按钮；手动选择时建议勾选暂停刷新。");
        SetLinesText(text);
    }

    /// <summary>功能：更新文本框内容，并尽量保留用户当前选择范围，降低刷新对复制操作的干扰。</summary>
    private void SetLinesText(string text)
    {
        if (string.Equals(_lastRenderedText, text, StringComparison.Ordinal))
        {
            return;
        }

        var selectionStart = _lines.SelectionStart;
        var selectionLength = _lines.SelectionLength;
        _lastRenderedText = text;
        _lines.Text = text;
        if (!_lines.Focused || selectionStart > _lines.TextLength)
        {
            return;
        }

        _lines.Select(selectionStart, Math.Min(selectionLength, _lines.TextLength - selectionStart));
    }

    /// <summary>功能：复制面板当前可见文本，优先使用缓存内容，避免刷新瞬间读到空文本。</summary>
    private string GetFullPanelText()
    {
        return string.IsNullOrWhiteSpace(_lastRenderedText) ? _lines.Text : _lastRenderedText;
    }

    /// <summary>功能：获取当前选中文本；如果没有选中，则复制光标所在行，适合快速复制单条 VFX。</summary>
    private string GetSelectionOrCurrentLineText()
    {
        if (!string.IsNullOrWhiteSpace(_lines.SelectedText))
        {
            return _lines.SelectedText;
        }

        var lines = _lines.Lines;
        if (lines.Length == 0)
        {
            return string.Empty;
        }

        var lineIndex = Math.Max(0, Math.Min(lines.Length - 1, _lines.GetLineFromCharIndex(_lines.SelectionStart)));
        return lines[lineIndex];
    }

    /// <summary>功能：写入系统剪贴板，并在 header 上给出成功/失败反馈，避免弹窗遮挡游戏。</summary>
    private void CopyToClipboard(string text, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowHeaderMessage("没有可复制内容");
            return;
        }

        try
        {
            Clipboard.SetText(text);
            ShowHeaderMessage(successMessage + "，共 " + text.Length + " 字符");
        }
        catch (Exception ex)
        {
            ShowHeaderMessage("复制失败：" + ex.Message);
        }
    }

    /// <summary>功能：在状态 header 后追加临时操作反馈，不改变底层服务状态。</summary>
    private void ShowHeaderMessage(string message)
    {
        var baseText = string.IsNullOrWhiteSpace(_lastHeaderText) ? "VFX 监控" : _lastHeaderText;
        _header.Text = baseText + " | " + message;
    }
}
