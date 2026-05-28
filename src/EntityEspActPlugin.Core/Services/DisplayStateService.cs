using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class DisplayStateService
{
    private readonly IEntitySource _entitySource;
    private readonly ICameraSource _cameraSource;

    public DisplayStateService(IEntitySource entitySource, ICameraSource cameraSource)
    {
        _entitySource = entitySource;
        _cameraSource = cameraSource;
    }

    public RuntimeDiagnosticsSnapshot LastDiagnostics { get; private set; } = RuntimeDiagnosticsSnapshot.Empty;
    public IReadOnlyList<EntitySnapshot> LastRawEntities { get; private set; } = Array.Empty<EntitySnapshot>();
    public IReadOnlyCollection<uint> PartyEntityIds { get; set; } = Array.Empty<uint>();
    public Action<IReadOnlyList<EntitySnapshot>>? OnRawEntitiesUpdated { get; set; }
    public Func<EntitySnapshot, bool>? IsEntityActiveByActLog { get; set; }
    public Func<IReadOnlyList<EntitySnapshot>, IReadOnlyList<EntitySnapshot>>? GetAdditionalEntitiesByActLog { get; set; }

    public IReadOnlyList<EntityDisplayState> BuildStates(int width, int height, EspConfig config)
    {
        var stopwatch = Stopwatch.StartNew();
        var camera = _cameraSource.GetCamera(width, height);
        var entities = _entitySource.GetEntities();
        LastRawEntities = entities.ToArray();
        OnRawEntitiesUpdated?.Invoke(LastRawEntities);
        var displayEntities = LastRawEntities;
        if (config.UseActLogActivityLifetime && GetAdditionalEntitiesByActLog != null)
        {
            var additionalEntities = GetAdditionalEntitiesByActLog(LastRawEntities);
            if (additionalEntities != null && additionalEntities.Count > 0)
            {
                displayEntities = LastRawEntities.Concat(additionalEntities).ToArray();
            }
        }

        var party = BuildPartyContext(displayEntities, PartyEntityIds);
        var states = EntityDisplayPipeline.BuildVisibleStates(
            displayEntities,
            camera,
            config,
            party,
            isEntityActiveByActLog: IsEntityActiveByActLog)
            .ToArray();
        stopwatch.Stop();

        var entityDiagnostics = _entitySource as IDiagnosticSource;
        var cameraDiagnostics = _cameraSource as IDiagnosticSource;
        var processDiagnostics = (_entitySource as IProcessMemoryDiagnosticSource) ?? (_cameraSource as IProcessMemoryDiagnosticSource);
        var patternScans = BuildPatternScans(_entitySource as IPatternDiagnosticSource, _cameraSource as IPatternDiagnosticSource);
        LastDiagnostics = new RuntimeDiagnosticsSnapshot
        {
            EntitySourceName = _entitySource.GetType().Name,
            CameraSourceName = _cameraSource.GetType().Name,
            CameraValid = camera.IsValid,
            ViewportWidth = width,
            ViewportHeight = height,
            RawEntityCount = LastRawEntities.Count,
            EntitySourceReady = entityDiagnostics?.IsReady ?? true,
            CameraSourceReady = cameraDiagnostics?.IsReady ?? camera.IsValid,
            SourceStatus = BuildSourceStatus(entityDiagnostics, cameraDiagnostics),
            ProcessMemory = processDiagnostics?.ProcessMemoryStatus,
            PatternScans = patternScans,
            VisibleStateCount = states.Length,
            FilteredDebugCount = states.Count(state => state.IsFilteredDebug),
            LastBuildMs = stopwatch.ElapsedMilliseconds,
            LastUpdatedAt = DateTime.Now,
        };

        return states;
    }

    public void SetLastRenderMs(long renderMs)
    {
        LastDiagnostics.LastRenderMs = renderMs;
        LastDiagnostics.LastUpdatedAt = DateTime.Now;
    }

    private static PartyContext BuildPartyContext(IReadOnlyList<EntitySnapshot> entities, IReadOnlyCollection<uint> trackedPartyEntityIds)
    {
        var self = entities.FirstOrDefault(entity => entity.IsSelf);
        var selfEntityId = self != null ? self.EntityId : 0;
        var partyEntityIds = new HashSet<uint>(trackedPartyEntityIds ?? Array.Empty<uint>());
        foreach (var entity in entities.Where(entity => entity.IsPartyMember))
        {
            partyEntityIds.Add(entity.EntityId);
        }

        return new PartyContext(selfEntityId, partyEntityIds);
    }

    private static string BuildSourceStatus(IDiagnosticSource? entityDiagnostics, IDiagnosticSource? cameraDiagnostics)
    {
        var parts = new List<string>();
        if (entityDiagnostics != null && !string.IsNullOrWhiteSpace(entityDiagnostics.Status))
        {
            parts.Add("Entity: " + entityDiagnostics.Status);
        }

        if (cameraDiagnostics != null && !string.IsNullOrWhiteSpace(cameraDiagnostics.Status))
        {
            parts.Add("Camera: " + cameraDiagnostics.Status);
        }

        return string.Join("; ", parts);
    }

    private static List<PatternScanResult> BuildPatternScans(IPatternDiagnosticSource? entityDiagnostics, IPatternDiagnosticSource? cameraDiagnostics)
    {
        var scans = new List<PatternScanResult>();
        if (entityDiagnostics != null)
        {
            scans.AddRange(entityDiagnostics.PatternScans);
        }

        if (cameraDiagnostics != null)
        {
            scans.AddRange(cameraDiagnostics.PatternScans);
        }

        return scans;
    }
}
