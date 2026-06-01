using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class ActiveVfxMemoryService : IDisposable
{
    private const string WorldInstanceSignature = "48 8B 05 ?? ?? ?? ?? 48 8B 50 40";
    private const int WorldResolveOffset = 3;
    private const int WorldInstructionLength = 7;
    private const int SceneObjectHeaderBytes = 0x80;
    private const int VfxObjectBytes = 0x2A8;
    private const int MaxGraphNodes = 8000;
    /// <summary>功能：未知 vtable 解析失败后的短暂抑制秒数，避免每帧重复重读非 VFX 对象但仍允许副本中新类型重试。</summary>
    private const float FailedVTableProbeTtlSeconds = 2f;
    private readonly object _syncRoot = new object();
    private readonly ProcessMemoryReader _reader = new ProcessMemoryReader();
    private readonly GameObjectTableReader _objectTableReader;
    /// <summary>功能：把 VFX 面板 UI tick 和重型 Scene.World 遍历解耦，UI 只读取缓存。</summary>
    private readonly VfxSnapshotSampler _snapshotSampler;
    /// <summary>功能：缓存已确认和短暂失败的 VFX vtable 探测状态，避免单一白名单漏掉副本内新 VFX 子类。</summary>
    private readonly VfxObjectProbeCache _vfxProbeCache = new VfxObjectProbeCache();
    private readonly Dictionary<long, DateTime> _firstSeenByAddress = new Dictionary<long, DateTime>();
    private readonly ActiveVfxDisplayCache _displayCache = new ActiveVfxDisplayCache();
    private long _worldGlobalAddress;
    /// <summary>功能：记录上一帧 World root 数量，显示到 VFX 面板状态栏用于排查 root 是否失效。</summary>
    private int _lastRootCount;
    /// <summary>功能：记录上一帧遍历到的 scene node 数量，显示到状态栏用于判断 graph 是否仍在刷新。</summary>
    private int _lastGraphNodeCount;
    /// <summary>功能：记录上一帧尝试按 VfxObject 解析的 node 数量，辅助观察未知 vtable 是否还在被重试。</summary>
    private int _lastVTableProbeCount;
    /// <summary>功能：记录上一帧因短 TTL 失败缓存跳过的 node 数量，辅助判断性能过滤是否生效。</summary>
    private int _lastVTableSkipCount;
    private bool _isRunning;
    private string _status = "Active VFX memory: stopped";

    public ActiveVfxMemoryService()
    {
        _objectTableReader = new GameObjectTableReader(_reader);
        _snapshotSampler = new VfxSnapshotSampler(SnapshotSections);
    }

    public bool IsRunning
    {
        get
        {
            lock (_syncRoot)
            {
                return _isRunning;
            }
        }
    }

    public string StatusText => _status + "; " + _snapshotSampler.StatusText;

    /// <summary>功能：启用 active VFX 直接内存读取；不启动后台扫描线程。</summary>
    public void EnsureRunning()
    {
        lock (_syncRoot)
        {
            _isRunning = true;
            _status = "Active VFX memory: ready";
        }
    }

    /// <summary>功能：停止 active VFX 直接读取并清理本轮运行缓存。</summary>
    public void Stop()
    {
        lock (_syncRoot)
        {
            _isRunning = false;
            _firstSeenByAddress.Clear();
            _vfxProbeCache.Clear();
            _displayCache.Clear();
            _snapshotSampler.Reset();
            _status = "Active VFX memory: stopped";
        }
    }

    /// <summary>功能：按当前 UI 帧直接遍历 Scene.World，返回历史日志式 VFX 列表；保留旧调用兼容。</summary>
    public IReadOnlyList<VfxMonitorEntry> Snapshot(EspConfig config)
    {
        return SnapshotSections(config).HistoryEntries;
    }

    /// <summary>功能：按当前 UI 帧读取缓存，并在后台按 VfxSampleHz 异步刷新 active VFX 快照。</summary>
    public VfxMonitorSnapshot SnapshotSectionsCached(EspConfig config)
    {
        return _snapshotSampler.GetLatest(config);
    }

    /// <summary>功能：按当前 UI 帧直接遍历 Scene.World，同时返回当前实时存活区和历史日志区；仅供后台采样器/诊断调用。</summary>
    public VfxMonitorSnapshot SnapshotSections(EspConfig config)
    {
        lock (_syncRoot)
        {
            if (!_isRunning)
            {
                return VfxMonitorSnapshot.Empty;
            }

            if (!_reader.IsReady)
            {
                // 功能：FF14 进程未就绪时按 1 秒节流刷新，避免后台采样循环高频 OpenProcess。
                _reader.RefreshIfDue(TimeSpan.FromSeconds(1));
                _worldGlobalAddress = 0;
                _vfxProbeCache.Clear();
            }

            if (!_reader.IsReady)
            {
                _status = "Active VFX memory: waiting for FF14 process, " + _reader.Status.LastError;
                return VfxMonitorSnapshot.Empty;
            }

            if (!TryResolveWorldAddress(out var worldAddress))
            {
                return VfxMonitorSnapshot.Empty;
            }

            var now = DateTime.UtcNow;
            var activeEntries = ReadActiveVfxEntries(worldAddress, now);
            var liveEntries = PrepareLiveEntriesForDisplay(activeEntries, config);
            var historyEntries = _displayCache.Update(
                liveEntries,
                Math.Max(0.1f, config.VfxDisplaySeconds),
                Math.Max(0f, config.VfxShortLivedMaxAgeSeconds),
                Math.Max(0f, config.VfxShortLivedHoldSeconds),
                now).ToList();
            PruneFirstSeenCache(historyEntries, now, Math.Max(10f, config.VfxDisplaySeconds + config.VfxShortLivedHoldSeconds + 2f));

            var maxRows = Math.Max(1, config.VfxMaxRows);
            var visibleLiveEntries = liveEntries
                .OrderByDescending(entry => entry.IsNew)
                .ThenBy(entry => entry.Distance ?? float.MaxValue)
                .ThenByDescending(entry => entry.LastSeenAt)
                .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
                .Take(maxRows)
                .ToList();
            var visibleHistoryEntries = historyEntries
                .OrderByDescending(entry => entry.FirstSeenAt)
                .ThenBy(entry => entry.Distance ?? float.MaxValue)
                .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
                .Take(maxRows)
                .ToList();

            _status = "Active VFX memory: active=" + activeEntries.Count
                + " live=" + visibleLiveEntries.Count
                + " history=" + visibleHistoryEntries.Count
                + " roots=" + _lastRootCount
                + " nodes=" + _lastGraphNodeCount
                + " vtables=" + _vfxProbeCache.ConfirmedCount
                + " reject=" + _vfxProbeCache.PruneAndCountRejected(now)
                + " probed=" + _lastVTableProbeCount
                + " skipped=" + _lastVTableSkipCount
                + " world=0x" + worldAddress.ToString("X");
            return new VfxMonitorSnapshot(visibleLiveEntries, visibleHistoryEntries);
        }
    }

    public void Dispose()
    {
        Stop();
        _reader.Dispose();
    }

    /// <summary>功能：为实时存活区计算当前观测距离；返回值也作为历史日志首次快照的入库来源。</summary>
    private List<VfxMonitorEntry> PrepareLiveEntriesForDisplay(IReadOnlyList<VfxMonitorEntry> activeEntries, EspConfig config)
    {
        if (_objectTableReader.TryReadSelfPosition(out var selfPosition))
        {
            return VfxDistanceFilter.Filter(
                activeEntries,
                new VfxAnchorContext(VfxAnchorMode.Self, selfPosition),
                Math.Max(0f, config.VfxMaxDistance),
                showFallbackWithoutPosition: false).ToList();
        }

        var withoutDistance = new List<VfxMonitorEntry>();
        foreach (var activeEntry in activeEntries)
        {
            var clone = activeEntry.Clone();
            clone.Distance = null;
            withoutDistance.Add(clone);
        }

        return withoutDistance;
    }

    /// <summary>功能：解析 Scene.World singleton，并返回当前 World 对象地址。</summary>
    private bool TryResolveWorldAddress(out long worldAddress)
    {
        worldAddress = 0;
        if (_worldGlobalAddress == 0)
        {
            var scan = _reader.ScanMainModule("SceneWorldInstance", WorldInstanceSignature, WorldResolveOffset, WorldInstructionLength, ".text");
            if (!scan.Success || scan.HitCount != 1 || scan.ResolvedAddress == 0)
            {
                _status = "Active VFX memory: Scene.World signature failed hits=" + scan.HitCount + " error=" + scan.Error;
                return false;
            }

            _worldGlobalAddress = scan.ResolvedAddress;
        }

        if (!TryReadPointer(_worldGlobalAddress, out worldAddress) || !LooksLikeUserModePointer(worldAddress))
        {
            _status = "Active VFX memory: Scene.World pointer invalid";
            return false;
        }

        return true;
    }

    /// <summary>功能：从 World 根节点遍历 scene graph，并收集当前 active VFX 条目。</summary>
    private List<VfxMonitorEntry> ReadActiveVfxEntries(long worldAddress, DateTime now)
    {
        var roots = ReadSceneRootCandidates(worldAddress);
        _lastRootCount = roots.Count;
        var queue = new Queue<long>(roots);
        var visited = new HashSet<long>();
        var entries = new List<VfxMonitorEntry>();
        var nodesRead = 0;
        var vtableProbeCount = 0;
        var vtableSkipCount = 0;
        while (queue.Count > 0 && visited.Count < MaxGraphNodes)
        {
            var address = queue.Dequeue();
            if (!LooksLikeUserModePointer(address) || !visited.Add(address))
            {
                continue;
            }

            if (!TryReadSceneObjectHeader(address, out var header))
            {
                continue;
            }

            nodesRead++;
            if (LooksLikeUserModePointer(header.ChildObject) && !visited.Contains(header.ChildObject))
            {
                queue.Enqueue(header.ChildObject);
            }

            if (LooksLikeUserModePointer(header.NextSiblingObject) && !visited.Contains(header.NextSiblingObject))
            {
                queue.Enqueue(header.NextSiblingObject);
            }

            if (!_vfxProbeCache.ShouldProbe(header.VTable, now))
            {
                vtableSkipCount++;
                continue;
            }

            vtableProbeCount++;
            var resolved = TryReadVfxObject(address, header, now, out var entry);
            _vfxProbeCache.MarkProbeResult(header.VTable, resolved, now, FailedVTableProbeTtlSeconds);
            if (!resolved)
            {
                continue;
            }

            entries.Add(entry);
        }

        _lastGraphNodeCount = nodesRead;
        _lastVTableProbeCount = vtableProbeCount;
        _lastVTableSkipCount = vtableSkipCount;
        return entries;
    }

    /// <summary>功能：读取 Scene.World 中当前验证可用的 root 指针候选。</summary>
    private List<long> ReadSceneRootCandidates(long worldAddress)
    {
        var roots = new List<long>();
        foreach (var offset in new[] { 0x30, 0x40, 0x48 })
        {
            if (TryReadPointer(worldAddress + offset, out var ptr) && LooksLikeUserModePointer(ptr) && !roots.Contains(ptr))
            {
                roots.Add(ptr);
            }
        }

        return roots;
    }

    /// <summary>功能：读取 scene object 基类头部，用于低成本遍历 child/next 链。</summary>
    private bool TryReadSceneObjectHeader(long address, out SceneObjectHeader header)
    {
        header = default;
        if (!_reader.TryReadBytes(address, SceneObjectHeaderBytes, out var bytes))
        {
            return false;
        }

        header = new SceneObjectHeader
        {
            Address = address,
            VTable = BitConverter.ToInt64(bytes, 0x00),
            NextSiblingObject = BitConverter.ToInt64(bytes, 0x28),
            ChildObject = BitConverter.ToInt64(bytes, 0x30),
            Position = new Vector3(
                BitConverter.ToSingle(bytes, 0x50),
                BitConverter.ToSingle(bytes, 0x54),
                BitConverter.ToSingle(bytes, 0x58)),
        };
        return LooksLikeUserModePointer(header.VTable);
    }

    /// <summary>功能：按 VfxObject 布局解析 path、position、caster/target，并生成面板条目。</summary>
    private bool TryReadVfxObject(long address, SceneObjectHeader header, DateTime now, out VfxMonitorEntry entry)
    {
        entry = new VfxMonitorEntry();
        if (!_reader.TryReadBytes(address, VfxObjectBytes, out var bytes))
        {
            return false;
        }

        var resourceInstance = BitConverter.ToInt64(bytes, 0x2A0);
        if (!LooksLikeUserModePointer(resourceInstance) || !TryResolveVfxResourcePath(resourceInstance, out var path))
        {
            return false;
        }

        if (!_firstSeenByAddress.TryGetValue(address, out var firstSeen))
        {
            firstSeen = now;
            _firstSeenByAddress[address] = firstSeen;
        }

        entry = new VfxMonitorEntry
        {
            Path = path,
            Address = address,
            FirstSeenAt = firstSeen,
            LastSeenAt = now,
            IsNew = (now - firstSeen).TotalSeconds <= 1.5,
            Source = VfxEntrySource.ActiveInstance,
            Position = header.Position,
            CasterEntityId = unchecked((uint)BitConverter.ToInt32(bytes, 0x128)),
            TargetEntityId = unchecked((uint)BitConverter.ToInt32(bytes, 0x130)),
            Kind = "ActiveInstance",
        };
        return true;
    }

    /// <summary>功能：沿 VfxResourceInstance 指针链解析 ResourceHandle.FileName。</summary>
    private bool TryResolveVfxResourcePath(long resourceInstance, out string path)
    {
        path = string.Empty;
        if (!TryReadPointer(resourceInstance + 0x08, out var vfxResourceUnk) || !LooksLikeUserModePointer(vfxResourceUnk))
        {
            return false;
        }

        if (!TryReadPointer(vfxResourceUnk + 0x18, out var apricotHandle) || !LooksLikeUserModePointer(apricotHandle))
        {
            return false;
        }

        return TryReadStdString(apricotHandle + 0x48, out path) && path.IndexOf(".avfx", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>功能：读取 FF14 std::string，小字符串和堆字符串两种布局都兼容。</summary>
    private bool TryReadStdString(long stdStringAddress, out string value)
    {
        value = string.Empty;
        if (!_reader.TryReadBytes(stdStringAddress, 0x20, out var bytes))
        {
            return false;
        }

        var length = BitConverter.ToUInt64(bytes, 0x10);
        var capacity = BitConverter.ToUInt64(bytes, 0x18);
        if (length == 0 || length > 260 || capacity < length || capacity > 4096)
        {
            return false;
        }

        byte[] stringBytes;
        if (capacity <= 15)
        {
            stringBytes = bytes.Take((int)length).ToArray();
        }
        else
        {
            var ptr = BitConverter.ToInt64(bytes, 0);
            if (!LooksLikeUserModePointer(ptr) || !_reader.TryReadBytes(ptr, (int)length, out stringBytes))
            {
                return false;
            }
        }

        value = System.Text.Encoding.UTF8.GetString(stringBytes).Replace('\\', '/').ToLowerInvariant();
        return value.Contains("vfx/") || value.Contains("/vfx/");
    }

    /// <summary>功能：读取目标进程内 64 位指针。</summary>
    private bool TryReadPointer(long address, out long pointer)
    {
        pointer = 0;
        if (!_reader.TryReadBytes(address, 8, out var bytes))
        {
            return false;
        }

        pointer = BitConverter.ToInt64(bytes, 0);
        return true;
    }

    /// <summary>功能：判断数值是否像用户态地址，过滤明显无效指针。</summary>
    private static bool LooksLikeUserModePointer(long value)
    {
        return value > 0x100000000L && value < 0x0000800000000000L;
    }

    /// <summary>功能：删除已经离开 active graph 一段时间的地址缓存，避免地址复用时 NEW 状态错误。</summary>
    private void PruneFirstSeenCache(IEnumerable<VfxMonitorEntry> entries, DateTime now, float retentionSeconds)
    {
        var active = new HashSet<long>(entries.Select(entry => entry.Address));
        foreach (var stale in _firstSeenByAddress.Where(pair => !active.Contains(pair.Key) && (now - pair.Value).TotalSeconds > retentionSeconds).Select(pair => pair.Key).ToList())
        {
            _firstSeenByAddress.Remove(stale);
        }
    }

    /// <summary>功能：保存 scene object 低成本遍历所需头部字段。</summary>
    private struct SceneObjectHeader
    {
        public long Address;
        public long VTable;
        public long NextSiblingObject;
        public long ChildObject;
        public Vector3 Position;
    }
}
