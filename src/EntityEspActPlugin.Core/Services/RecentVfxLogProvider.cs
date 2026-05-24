using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class RecentVfxLogProvider
{
    private static readonly Regex TickPattern = new Regex(@"^tick=\d+\s+time=(?<time>\d{2}:\d{2}:\d{2})\b", RegexOptions.Compiled);
    private static readonly Regex VfxPattern = new Regex(@"^\s*\+\s+(?<address>0x[0-9A-Fa-f]+)\s+(?<path>.+\.avfx)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly string _capturesDirectory;
    private readonly object _syncRoot = new object();
    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private string _loadedFile = string.Empty;
    private DateTime _loadedWriteUtc = DateTime.MinValue;
    private long _loadedLength;
    private IReadOnlyList<RecentVfxEntry> _entries = Array.Empty<RecentVfxEntry>();

    public RecentVfxLogProvider()
        : this(FindDefaultCapturesDirectory())
    {
    }

    public RecentVfxLogProvider(string capturesDirectory)
    {
        _capturesDirectory = capturesDirectory;
    }

    public IReadOnlyList<RecentVfxEntry> GetRecent(float recentSeconds, float displaySeconds, int maxEntries = 12)
    {
        RefreshIfDue();
        var now = DateTime.Now;
        var windowSeconds = Math.Max(1f, Math.Max(recentSeconds, displaySeconds));
        lock (_syncRoot)
        {
            return _entries
                .Where(entry => (now - entry.SeenAt).TotalSeconds <= windowSeconds)
                .OrderByDescending(entry => entry.SeenAt)
                .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(1, maxEntries))
                .ToArray();
        }
    }

    public string LoadedFile
    {
        get
        {
            lock (_syncRoot)
            {
                return _loadedFile;
            }
        }
    }

    private void RefreshIfDue()
    {
        var now = DateTime.UtcNow;
        lock (_syncRoot)
        {
            if ((now - _lastRefreshUtc).TotalSeconds < 1)
            {
                return;
            }

            _lastRefreshUtc = now;
        }

        Refresh();
    }

    private void Refresh()
    {
        try
        {
            if (!Directory.Exists(_capturesDirectory))
            {
                return;
            }

            var file = Directory.GetFiles(_capturesDirectory, "cast-vfx-*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(file))
            {
                return;
            }

            var info = new FileInfo(file);
            lock (_syncRoot)
            {
                if (string.Equals(file, _loadedFile, StringComparison.OrdinalIgnoreCase)
                    && info.LastWriteTimeUtc == _loadedWriteUtc
                    && info.Length == _loadedLength)
                {
                    return;
                }
            }

            var entries = LoadEntries(file);
            lock (_syncRoot)
            {
                _entries = entries;
                _loadedFile = file;
                _loadedWriteUtc = info.LastWriteTimeUtc;
                _loadedLength = info.Length;
            }
        }
        catch
        {
            // Raw VFX display is optional. Malformed/in-progress logs must never break the ACT overlay.
        }
    }

    private static IReadOnlyList<RecentVfxEntry> LoadEntries(string file)
    {
        var lines = ReadTailLines(file, maxBytes: 2 * 1024 * 1024);
        var fileDate = File.GetLastWriteTime(file).Date;
        var currentTickTime = File.GetLastWriteTime(file);
        var result = new List<RecentVfxEntry>();

        foreach (var line in lines)
        {
            var tickMatch = TickPattern.Match(line);
            if (tickMatch.Success && TimeSpan.TryParseExact(tickMatch.Groups["time"].Value, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var tickTime))
            {
                currentTickTime = fileDate.Add(tickTime);
                if (currentTickTime > File.GetLastWriteTime(file).AddHours(1))
                {
                    currentTickTime = currentTickTime.AddDays(-1);
                }

                continue;
            }

            var vfxMatch = VfxPattern.Match(line);
            if (!vfxMatch.Success)
            {
                continue;
            }

            result.Add(new RecentVfxEntry
            {
                SeenAt = currentTickTime,
                Address = vfxMatch.Groups["address"].Value,
                Path = vfxMatch.Groups["path"].Value.Trim(),
            });
        }

        return result;
    }

    private static IEnumerable<string> ReadTailLines(string path, int maxBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var bytesToRead = Math.Min(stream.Length, maxBytes);
        stream.Seek(-bytesToRead, SeekOrigin.End);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd()
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Where(line => line.Length > 0)
            .ToArray();
    }

    private static string FindDefaultCapturesDirectory()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = Path.Combine(dir, "tools", "EntityEspProbe", "captures");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(dir);
            if (parent == null)
            {
                break;
            }

            dir = parent.FullName;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Advanced Combat Tracker", "Config", "EntityEspProbe", "captures");
    }
}
