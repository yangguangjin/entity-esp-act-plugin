using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class LiveVfxMonitorService : IDisposable
{
    private const int MaxRegionBytesPerTick = 512 * 1024 * 1024;
    private const int ChunkSize = 4 * 1024 * 1024;
    private readonly object _syncRoot = new object();
    private readonly Dictionary<string, LiveVfxEntry> _entries = new Dictionary<string, LiveVfxEntry>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<long> _hotRegions = new HashSet<long>();
    private List<MemoryRegion> _regions = new List<MemoryRegion>();
    private int _nextRegionIndex;
    private Thread? _worker;
    private bool _stopRequested;
    private string _status = "VFX monitor: stopped";

    public bool IsRunning
    {
        get
        {
            lock (_syncRoot)
            {
                return _worker != null && _worker.IsAlive;
            }
        }
    }

    public string StatusText
    {
        get
        {
            lock (_syncRoot)
            {
                return _status;
            }
        }
    }

    public void EnsureRunning()
    {
        lock (_syncRoot)
        {
            if (_worker != null && _worker.IsAlive)
            {
                return;
            }

            _stopRequested = false;
            _worker = new Thread(RunWorker)
            {
                IsBackground = true,
                Name = "Entity ESP Live VFX Monitor",
            };
            _worker.Start();
            _status = "VFX monitor: starting";
        }
    }

    public void Stop()
    {
        Thread? worker;
        lock (_syncRoot)
        {
            _stopRequested = true;
            worker = _worker;
            _status = "VFX monitor: stopping";
        }

        if (worker != null && worker.IsAlive)
        {
            worker.Join(1500);
        }

        lock (_syncRoot)
        {
            _worker = null;
            _status = "VFX monitor: stopped";
        }
    }

    /// <summary>
    /// 功能：记录一次 VFX 路径观察结果，供内存扫描线程和后续 active VFX 数据源复用。
    /// </summary>
    public void RecordPathObservation(string path, long address, DateTime observedAt, bool markAsNew)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        lock (_syncRoot)
        {
            UpsertPathObservationNoLock(path, address, observedAt, markAsNew);
        }
    }

    /// <summary>
    /// 功能：生成 VFX 面板快照；支持短存活路径在消失后按配置续显，降低扫描频率造成的漏显。
    /// </summary>
    public IReadOnlyList<LiveVfxEntry> Snapshot(
        float windowSeconds,
        float displaySeconds,
        int maxEntries = 14,
        float shortLivedMaxAgeSeconds = 0f,
        float shortLivedHoldSeconds = 0f,
        DateTime? nowOverride = null)
    {
        var now = nowOverride ?? DateTime.UtcNow;
        var keepSeconds = Math.Max(1f, Math.Max(windowSeconds, displaySeconds));
        var effectiveDisplaySeconds = Math.Max(1f, displaySeconds);
        var effectiveShortLivedMaxAgeSeconds = Math.Max(0f, shortLivedMaxAgeSeconds);
        var effectiveShortLivedHoldSeconds = Math.Max(0f, shortLivedHoldSeconds);
        lock (_syncRoot)
        {
            return _entries.Values
                .Select(entry => BuildSnapshotEntry(entry, now, keepSeconds, effectiveDisplaySeconds, effectiveShortLivedMaxAgeSeconds, effectiveShortLivedHoldSeconds))
                .Where(entry => entry != null)
                .Cast<LiveVfxEntry>()
                .OrderByDescending(entry => entry.IsNew)
                .ThenBy(entry => entry.IsHeldShortLived ? 1 : 0)
                .ThenByDescending(entry => entry.LastSeenAt)
                .Take(Math.Max(1, maxEntries))
                .ToList();
        }
    }

    /// <summary>
    /// 功能：把内部 entry 转成面板快照 entry，并决定是否因短存活规则进入 HOLD 状态。
    /// </summary>
    private static LiveVfxEntry? BuildSnapshotEntry(
        LiveVfxEntry entry,
        DateTime now,
        float keepSeconds,
        float displaySeconds,
        float shortLivedMaxAgeSeconds,
        float shortLivedHoldSeconds)
    {
        var ageSeconds = Math.Max(0, (now - entry.LastSeenAt).TotalSeconds);
        var lifeSeconds = Math.Max(0, (entry.LastSeenAt - entry.FirstSeenAt).TotalSeconds);
        var isWithinNormalWindow = ageSeconds <= keepSeconds;
        var isHeldShortLived = !isWithinNormalWindow
            && shortLivedMaxAgeSeconds > 0f
            && shortLivedHoldSeconds > 0f
            && lifeSeconds <= shortLivedMaxAgeSeconds
            && ageSeconds <= shortLivedHoldSeconds;

        if (!isWithinNormalWindow && !isHeldShortLived)
        {
            return null;
        }

        return new LiveVfxEntry
        {
            Path = entry.Path,
            Address = entry.Address,
            FirstSeenAt = entry.FirstSeenAt,
            LastSeenAt = entry.LastSeenAt,
            IsNew = !isHeldShortLived && entry.IsNew && (now - entry.FirstSeenAt).TotalSeconds <= displaySeconds,
            IsHeldShortLived = isHeldShortLived,
        };
    }

    /// <summary>
    /// 功能：在已持有锁的情况下更新 VFX 路径生命周期，保留第一次出现时间并刷新最近观察时间。
    /// </summary>
    private void UpsertPathObservationNoLock(string path, long address, DateTime observedAt, bool markAsNew)
    {
        if (_entries.TryGetValue(path, out var existing))
        {
            existing.Address = address;
            existing.LastSeenAt = observedAt;
            existing.IsNew = existing.IsNew && (observedAt - existing.FirstSeenAt).TotalSeconds <= 12;
            existing.IsHeldShortLived = false;
            return;
        }

        _entries[path] = new LiveVfxEntry
        {
            Path = path,
            Address = address,
            FirstSeenAt = observedAt,
            LastSeenAt = observedAt,
            IsNew = markAsNew,
            IsHeldShortLived = false,
        };
    }

    public void Dispose()
    {
        Stop();
    }

    private void RunWorker()
    {
        using var reader = new ProcessMemoryReader();
        var baselineDone = false;
        var tick = 0;
        while (true)
        {
            lock (_syncRoot)
            {
                if (_stopRequested)
                {
                    return;
                }
            }

            try
            {
                if (!reader.IsReady)
                {
                    reader.Refresh();
                    _regions.Clear();
                    _nextRegionIndex = 0;
                }

                if (!reader.IsReady)
                {
                    SetStatus("VFX monitor: waiting for FF14 process, " + reader.Status.LastError);
                    Thread.Sleep(1000);
                    continue;
                }

                tick++;
                var current = ScanAvfxPathsIncremental(reader, tick, out var scannedBytes, out var scannedRegions, out var totalRegions, out var fullCycleCompleted);
                var scanCompletedAt = DateTime.UtcNow;
                MergeScan(current, scanCompletedAt, baselineDone, tick, scannedBytes, scannedRegions, totalRegions, fullCycleCompleted);
                baselineDone = true;
            }
            catch (Exception ex)
            {
                SetStatus("VFX monitor: " + ex.GetType().Name + ": " + ex.Message);
            }

            Thread.Sleep(250);
        }
    }

    private void MergeScan(Dictionary<string, long> current, DateTime now, bool baselineDone, int tick, long scannedBytes, int scannedRegions, int totalRegions, bool fullCycleCompleted)
    {
        lock (_syncRoot)
        {
            foreach (var item in current)
            {
                // 功能：把本轮扫描命中的 path 写入统一观察表，避免扫描路径和后续 active 数据源各写一套生命周期逻辑。
                UpsertPathObservationNoLock(item.Key, item.Value, now, baselineDone);
            }

            foreach (var stale in _entries.Where(pair => (now - pair.Value.LastSeenAt).TotalSeconds > 300).Select(pair => pair.Key).ToList())
            {
                _entries.Remove(stale);
            }

            var newCount = _entries.Values.Count(entry => entry.IsNew && (now - entry.FirstSeenAt).TotalSeconds <= 12);
            _status = "VFX monitor: running tick=" + tick
                + " paths=" + _entries.Count
                + " new=" + newCount
                + " scannedMB/tick=" + (scannedBytes / 1024.0 / 1024.0).ToString("0.0")
                + " regions=" + scannedRegions + "/" + totalRegions
                + (fullCycleCompleted ? " cycle=done" : string.Empty)
                + " hot=" + _hotRegions.Count;
        }
    }

    private void SetStatus(string status)
    {
        lock (_syncRoot)
        {
            _status = status;
        }
    }

    private Dictionary<string, long> ScanAvfxPathsIncremental(ProcessMemoryReader reader, int tick, out long scannedBytes, out int scannedRegions, out int totalRegions, out bool fullCycleCompleted)
    {
        if (_regions.Count == 0 || tick % 60 == 1)
        {
            _regions = reader.GetReadableMemoryRegions(maxRegionSize: 32 * 1024 * 1024)
                .Where(region => region.Size >= 4096)
                .ToList();
            _nextRegionIndex = 0;
        }

        totalRegions = _regions.Count;
        scannedBytes = 0;
        scannedRegions = 0;
        fullCycleCompleted = false;
        var hits = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (_regions.Count == 0)
        {
            return hits;
        }

        foreach (var region in _regions.Where(region => _hotRegions.Contains(region.BaseAddress)).Take(64))
        {
            ScanRegion(reader, region, hits, ref scannedBytes, ref scannedRegions);
            if (scannedBytes >= MaxRegionBytesPerTick / 2)
            {
                break;
            }
        }

        var visited = 0;
        while (visited < _regions.Count && scannedBytes < MaxRegionBytesPerTick)
        {
            var region = _regions[_nextRegionIndex];
            _nextRegionIndex = (_nextRegionIndex + 1) % _regions.Count;
            visited++;
            if (_nextRegionIndex == 0)
            {
                fullCycleCompleted = true;
            }

            ScanRegion(reader, region, hits, ref scannedBytes, ref scannedRegions);
        }

        return hits;
    }

    private void ScanRegion(ProcessMemoryReader reader, MemoryRegion region, Dictionary<string, long> hits, ref long scannedBytes, ref int scannedRegions)
    {
        var regionHadHit = false;
        var remaining = region.Size;
        var address = region.BaseAddress;
        while (remaining > 0)
        {
            var chunkSize = (int)Math.Min(ChunkSize, remaining);
            if (reader.TryReadBytes(address, chunkSize, out var bytes))
            {
                scannedBytes += bytes.Length;
                foreach (var hit in ExtractAvfxPaths(bytes, address))
                {
                    regionHadHit = true;
                    if (!hits.ContainsKey(hit.Path))
                    {
                        hits.Add(hit.Path, hit.Address);
                    }
                }
            }

            address += chunkSize;
            remaining -= chunkSize;
            if (hits.Count >= 2000)
            {
                break;
            }
        }

        scannedRegions++;
        if (regionHadHit)
        {
            _hotRegions.Add(region.BaseAddress);
        }
    }

    /// <summary>
    /// 功能：从字节块中提取 VFX path 候选；公开给测试和 probe 复用，避免 .avfx 扩展名匹配再次写反。
    /// </summary>
    public static IReadOnlyList<string> ExtractAvfxPathCandidates(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return Array.Empty<string>();
        }

        return ExtractAvfxPaths(bytes, 0)
            .Select(hit => hit.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<AvfxMemoryHit> ExtractAvfxPaths(byte[] bytes, long baseAddress)
    {
        for (var i = 0; i <= bytes.Length - 5; i++)
        {
            if (!IsAvfxAt(bytes, i))
            {
                continue;
            }

            var start = i;
            while (start > 0 && IsPathByte(bytes[start - 1]))
            {
                start--;
            }

            var end = i + 5;
            while (end < bytes.Length && IsPathByte(bytes[end]))
            {
                end++;
            }

            var length = end - start;
            if (length < 12 || length > 180)
            {
                continue;
            }

            var text = Encoding.ASCII.GetString(bytes, start, length).Replace('\\', '/').ToLowerInvariant();
            if (!text.EndsWith(".avfx", StringComparison.OrdinalIgnoreCase) || !text.Contains("vfx/"))
            {
                continue;
            }

            yield return new AvfxMemoryHit(text, baseAddress + start);
        }
    }

    private static bool IsAvfxAt(byte[] bytes, int index)
    {
        return bytes[index] == (byte)'.'
            && (bytes[index + 1] == (byte)'a' || bytes[index + 1] == (byte)'A')
            && (bytes[index + 2] == (byte)'v' || bytes[index + 2] == (byte)'V')
            && (bytes[index + 3] == (byte)'f' || bytes[index + 3] == (byte)'F')
            && (bytes[index + 4] == (byte)'x' || bytes[index + 4] == (byte)'X');
    }

    private static bool IsPathByte(byte value)
    {
        return (value >= (byte)'a' && value <= (byte)'z')
            || (value >= (byte)'A' && value <= (byte)'Z')
            || (value >= (byte)'0' && value <= (byte)'9')
            || value == (byte)'/'
            || value == (byte)'\\'
            || value == (byte)'_'
            || value == (byte)'-'
            || value == (byte)'.';
    }

    private readonly struct AvfxMemoryHit
    {
        public AvfxMemoryHit(string path, long address)
        {
            Path = path;
            Address = address;
        }

        public string Path { get; }
        public long Address { get; }
    }
}
