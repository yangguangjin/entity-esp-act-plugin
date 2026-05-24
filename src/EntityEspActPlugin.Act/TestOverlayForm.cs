using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using EntityEspActPlugin.Core.Models;
using EntityEspActPlugin.Core.Services;

namespace EntityEspActPlugin.Act;

public sealed class TestOverlayForm : Form
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private readonly Timer _followTimer;
    private readonly DisplayStateService _displayStateService;
    private readonly RelatedActLogStore? _relatedLogStore;
    private readonly PartyListTracker? _partyListTracker;
    private readonly ActCastProgressStore? _actCastProgressStore;
    private readonly NetworkPartyListTailer? _partyListTailer;
    private readonly LiveVfxMonitorService? _liveVfxMonitor;
    private readonly EspConfig _config;
    private IReadOnlyList<EntityDisplayState> _cachedStates = Array.Empty<EntityDisplayState>();
    private long _lastStateBuildTicks;
    private bool _isRendering;

    public TestOverlayForm(EspConfig config, RelatedActLogStore? relatedLogStore = null, PartyListTracker? partyListTracker = null, ActCastProgressStore? actCastProgressStore = null, LiveVfxMonitorService? liveVfxMonitor = null)
    {
        _config = config;
        _relatedLogStore = relatedLogStore;
        _partyListTracker = partyListTracker;
        _actCastProgressStore = actCastProgressStore;
        _liveVfxMonitor = liveVfxMonitor;
        _partyListTailer = partyListTracker == null ? null : new NetworkPartyListTailer(config.Paths, partyListTracker);
        _displayStateService = CreateDisplayStateService(config);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1280, 720);
        BackColor = Color.Black;
        DoubleBuffered = false;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);

        _followTimer = new Timer { Interval = GetRenderInterval(config.RenderFps) };
        _followTimer.Tick += delegate
        {
            FollowFfxivWindow();
            RenderLayeredOverlay();
        };
        _followTimer.Start();
        FollowFfxivWindow();
        RenderLayeredOverlay();
    }

    private static int GetRenderInterval(int fps)
    {
        var clamped = Math.Max(15, Math.Min(144, fps));
        return Math.Max(1, 1000 / clamped);
    }

    private static long GetScanIntervalTicks(int hz)
    {
        var clamped = Math.Max(1, Math.Min(60, hz));
        return Stopwatch.Frequency / clamped;
    }

    private static DisplayStateService CreateDisplayStateService(EspConfig config)
    {
        if (config.DataSourceMode == DataSourceMode.Real)
        {
            var memoryReader = new ProcessMemoryReader();
            return new DisplayStateService(
                new RealEntitySource(memoryReader),
                new RealCameraSource(memoryReader));
        }

        return new DisplayStateService(new MockEntitySource(), new MockCameraSource());
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _followTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // UpdateLayeredWindow owns all pixels; drawing through WM_PAINT can race the timer and leave trails.
    }

    public RuntimeDiagnosticsSnapshot RuntimeDiagnostics => _displayStateService.LastDiagnostics;

    private void RenderLayeredOverlay()
    {
        if (_isRendering || !IsHandleCreated || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        _isRendering = true;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using (var buffer = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(buffer))
                {
                    graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    graphics.Clear(Color.Transparent);
                    graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                    if (Win32.IsFfxivForeground())
                    {
                        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                        DrawHeader(graphics);
                        DrawRecentVfxPanel(graphics);
                        DrawDisplayStates(graphics);
                    }
                }

                ApplyLayeredBitmap(buffer);
            }
        }
        finally
        {
            stopwatch.Stop();
            _displayStateService.SetLastRenderMs(stopwatch.ElapsedMilliseconds);
            _isRendering = false;
        }
    }

    private void ApplyLayeredBitmap(Bitmap bitmap)
    {
        var screenDc = Win32.GetDC(IntPtr.Zero);
        var memDc = Win32.CreateCompatibleDC(screenDc);
        var hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
        var oldBitmap = IntPtr.Zero;
        try
        {
            oldBitmap = Win32.SelectObject(memDc, hBitmap);
            var topLeft = new Win32.WinPoint { X = Left, Y = Top };
            var size = new Win32.WinSize { Cx = bitmap.Width, Cy = bitmap.Height };
            var source = new Win32.WinPoint { X = 0, Y = 0 };
            var blend = new Win32.BlendFunction
            {
                BlendOp = Win32.AcSrcOver,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = Win32.AcSrcAlpha,
            };

            Win32.UpdateLayeredWindow(Handle, screenDc, ref topLeft, ref size, memDc, ref source, 0, ref blend, Win32.UlwAlpha);
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero)
            {
                Win32.SelectObject(memDc, oldBitmap);
            }

            Win32.DeleteObject(hBitmap);
            Win32.DeleteDC(memDc);
            Win32.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private void FollowFfxivWindow()
    {
        var nextBounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1280, 720);
        if (Win32.TryGetFfxivClientRect(out var rect))
        {
            nextBounds = rect;
        }

        if (Bounds != nextBounds)
        {
            Bounds = nextBounds;
            RenderLayeredOverlay();
        }
    }

    private void DrawHeader(Graphics graphics)
    {
        if (!_config.DebugOverlayStyle)
        {
            return;
        }

        var message = "Entity ESP Mock Camera - WorldToScreen";
        using (var font = new Font("Microsoft YaHei UI", 18f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var back = new SolidBrush(Color.FromArgb(130, 0, 0, 0)))
        using (var text = new SolidBrush(Color.FromArgb(225, 255, 255, 255)))
        using (var pen = new Pen(Color.FromArgb(220, 255, 204, 0), 2f))
        {
            var size = graphics.MeasureString(message, font);
            var rect = new RectangleF(18f, 18f, size.Width + 28f, size.Height + 18f);
            graphics.FillRectangle(back, rect);
            graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            graphics.DrawString(message, font, text, rect.X + 14f, rect.Y + 9f);
        }
    }

    private void DrawRecentVfxPanel(Graphics graphics)
    {
        if (!_config.ShowRecentVfxPanel)
        {
            return;
        }

        var entries = _liveVfxMonitor?.Snapshot(_config.RecentVfxWindowSeconds, _config.RecentVfxDisplaySeconds, Math.Max(1, _config.RecentVfxMaxLines)) ?? Array.Empty<LiveVfxEntry>();

        using (var font = new Font("Microsoft YaHei UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var titleFont = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var back = new SolidBrush(Color.FromArgb(82, 0, 0, 0)))
        using (var border = new Pen(Color.FromArgb(90, 130, 255, 220), 1f))
        using (var text = new SolidBrush(Color.FromArgb(230, 180, 255, 235)))
        using (var title = new SolidBrush(Color.FromArgb(235, 235, 255, 210)))
        using (var shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
        {
            var now = DateTime.UtcNow;
            var lines = entries.Count == 0
                ? BuildEmptyRecentVfxLines(_liveVfxMonitor?.StatusText ?? "VFX monitor: stopped")
                : entries
                    .Select(entry => (entry.IsNew ? "NEW " : "LIVE ") + Math.Max(0, (int)Math.Ceiling((now - entry.LastSeenAt).TotalSeconds)).ToString("00") + "s " + entry.Path)
                    .ToArray();
            var header = "Live VFX keep " + Math.Max(1f, _config.RecentVfxWindowSeconds).ToString("0") + "s / NEW " + Math.Max(1f, _config.RecentVfxDisplaySeconds).ToString("0") + "s / max " + Math.Max(1, _config.RecentVfxMaxLines);
            var contentWidth = graphics.MeasureString(header, titleFont).Width;
            var lineHeight = graphics.MeasureString("X", font).Height;
            foreach (var line in lines)
            {
                contentWidth = Math.Max(contentWidth, graphics.MeasureString(line, font).Width);
            }

            var maxWidth = Math.Min(ClientSize.Width * 0.42f, 760f);
            var rect = new RectangleF(96f, 16f, Math.Min(contentWidth + 14f, maxWidth), 22f + lines.Length * lineHeight + 8f);
            graphics.FillRectangle(back, rect);
            graphics.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
            DrawOutlinedText(graphics, header, titleFont, shadow, title, rect.X + 7f, rect.Y + 4f);

            var y = rect.Y + 22f;
            foreach (var line in lines)
            {
                var clipped = TrimToWidth(graphics, line, font, rect.Width - 14f);
                DrawOutlinedText(graphics, clipped, font, shadow, text, rect.X + 7f, y);
                y += lineHeight;
            }
        }
    }

    private static string[] BuildEmptyRecentVfxLines(string status)
    {
        return new[]
        {
            "no live .avfx yet",
            status,
        };
    }

    private void DrawDisplayStates(Graphics graphics)
    {
        var states = GetDisplayStatesForRender();
        var fontSize = _config.DebugOverlayStyle ? 13f : Math.Max(9f, _config.FontSize);
        var backgroundOpacity = _config.DebugOverlayStyle ? 0.88f : Math.Max(0f, Math.Min(1f, _config.Opacity));
        var labelDecorationOpacity = backgroundOpacity <= 0.001f ? 0f : 1f;
        var textOpacity = _config.DebugOverlayStyle ? 1f : 0.96f;
        var markerOpacity = _config.DebugOverlayStyle ? 1f : 0.92f;
        var castBarOpacity = _config.DebugOverlayStyle ? 1f : 0.9f;
        var markerRadius = _config.DebugOverlayStyle ? 5f : 4f;
        var crossHalf = _config.DebugOverlayStyle ? 10f : 0f;
        using (var font = new Font("Microsoft YaHei UI", fontSize, _config.DebugOverlayStyle ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
        using (var back = new SolidBrush(ToDrawingColor(OverlayStyleService.ParseHexColor(_config.Style.BackgroundColor, backgroundOpacity))))
        using (var normal = new SolidBrush(ToDrawingColor(OverlayStyleService.ParseHexColor(_config.Style.TextColor, textOpacity))))
        using (var castingText = new SolidBrush(ToDrawingColor(OverlayStyleService.ParseHexColor(_config.Style.TextColor, textOpacity))))
        using (var pinned = new SolidBrush(ToDrawingColor(OverlayStyleService.ParseHexColor(_config.Style.PinnedTextColor, textOpacity))))
        using (var filtered = new SolidBrush(Color.FromArgb(175, 170, 170, 170)))
        using (var castBar = new SolidBrush(ToDrawingColor(OverlayStyleService.ParseHexColor(_config.Style.CastingTextColor, castBarOpacity))))
        using (var relatedLogText = new SolidBrush(ToDrawingColor(OverlayStyleService.ParseHexColor(_config.Style.RelatedLogTextColor, textOpacity))))
        using (var castBarBack = new SolidBrush(Color.FromArgb((int)(80 * labelDecorationOpacity), 255, 255, 255)))
        using (var textShadow = new SolidBrush(Color.FromArgb((int)(145 * labelDecorationOpacity), 255, 255, 255)))
        using (var centerDot = new SolidBrush(Color.FromArgb(_config.DebugOverlayStyle ? 255 : 230, 255, 32, 32)))
        using (var line = new Pen(ToDrawingColor(OverlayStyleService.ParseHexColor(_config.Style.MarkerColor, markerOpacity * labelDecorationOpacity)), _config.DebugOverlayStyle ? 2f : 1f))
        {
            foreach (var state in states)
            {
                var point = SnapPoint(state.ScreenPosition);
                var brush = state.IsFilteredDebug ? filtered : PickBrush(state, normal, castingText, pinned);
                if (crossHalf > 0f)
                {
                    graphics.DrawLine(line, point.X - crossHalf, point.Y, point.X + crossHalf, point.Y);
                    graphics.DrawLine(line, point.X, point.Y - crossHalf, point.X, point.Y + crossHalf);
                }

                if (line.Color.A > 0)
                {
                    DrawDiamondMarker(graphics, line.Brush, point, markerRadius);
                }

                DrawCenterDot(graphics, centerDot, state, _config.DebugOverlayStyle ? 2.5f : 2f);
                DrawLabel(graphics, font, back, brush, textShadow, line, castBarBack, castBar, relatedLogText, state, point, _config.ShowCastBar, _config.ShowActCastProgressBar, GetActCastProgress(state), GetRelatedLogs(state));
            }

            DrawRelatedLogPanel(graphics, font, textShadow, states);
        }
    }

    private IReadOnlyList<EntityDisplayState> GetDisplayStatesForRender()
    {
        var now = Stopwatch.GetTimestamp();
        var scanInterval = GetScanIntervalTicks(_config.EntityScanHz);
        if (_cachedStates.Count == 0 || now - _lastStateBuildTicks >= scanInterval)
        {
            _partyListTailer?.RefreshIfDue();
            var partyEntityIds = _partyListTracker?.PartyEntityIds ?? Array.Empty<uint>();
            _displayStateService.PartyEntityIds = partyEntityIds;
            _cachedStates = _displayStateService.BuildStates(ClientSize.Width, ClientSize.Height, _config);
            _relatedLogStore?.UpdateContext(RelatedActLogContext.FromEntities(_displayStateService.LastRawEntities, partyEntityIds));
            _lastStateBuildTicks = now;
        }

        return _cachedStates;
    }

    private IReadOnlyList<RelatedActLogEntry> GetRelatedLogs(EntityDisplayState state)
    {
        if (!_config.ShowRelatedActLogs || !_config.ShowRelatedActLogsNearEntity || _relatedLogStore == null)
        {
            return Array.Empty<RelatedActLogEntry>();
        }

        return _relatedLogStore.GetRecent(
            state.Snapshot.EntityId,
            _config.RelatedActLogSeconds,
            _config.RelatedActLogMaxLinesPerEntity);
    }

    private ActCastProgressEntry? GetActCastProgress(EntityDisplayState state)
    {
        if (!_config.ShowActCastProgressBar || _actCastProgressStore == null)
        {
            return null;
        }

        return _actCastProgressStore.GetActive(state.Snapshot.EntityId);
    }

    private static Color ToDrawingColor(RgbaColor color)
    {
        return Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    private static Vector2 SnapPoint(Vector2 point)
    {
        return new Vector2((float)Math.Round(point.X), (float)Math.Round(point.Y));
    }

    private static Brush PickBrush(EntityDisplayState state, Brush normal, Brush casting, Brush pinned)
    {
        if (state.Pinned)
        {
            return pinned;
        }

        return state.Snapshot.IsCasting ? casting : normal;
    }

    private static void DrawDiamondMarker(Graphics graphics, Brush marker, Vector2 point, float radius)
    {
        var points = new[]
        {
            new PointF(point.X, point.Y - radius),
            new PointF(point.X + radius, point.Y),
            new PointF(point.X, point.Y + radius),
            new PointF(point.X - radius, point.Y),
        };
        graphics.FillPolygon(marker, points);
    }

    private static void DrawCenterDot(Graphics graphics, Brush brush, EntityDisplayState state, float radius)
    {
        if (!state.HasCenterScreenPosition)
        {
            return;
        }

        var center = SnapPoint(state.CenterScreenPosition);
        graphics.FillEllipse(brush, center.X - radius, center.Y - radius, radius * 2f, radius * 2f);
    }

    private static void DrawLabel(Graphics graphics, Font font, Brush back, Brush text, Brush textShadow, Pen border, Brush castBarBack, Brush castBar, Brush relatedLogText, EntityDisplayState state, Vector2 snappedPoint, bool showCastBar, bool showActCastProgressBar, ActCastProgressEntry? actCast, IReadOnlyList<RelatedActLogEntry> relatedLogs)
    {
        var label = state.LabelText;
        if (state.IsFilteredDebug && !string.IsNullOrWhiteSpace(state.FilterReason))
        {
            label += Environment.NewLine + "Filtered: " + state.FilterReason;
        }

        var relatedText = BuildRelatedLogText(relatedLogs);
        if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(relatedText))
        {
            return;
        }

        var point = snappedPoint;
        var labelSize = string.IsNullOrWhiteSpace(label) ? SizeF.Empty : graphics.MeasureString(label, font);
        var logSize = string.IsNullOrWhiteSpace(relatedText) ? SizeF.Empty : graphics.MeasureString(relatedText, font);
        var castBarHeight = state.Snapshot.IsCasting && showCastBar ? 3f : 0f;
        var actCastBarHeight = showActCastProgressBar && actCast != null ? 3f : 0f;
        var actCastGap = actCastBarHeight > 0f ? 3f : 0f;
        var logGap = string.IsNullOrWhiteSpace(relatedText) ? 0f : 4f;
        var actCastLabel = actCast == null ? string.Empty : (actCast.GetRemainingSeconds(DateTime.UtcNow).ToString("0.0") + "s");
        var actCastLabelSize = string.IsNullOrWhiteSpace(actCastLabel) ? SizeF.Empty : graphics.MeasureString(actCastLabel, font);
        var castProgressWidth = Math.Max(labelSize.Width, actCastLabelSize.Width);
        var contentWidth = Math.Max(Math.Max(labelSize.Width, logSize.Width), actCastLabelSize.Width);
        var rect = new RectangleF(
            point.X + 7f,
            point.Y - 16f,
            (float)Math.Ceiling(Math.Max(contentWidth, 96f)) + 8f,
            (float)Math.Ceiling(labelSize.Height + castBarHeight + actCastGap + actCastBarHeight + (actCastLabelSize.Height > 0f ? actCastLabelSize.Height : 0f) + logGap + logSize.Height) + 5f);
        if (back is SolidBrush solidBack && solidBack.Color.A > 0)
        {
            graphics.FillRectangle(back, rect);
        }

        if (border.Color.A > 0)
        {
            graphics.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
        }

        var nextY = rect.Y + 2f;
        if (!string.IsNullOrWhiteSpace(label))
        {
            DrawOutlinedText(graphics, label, font, textShadow, text, rect.X + 4f, nextY);
        }

        nextY += labelSize.Height;

        if (showCastBar && state.Snapshot.IsCasting && state.CastProgress > 0f)
        {
            var barRect = new RectangleF(rect.X + 4f, nextY, rect.Width - 8f, castBarHeight);
            if (castBarBack is SolidBrush solidCastBack && solidCastBack.Color.A > 0)
            {
                graphics.FillRectangle(castBarBack, barRect);
            }

            graphics.FillRectangle(castBar, barRect.X, barRect.Y, barRect.Width * state.CastProgress, barRect.Height);
            nextY += castBarHeight;
        }

        if (showActCastProgressBar && actCast != null)
        {
            nextY += actCastGap;
            var barRect = new RectangleF(rect.X + 4f, nextY, Math.Max(48f, castProgressWidth), actCastBarHeight);
            if (castBarBack is SolidBrush solidCastBack && solidCastBack.Color.A > 0)
            {
                graphics.FillRectangle(castBarBack, barRect);
            }

            var progress = actCast.GetProgress(DateTime.UtcNow);
            using (var actCastFill = new SolidBrush(Color.FromArgb(225, 230, 210, 92)))
            using (var actCastGlow = new Pen(Color.FromArgb(175, 130, 255, 180), 1f))
            {
                graphics.FillRectangle(actCastFill, barRect.X, barRect.Y, barRect.Width * progress, barRect.Height);
                graphics.DrawLine(actCastGlow, barRect.X, barRect.Y - 1f, barRect.X + barRect.Width * progress, barRect.Y - 1f);
            }

            nextY += actCastBarHeight;
            if (!string.IsNullOrWhiteSpace(actCastLabel))
            {
                using (var castText = new SolidBrush(Color.FromArgb(225, 245, 230, 150)))
                {
                    DrawOutlinedText(graphics, actCastLabel, font, textShadow, castText, rect.X + 4f, nextY);
                }

                nextY += actCastLabelSize.Height;
            }
        }

        if (!string.IsNullOrWhiteSpace(relatedText))
        {
            DrawOutlinedText(graphics, relatedText, font, textShadow, relatedLogText, rect.X + 4f, nextY + logGap);
        }
    }

    private void DrawRelatedLogPanel(Graphics graphics, Font font, Brush textShadow, IReadOnlyList<EntityDisplayState> states)
    {
        if (!_config.ShowRelatedActLogs || !_config.ShowRelatedActLogPanel || _relatedLogStore == null || states == null || states.Count == 0)
        {
            return;
        }

        var entityIds = states.Select(state => state.Snapshot.EntityId).Where(id => id != 0).ToArray();
        var entries = _relatedLogStore.GetRecentForEntities(entityIds, _config.RelatedActLogPanelSeconds, _config.RelatedActLogPanelMaxLines);
        if (entries.Count == 0)
        {
            return;
        }

        var lines = new List<string> { "Entity Logs" };
        foreach (var pair in entries)
        {
            var text = string.IsNullOrWhiteSpace(pair.Value.DisplayLine)
                ? RelatedActLogFormatter.StripDisplayPrefix(pair.Value.Line)
                : RelatedActLogFormatter.StripDisplayPrefix(pair.Value.DisplayLine);
            lines.Add("#" + pair.Key.ToString("X8") + " " + text);
        }

        var maxWidth = Math.Max(360f, Math.Min(900f, ClientSize.Width * 0.56f));
        var contentWidth = maxWidth - 14f;
        var wrappedLines = WrapPanelLines(graphics, lines, font, contentWidth);
        var panelText = string.Join(Environment.NewLine, wrappedLines);
        var size = graphics.MeasureString(panelText, font, (int)contentWidth);
        var rect = new RectangleF(
            ClientSize.Width - maxWidth - 12f,
            72f,
            maxWidth,
            Math.Min(ClientSize.Height - 84f, (float)Math.Ceiling(size.Height) + 12f));

        using (var back = new SolidBrush(ToDrawingColor(OverlayStyleService.ParseHexColor(_config.Style.RelatedLogPanelBackgroundColor, 0.55f))))
        using (var border = new Pen(Color.FromArgb(110, 160, 240, 255), 1f))
        using (var text = new SolidBrush(ToDrawingColor(OverlayStyleService.ParseHexColor(_config.Style.RelatedLogPanelTextColor, 0.96f))))
        {
            if (back.Color.A > 0)
            {
                graphics.FillRectangle(back, rect);
            }

            graphics.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
            DrawWrappedOutlinedLines(graphics, wrappedLines, font, textShadow, text, rect.X + 7f, rect.Y + 6f, rect.Bottom - 6f);
        }
    }

    private static string BuildRelatedLogText(IReadOnlyList<RelatedActLogEntry> relatedLogs)
    {
        if (relatedLogs == null || relatedLogs.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        foreach (var entry in relatedLogs)
        {
            var line = string.IsNullOrWhiteSpace(entry.DisplayLine)
                ? RelatedActLogFormatter.StripDisplayPrefix(entry.Line)
                : RelatedActLogFormatter.StripDisplayPrefix(entry.DisplayLine);
            lines.Add(line);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<string> WrapPanelLines(Graphics graphics, IReadOnlyList<string> lines, Font font, float maxWidth)
    {
        var wrapped = new List<string>();
        foreach (var line in lines)
        {
            WrapPanelLine(graphics, line ?? string.Empty, font, maxWidth, wrapped);
        }

        return wrapped;
    }

    private static void WrapPanelLine(Graphics graphics, string line, Font font, float maxWidth, List<string> output)
    {
        if (string.IsNullOrWhiteSpace(line) || graphics.MeasureString(line, font).Width <= maxWidth)
        {
            output.Add(line);
            return;
        }

        var start = 0;
        var firstLine = true;
        while (start < line.Length)
        {
            var prefix = firstLine ? string.Empty : "  ";
            var availableWidth = Math.Max(40f, maxWidth - graphics.MeasureString(prefix, font).Width);
            var count = FindMaxFittingCharCount(graphics, line, start, font, availableWidth);
            output.Add(prefix + line.Substring(start, count));
            start += count;
            firstLine = false;
        }
    }

    private static int FindMaxFittingCharCount(Graphics graphics, string text, int start, Font font, float maxWidth)
    {
        var remaining = text.Length - start;
        var left = 1;
        var right = remaining;
        while (left < right)
        {
            var mid = (left + right + 1) / 2;
            if (graphics.MeasureString(text.Substring(start, mid), font).Width <= maxWidth)
            {
                left = mid;
            }
            else
            {
                right = mid - 1;
            }
        }

        return Math.Max(1, left);
    }

    private static void DrawWrappedOutlinedLines(Graphics graphics, IReadOnlyList<string> lines, Font font, Brush outline, Brush fill, float x, float y, float maxY)
    {
        var lineHeight = font.GetHeight(graphics) + 1f;
        foreach (var line in lines)
        {
            if (y + lineHeight > maxY)
            {
                return;
            }

            DrawOutlinedText(graphics, line, font, outline, fill, x, y);
            y += lineHeight;
        }
    }

    private static string TrimToWidth(Graphics graphics, string text, Font font, float maxWidth)
    {
        if (graphics.MeasureString(text, font).Width <= maxWidth)
        {
            return text;
        }

        const string suffix = "...";
        var left = 0;
        var right = text.Length;
        while (left < right)
        {
            var mid = (left + right + 1) / 2;
            var candidate = text.Substring(0, mid) + suffix;
            if (graphics.MeasureString(candidate, font).Width <= maxWidth)
            {
                left = mid;
            }
            else
            {
                right = mid - 1;
            }
        }

        return text.Substring(0, Math.Max(0, left)) + suffix;
    }

    private static void DrawOutlinedText(Graphics graphics, string text, Font font, Brush outline, Brush fill, float x, float y)
    {
        if (outline is SolidBrush solidOutline && solidOutline.Color.A > 0)
        {
            graphics.DrawString(text, font, outline, x - 1f, y);
            graphics.DrawString(text, font, outline, x + 1f, y);
            graphics.DrawString(text, font, outline, x, y - 1f);
            graphics.DrawString(text, font, outline, x, y + 1f);
        }

        graphics.DrawString(text, font, fill, x, y);
    }
}
