using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class NetworkPartyListTailer
{
    private readonly EnvironmentPaths _paths;
    private readonly PartyListTracker _tracker;
    private DateTime _lastCheckUtc = DateTime.MinValue;
    private string _lastPath = string.Empty;
    private long _lastLength;
    private DateTime _lastWriteUtc;

    public NetworkPartyListTailer(EnvironmentPaths paths, PartyListTracker tracker)
    {
        _paths = paths;
        _tracker = tracker;
    }

    public void RefreshIfDue()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastCheckUtc).TotalSeconds < 1)
        {
            return;
        }

        _lastCheckUtc = now;
        var file = FindLatestNetworkLog();
        if (file == null)
        {
            return;
        }

        if (string.Equals(file.FullName, _lastPath, StringComparison.OrdinalIgnoreCase)
            && file.Length == _lastLength
            && file.LastWriteTimeUtc == _lastWriteUtc)
        {
            return;
        }

        _lastPath = file.FullName;
        _lastLength = file.Length;
        _lastWriteUtc = file.LastWriteTimeUtc;

        foreach (var line in ReadTailLines(file.FullName, maxBytes: 4 * 1024 * 1024).Where(line => line.StartsWith("11|", StringComparison.Ordinal)).TakeLastCompat(8))
        {
            _tracker.ObserveLogLine(line);
        }
    }

    private FileInfo? FindLatestNetworkLog()
    {
        var dir = Path.Combine(_paths.ActDirectory, "FFXIVLogs");
        if (!Directory.Exists(dir))
        {
            return null;
        }

        return new DirectoryInfo(dir)
            .GetFiles("Network_*.log")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static IEnumerable<string> ReadTailLines(string path, int maxBytes)
    {
        try
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
        catch
        {
            return Array.Empty<string>();
        }
    }
}

internal static class EnumerableExtensions
{
    public static IEnumerable<T> TakeLastCompat<T>(this IEnumerable<T> source, int count)
    {
        var queue = new Queue<T>();
        foreach (var item in source)
        {
            queue.Enqueue(item);
            if (queue.Count > count)
            {
                queue.Dequeue();
            }
        }

        return queue.ToArray();
    }
}
