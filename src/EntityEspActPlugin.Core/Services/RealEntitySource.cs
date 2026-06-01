using System;
using System.Collections.Generic;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class RealEntitySource : IEntitySource, IProcessMemoryDiagnosticSource, IPatternDiagnosticSource
{
    private readonly ProcessMemoryReader _memoryReader;
    private readonly GameObjectTableReader _objectTableReader;
    private readonly CurrentTargetReader _targetReader;

    public RealEntitySource()
        : this(new ProcessMemoryReader())
    {
    }

    public RealEntitySource(ProcessMemoryReader memoryReader)
    {
        _memoryReader = memoryReader;
        _objectTableReader = new GameObjectTableReader(_memoryReader);
        _targetReader = new CurrentTargetReader(_memoryReader, new ControlCameraReader(_memoryReader));
    }

    public bool IsReady { get; private set; }
    public string Status { get; private set; } = "not sampled";
    public ProcessMemoryStatus? ProcessMemoryStatus => _memoryReader.Status;
    public IReadOnlyList<PatternScanResult> PatternScans { get; private set; } = new List<PatternScanResult>();

    public IReadOnlyList<EntitySnapshot> GetEntities()
    {
        if (!_memoryReader.IsReady)
        {
            // 功能：FF14 未启动或句柄暂不可用时定期重试，不在每个 EntityScanHz tick 重复 OpenProcess。
            _memoryReader.RefreshIfDue(TimeSpan.FromSeconds(1));
        }

        if (!_memoryReader.IsReady)
        {
            IsReady = false;
            Status = _memoryReader.Status.LastError;
            PatternScans = new List<PatternScanResult>();
            return new List<EntitySnapshot>();
        }

        var entities = _objectTableReader.ReadExperimentalObjectTable(out var matchedSlots);
        var hasCurrentTarget = _targetReader.TryReadCurrentTargetEntityId(out var currentTargetEntityId);
        if (hasCurrentTarget)
        {
            foreach (var entity in entities)
            {
                entity.IsCurrentTarget = entity.EntityId == currentTargetEntityId;
            }
        }

        var scans = new List<PatternScanResult>();
        if (_objectTableReader.LastObjectTableScan != null)
        {
            scans.Add(_objectTableReader.LastObjectTableScan);
        }

        PatternScans = scans;
        IsReady = entities.Count > 0;
        Status = IsReady
            ? _objectTableReader.LastStatus + "; entities=" + entities.Count + "; matchedSlots=" + matchedSlots + "; " + _targetReader.Status
            : "process ready; ObjectTable reader produced no entities; " + _objectTableReader.LastStatus + "; " + _targetReader.Status;
        return entities;
    }
}
