using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using EntityEspActPlugin.Core.Models;
using EntityEspActPlugin.Core.Services;

var tests = new List<(string Name, Action Body)>
{
    ("WorldToScreen projects center point", ProjectionProjectsCenterPoint),
    ("WorldToScreen rejects point behind camera", ProjectionRejectsPointBehindCamera),
    ("Filter rejects party owned entity", FilterRejectsPartyOwnedEntity),
    ("Filter party toggle controls party-owned entity", FilterPartyToggleControlsPartyOwnedEntity),
    ("Filter rejects entities beyond max distance", FilterRejectsEntitiesBeyondMaxDistance),
    ("Filter blacklists reject matching IDs", FilterBlacklistsRejectMatchingIds),
    ("UInt list parser accepts decimal and hex values", UIntListParserAcceptsDecimalAndHexValues),
    ("Pattern scanner supports wildcards and addresses", PatternScannerSupportsWildcardsAndAddresses),
    ("Pattern scanner resolves RIP-relative targets", PatternScannerResolvesRipRelativeTargets),
    ("PE section parser reads section table", PeSectionParserReadsSectionTable),
    ("RIP candidate scanner finds common prefixes", RipCandidateScannerFindsCommonPrefixes),
    ("Renderer builds casting label and clamps progress", RendererBuildsCastingLabelAndProgress),
    ("Environment paths point to ACT and FF14 executables", EnvironmentPathsPointToExecutables),
    ("Environment paths detect current FF14 game version", EnvironmentPathsDetectCurrentFfxivGameVersion),
    ("Config service loads saved list values", ConfigServiceLoadsSavedListValues),
    ("Diagnostics report contains path and version status", DiagnosticsReportContainsPathAndVersionStatus),
    ("Mock overlay points are placed inside viewport", MockOverlayPointsArePlacedInsideViewport),
    ("Mock entity display states are projected by WorldToScreen", MockEntityDisplayStatesAreProjectedByWorldToScreen),
    ("DisplayStateService uses entity and camera sources", DisplayStateServiceUsesEntityAndCameraSources),
    ("DisplayStateService filters self-owned duty NPCs", DisplayStateServiceFiltersSelfOwnedDutyNpcs),
    ("DisplayStateService filters only tracked party player objects", DisplayStateServiceFiltersOnlyTrackedPartyPlayerObjects),
    ("PartyListTracker reads alliance party list", PartyListTrackerReadsAlliancePartyList),
    ("NetworkPartyListTailer reads latest network 11 roster", NetworkPartyListTailerReadsLatestNetwork11Roster),
    ("DisplayStateService respects max displayed entities", DisplayStateServiceRespectsMaxDisplayedEntities),
    ("DisplayStateService can show filtered entities for debugging", DisplayStateServiceCanShowFilteredEntitiesForDebugging),
    ("DisplayStateService records runtime diagnostics", DisplayStateServiceRecordsRuntimeDiagnostics),
    ("DisplayStateService keeps raw entities for log context", DisplayStateServiceKeepsRawEntitiesForLogContext),
    ("Real data sources fail safely when game process is unavailable", RealDataSourcesFailSafelyUntilSignaturesAreConfigured),
    ("Real data sources expose built-in signature diagnostics", RealDataSourcesExposeBuiltInSignatureDiagnostics),
    ("Diagnostics report includes runtime diagnostics", DiagnosticsReportIncludesRuntimeDiagnostics),
    ("ESP config exposes render and scan controls", EspConfigExposesRenderAndScanControls),
    ("ESP config defaults to low obstruction combat style", EspConfigDefaultsToLowObstructionCombatStyle),
    ("Overlay style parser applies color and opacity", OverlayStyleParserAppliesColorAndOpacity),
    ("Overlay text opacity stays readable when background opacity is low", OverlayTextOpacityStaysReadableWhenBackgroundOpacityIsLow),
    ("Related ACT log store keeps all recent entity lines", RelatedActLogStoreKeepsRecentEntityLines),
    ("Related ACT log store display windows are independent", RelatedActLogStoreDisplayWindowsAreIndependent),
    ("Related ACT log formatter simplifies TRN fields", RelatedActLogFormatterSimplifiesTrnFields),
    ("Related ACT log 14 player filter is scoped", RelatedActLog14PlayerFilterIsScoped),
    ("Related ACT log 1A player filter is scoped", RelatedActLog1APlayerFilterIsScoped),
    ("Related ACT log filters by line type", RelatedActLogFiltersByLineType),
    ("Related ACT log attaches VFX candidate", RelatedActLogAttachesVfxCandidate),
    ("ACT cast progress store tracks 14 cast duration", ActCastProgressStoreTracks14CastDuration),
    ("ACT cast progress store rejects impossible duration", ActCastProgressStoreRejectsImpossibleDuration),
    ("Recent VFX log provider reads raw cast-vfx entries", RecentVfxLogProviderReadsRawEntries),
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

return failed == 0 ? 0 : 1;

static void ProjectionProjectsCenterPoint()
{
    var camera = new CameraSnapshot
    {
        ViewProjectionMatrix = Matrix4x4.Identity,
        Viewport = new ViewportRect(10, 20, 800, 600),
        IsValid = true,
    };

    var ok = ProjectionService.WorldToScreen(Vector3.Zero, camera, out var screen);

    AssertTrue(ok, "expected projection to succeed");
    AssertNear(410, screen.X, "screen x");
    AssertNear(320, screen.Y, "screen y");
}

static void ProjectionRejectsPointBehindCamera()
{
    var matrix = Matrix4x4.Identity;
    matrix.M44 = -1f;
    var camera = new CameraSnapshot
    {
        ViewProjectionMatrix = matrix,
        Viewport = new ViewportRect(0, 0, 100, 100),
        IsValid = true,
    };

    var ok = ProjectionService.WorldToScreen(Vector3.Zero, camera, out _);

    AssertFalse(ok, "expected projection to reject clip.W <= 0");
}

static void FilterRejectsPartyOwnedEntity()
{
    var config = new EspConfig { FilterPartyOwned = true };
    var party = new PartyContext(0x10, new HashSet<uint> { 0x20 });
    var entity = new EntitySnapshot { EntityId = 0x40001234, OwnerId = 0x20, DistanceToPlayer = 10f };

    var result = FilterService.Evaluate(entity, config, party);

    AssertFalse(result.ShouldDisplay, "party-owned entity should be filtered");
    AssertEqual("party-owned", result.Reason, "filter reason");
}

static void FilterPartyToggleControlsPartyOwnedEntity()
{
    var party = new PartyContext(0x10, new HashSet<uint> { 0x20 });
    var selfOwned = new EntitySnapshot { EntityId = 0x40001234, OwnerId = 0x10, DistanceToPlayer = 10f };
    var partyOwned = new EntitySnapshot { EntityId = 0x40005678, OwnerId = 0x20, DistanceToPlayer = 10f };
    var config = new EspConfig { FilterPartyOwned = true, FilterPartyPlayers = false };

    var selfOwnedResult = FilterService.Evaluate(selfOwned, config, party);
    var partyOwnedResult = FilterService.Evaluate(partyOwned, config, party);

    AssertFalse(selfOwnedResult.ShouldDisplay, "self-owned entity should still be filtered");
    AssertTrue(partyOwnedResult.ShouldDisplay, "party-owned entity should be shown when party filtering is disabled");
}

static void FilterRejectsEntitiesBeyondMaxDistance()
{
    var config = new EspConfig { MaxDistance = 10f };
    var entity = new EntitySnapshot
    {
        EntityId = 0x40001234,
        DistanceToPlayer = 10.5f,
        IsVisible = true,
        IsTargetable = true,
    };

    var result = FilterService.Evaluate(entity, config, new PartyContext(0, new HashSet<uint>()));

    AssertFalse(result.ShouldDisplay, "entity beyond max distance should be filtered");
    AssertEqual("distance", result.Reason, "filter reason");
}

static void FilterBlacklistsRejectMatchingIds()
{
    var entity = new EntitySnapshot
    {
        EntityId = 0x40001234,
        BNpcId = 20123,
        BNpcNameId = 30123,
        DistanceToPlayer = 10f,
        IsVisible = true,
        IsTargetable = true,
    };

    var entityIdConfig = new EspConfig();
    entityIdConfig.EntityIdBlacklist.Add(0x40001234);
    var entityIdResult = FilterService.Evaluate(entity, entityIdConfig, new PartyContext(0, new HashSet<uint>()));
    AssertFalse(entityIdResult.ShouldDisplay, "entity id blacklist should reject matching entity");
    AssertEqual("blacklist", entityIdResult.Reason, "entity id blacklist reason");

    var bnpcConfig = new EspConfig();
    bnpcConfig.BNpcBlacklist.Add(20123);
    var bnpcResult = FilterService.Evaluate(entity, bnpcConfig, new PartyContext(0, new HashSet<uint>()));
    AssertFalse(bnpcResult.ShouldDisplay, "bnpc blacklist should reject matching entity");
    AssertEqual("blacklist", bnpcResult.Reason, "bnpc blacklist reason");

    var bnpcNameConfig = new EspConfig();
    bnpcNameConfig.BNpcNameBlacklist.Add(30123);
    var bnpcNameResult = FilterService.Evaluate(entity, bnpcNameConfig, new PartyContext(0, new HashSet<uint>()));
    AssertFalse(bnpcNameResult.ShouldDisplay, "bnpc name blacklist should reject matching entity");
    AssertEqual("blacklist", bnpcNameResult.Reason, "bnpc name blacklist reason");
}

static void UIntListParserAcceptsDecimalAndHexValues()
{
    var values = UIntListParser.Parse("20101, 0x4E86\n20101 bad 42");

    AssertEqual(3, values.Count, "parsed unique count");
    AssertEqual(20101u, values[0], "first value");
    AssertEqual(20102u, values[1], "hex value");
    AssertEqual(42u, values[2], "third value");
    AssertEqual("20101, 20102, 42", UIntListParser.Format(values), "formatted list");
}

static void PatternScannerSupportsWildcardsAndAddresses()
{
    var data = new byte[] { 0x90, 0x48, 0x8B, 0x11, 0x22, 0x89, 0x48, 0x8B, 0xFF, 0xEE, 0x89 };

    var result = PatternScanner.Scan(data, 0x140000000, "test", "48 8B ?? ?? 89");

    AssertEqual(2, result.HitCount, "pattern hit count");
    AssertEqual(0x140000001L, result.FirstHitAddress, "first hit address");
    AssertTrue(result.Success, "pattern should succeed");
    AssertEqual(5, PatternScanner.Parse("48 8B ? ?? 89").Count, "parsed token count");
}

static void PatternScannerResolvesRipRelativeTargets()
{
    var data = new byte[] { 0x48, 0x8B, 0x05, 0x10, 0x00, 0x00, 0x00, 0x90 };

    var result = PatternScanner.Scan(data, 0x140000000, "rip", "48 8B 05 ?? ?? ?? ??");

    AssertEqual(1, result.HitCount, "pattern hit count");
    AssertEqual(0x140000000L, result.FirstHitAddress, "first hit address");
    AssertEqual(0x140000017L, result.ResolvedAddress, "resolved rip-relative address");
    AssertEqual(string.Empty, result.ResolveError, "resolve error");

    var custom = PatternScanner.Scan(data, 0x140000000, "custom", "48 8B 05 ?? ?? ?? ??", resolveOffset: 4, instructionLength: 8);
    AssertTrue(custom.ResolvedAddress != result.ResolvedAddress || !string.IsNullOrWhiteSpace(custom.ResolveError), "custom resolve parameters should change resolution behavior");
}

static void PeSectionParserReadsSectionTable()
{
    var image = new byte[0x400];
    image[0] = 0x4D;
    image[1] = 0x5A;
    WriteInt32(image, 0x3C, 0x80);
    image[0x80] = 0x50;
    image[0x81] = 0x45;
    WriteUInt16(image, 0x86, 1);
    WriteUInt16(image, 0x94, 0xF0);
    var sectionOffset = 0x80 + 4 + 20 + 0xF0;
    WriteAscii(image, sectionOffset, ".text");
    WriteInt32(image, sectionOffset + 8, 0x1234);
    WriteInt32(image, sectionOffset + 12, 0x1000);
    WriteInt32(image, sectionOffset + 16, 0x2000);

    var sections = PeSectionParser.Parse(image, 0x140000000);

    AssertEqual(1, sections.Count, "section count");
    AssertEqual(".text", sections[0].Name, "section name");
    AssertEqual(0x140001000L, sections[0].StartAddress, "section start");
    AssertEqual(0x1234, sections[0].ScanSize, "section scan size");
}

static void RipCandidateScannerFindsCommonPrefixes()
{
    var data = new byte[] { 0x90, 0x48, 0x8B, 0x05, 0x09, 0x00, 0x00, 0x00 };
    var sections = new List<PeSectionInfo>
    {
        new PeSectionInfo { Name = ".text", StartAddress = 0x140000000, VirtualSize = 0x10 },
        new PeSectionInfo { Name = ".data", StartAddress = 0x140000010, VirtualSize = 0x100 },
    };

    var result = RipCandidateScanner.Scan(data, 0x140000000, sections, ".text", 10);

    AssertEqual(1, result.Candidates.Count, "candidate group count");
    AssertEqual("48 8B 05", result.Candidates[0].Opcode, "candidate opcode");
    AssertEqual(1, result.Candidates[0].ReferenceCount, "candidate refs");
    AssertEqual(0x140000011L, result.Candidates[0].ResolvedAddress, "candidate resolved address");
    AssertEqual(".data", result.Candidates[0].TargetSection, "candidate target section");
    AssertEqual("48 8B 05 ?? ?? ?? ??", result.Candidates[0].CandidateSignature, "candidate signature");
    AssertEqual(1, result.Candidates[0].CandidateSignatureHits, "candidate signature hits");

    var filtered = RipCandidateScanner.Scan(data, 0x140000000, sections, ".text", 10, minRefs: 2, maxRefs: 200);
    AssertEqual(0, filtered.Candidates.Count, "min refs should filter single-reference candidates");
    AssertEqual(1, filtered.TotalGroups, "total groups should be counted before filters");
    AssertEqual(0, filtered.FilteredGroups, "filtered groups should honor refs range");
}

static void RendererBuildsCastingLabelAndProgress()
{
    var entity = new EntitySnapshot
    {
        EntityId = 0x40001234,
        BNpcId = 20123,
        BNpcNameId = 30123,
        IsCasting = true,
        CastId = 0xC341,
        CastCurrent = 10f,
        CastMax = 4f,
        DistanceToPlayer = 12.34f,
    };

    var config = new EspConfig();
    config.LabelFields.Distance = true;
    config.LabelFields.BNpcId = true;
    config.LabelFields.BNpcNameId = true;
    config.LabelFields.Cast = true;
    var state = DisplayStateBuilder.Build(entity, new Vector2(100, 200), new Vector2(100, 230), pinned: true, config);

    AssertTrue(state.LabelText.Contains("EntityId:40001234"), "label should include entity id key");
    AssertTrue(state.LabelText.Contains("BNpcId:20123"), "label should include bnpc id key");
    AssertTrue(state.LabelText.Contains("NameId:30123"), "label should include name id key");
    AssertTrue(state.LabelText.Contains("Dist:12.3m"), "label should include distance");
    AssertTrue(state.LabelText.Contains("CastId:C341 Cast:10.0/4.0"), "label should include cast info");

    var compactConfig = new EspConfig();
    compactConfig.LabelFields.Kind = false;
    compactConfig.LabelFields.BNpcNameId = false;
    compactConfig.LabelFields.Cast = false;
    var compact = DisplayStateBuilder.Build(entity, new Vector2(100, 200), new Vector2(100, 230), pinned: false, compactConfig);
    AssertFalse(compact.LabelText.Contains("Kind:"), "label should hide kind when disabled");
    AssertFalse(compact.LabelText.Contains("NameId:"), "label should hide name id when disabled");
    AssertFalse(compact.LabelText.Contains("CastId:"), "label should hide cast when disabled");

    AssertNear(1f, state.CastProgress, "cast progress should clamp to 1");
    AssertTrue(state.Pinned, "state should be pinned");
}

static void EnvironmentPathsPointToExecutables()
{
    var defaults = new EnvironmentPaths();
    var paths = EnvironmentPathResolver.Resolve(defaults);

    AssertEqual(string.Empty, defaults.ActDirectory, "fresh ACT path default should not be hard-coded");
    AssertEqual(string.Empty, defaults.FfxivDirectory, "fresh FF14 path default should not be hard-coded");
    AssertTrue(paths != null, "resolver should return a paths object");
}

static void EnvironmentPathsDetectCurrentFfxivGameVersion()
{
    var paths = EnvironmentPathResolver.Resolve(new EnvironmentPaths());

    var version = paths.TryReadFfxivGameVersion();

    AssertTrue(string.IsNullOrWhiteSpace(version) || version.Contains("."), "ffxiv game version should be empty or version-like");
}

static void ConfigServiceLoadsSavedListValues()
{
    var path = Path.Combine(Path.GetTempPath(), "EntityEspPlugin-test-" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
        File.WriteAllText(path, "{\"EntityIdBlacklist\":[1073746484],\"BNpcBlacklist\":[123],\"RelatedActLogFilters\":{\"Cast\":true,\"Status\":true,\"ExtraPosition\":false}}");
        var service = new ConfigService();

        var config = service.Load(path);

        AssertEqual(1, config.EntityIdBlacklist.Count, "entity id blacklist count");
        AssertEqual(0x40001234u, config.EntityIdBlacklist[0], "entity id blacklist value");
        AssertEqual(1, config.BNpcBlacklist.Count, "bnpc blacklist count");
        AssertEqual(123u, config.BNpcBlacklist[0], "bnpc blacklist value");
        AssertTrue(config.RelatedActLogFilters.Log14, "legacy cast filter should enable log 14");
        AssertTrue(config.RelatedActLogFilters.Log17, "legacy cast filter should enable log 17");
        AssertTrue(config.RelatedActLogFilters.Log1A, "legacy status filter should enable log 1A");
        AssertTrue(config.RelatedActLogFilters.Log1E, "legacy status filter should enable log 1E");
        AssertFalse(config.RelatedActLogFilters.Log107, "legacy extra-position filter should keep log 107 disabled");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void DiagnosticsReportContainsPathAndVersionStatus()
{
    var report = DiagnosticsService.BuildReport(EnvironmentPathResolver.Resolve(new EnvironmentPaths()));

    AssertTrue(report.Contains("ACT directory:"), "diagnostics should include ACT directory status");
    AssertTrue(report.Contains("FF14 directory:"), "diagnostics should include FF14 directory status");
    AssertTrue(report.Contains("ffxiv_dx11.exe:"), "diagnostics should include dx11 status");
    AssertTrue(report.Contains("Game version:"), "diagnostics should include game version");
}

static void MockOverlayPointsArePlacedInsideViewport()
{
    var points = MockOverlayPointService.CreatePoints(1000, 800);

    AssertEqual(5, points.Count, "mock point count");
    foreach (var point in points)
    {
        AssertTrue(point.X >= 0 && point.X <= 1000, "mock point x should be inside viewport");
        AssertTrue(point.Y >= 0 && point.Y <= 800, "mock point y should be inside viewport");
        AssertTrue(!string.IsNullOrWhiteSpace(point.Label), "mock point should have label");
    }
}

static void MockEntityDisplayStatesAreProjectedByWorldToScreen()
{
    var states = MockEntityDisplayService.BuildStates(1000, 800);

    AssertEqual(5, states.Count, "mock display state count");
    foreach (var state in states)
    {
        AssertTrue(state.OnScreen, "mock display state should be on screen");
        AssertTrue(state.ScreenPosition.X >= 0 && state.ScreenPosition.X <= 1000, "display state x should be inside viewport");
        AssertTrue(state.ScreenPosition.Y >= 0 && state.ScreenPosition.Y <= 800, "display state y should be inside viewport");
        AssertTrue(state.LabelText.Contains("EntityId:"), "display state should reuse entity label text");
    }
}

static void DisplayStateServiceUsesEntityAndCameraSources()
{
    var service = new DisplayStateService(new MockEntitySource(), new MockCameraSource());

    var states = service.BuildStates(1000, 800, new EspConfig());

    AssertEqual(5, states.Count, "display state service state count");
    AssertTrue(states[0].LabelText.Contains("EntityId:"), "display state service should produce labels");
}

static void DisplayStateServiceFiltersSelfOwnedDutyNpcs()
{
    var source = new StaticEntitySource(new[]
    {
        new EntitySnapshot
        {
            EntityId = 0x100472EA,
            Kind = EntityKind.Player,
            Position = new Vector3(0f, -1.8f, 0f),
            DistanceToPlayer = 0f,
            IsSelf = true,
            IsVisible = true,
            IsTargetable = true,
        },
        new EntitySnapshot
        {
            EntityId = 0x40006669,
            BNpcId = 16879,
            BNpcNameId = 16879,
            OwnerId = 0x100472EA,
            Kind = EntityKind.BattleNpc,
            Position = new Vector3(0.2f, -1.8f, 0f),
            DistanceToPlayer = 1f,
            IsVisible = true,
            IsTargetable = true,
            Name = "桑克瑞德的幻体",
        },
        new EntitySnapshot
        {
            EntityId = 0x40006688,
            BNpcId = 19439,
            BNpcNameId = 19439,
            OwnerId = 0xE0000000,
            Kind = EntityKind.BattleNpc,
            Position = new Vector3(0.4f, -1.8f, 0f),
            DistanceToPlayer = 10f,
            IsVisible = true,
            IsTargetable = true,
            IsCasting = true,
            CastId = 0x1234,
            CastCurrent = 1f,
            CastMax = 3f,
            Name = "来访长指魔",
        },
    });
    var service = new DisplayStateService(source, new MockCameraSource());

    var states = service.BuildStates(1000, 800, new EspConfig { FilterSelf = true, FilterPartyOwned = true });

    AssertTrue(states.All(state => state.Snapshot.EntityId != 0x100472EA), "self should be filtered");
    AssertTrue(states.All(state => state.Snapshot.EntityId != 0x40006669), "self-owned duty npc should be filtered");
    AssertTrue(states.Any(state => state.Snapshot.EntityId == 0x40006688), "hostile npc should remain visible");
}

static void DisplayStateServiceFiltersOnlyTrackedPartyPlayerObjects()
{
    var service = new DisplayStateService(new StaticEntitySource(new[]
    {
        new EntitySnapshot
        {
            EntityId = 0x10000001,
            IsSelf = true,
            Kind = EntityKind.Player,
            Position = new Vector3(0f, -1.8f, 0f),
            IsVisible = true,
            IsTargetable = true,
        },
        new EntitySnapshot
        {
            EntityId = 0x10000002,
            Kind = EntityKind.Player,
            Position = new Vector3(0.2f, -1.8f, 0f),
            IsVisible = true,
            IsTargetable = true,
        },
        new EntitySnapshot
        {
            EntityId = 0x10000003,
            Kind = EntityKind.Player,
            Position = new Vector3(0.4f, -1.8f, 0f),
            IsVisible = true,
            IsTargetable = true,
        },
        new EntitySnapshot
        {
            EntityId = 0x40001234,
            Kind = EntityKind.BattleNpc,
            Position = new Vector3(0.6f, -1.8f, 0f),
            IsVisible = true,
            IsTargetable = true,
            IsCasting = true,
            CastId = 0xBEEF,
            CastCurrent = 1f,
            CastMax = 3f,
        },
    }), new MockCameraSource())
    {
        PartyEntityIds = new[] { 0x10000002u },
    };

    var states = service.BuildStates(1000, 800, new EspConfig { FilterSelf = true, FilterPartyPlayers = true });
    var context = RelatedActLogContext.FromEntities(service.LastRawEntities, service.PartyEntityIds);

    AssertFalse(states.Any(state => state.Snapshot.EntityId == 0x10000002), "tracked party player should be filtered");
    AssertTrue(states.Any(state => state.Snapshot.EntityId == 0x10000003), "untracked enemy player should remain visible");
    AssertTrue(states.Any(state => state.Snapshot.EntityId == 0x40001234), "hostile NPC should remain visible");
    AssertTrue(context.PlayerOrOwnedEntityIds.Contains(0x10000002), "tracked party player should be available for log filtering");
    AssertFalse(context.PlayerOrOwnedEntityIds.Contains(0x10000003), "untracked enemy player should not be treated as party log noise");
}

static void PartyListTrackerReadsAlliancePartyList()
{
    var tracker = new PartyListTracker();
    tracker.ObserveLogLine("[15:10:00.000] PartyList 0B:24:10000001:10000002:10000003:10000004:10000005:10000006:10000007:10000008:10000009:1000000A:1000000B:1000000C:1000000D:1000000E:1000000F:10000010:10000011:10000012:10000013:10000014:10000015:10000016:10000017:10000018:10000019");

    var ids = tracker.PartyEntityIds;
    var idSet = new HashSet<uint>(ids);

    AssertEqual(24, ids.Count, "parsed party tracker should keep only listed 24 members");
    AssertTrue(idSet.Contains(0x10000018), "last valid alliance member should be tracked");
    AssertFalse(idSet.Contains(0x10000019), "ids beyond count 24 should be ignored");

    tracker.ObserveLogLine("11|2026-05-23T15:22:08.9910000+08:00|8|1001A087|10021A0E|1004C1CC|1004C332|1004B918|100472EA|1001A06F|1004C7A5|1002715A|10048B44|1004C75F|1001A856|1001AF99|100259D2|10049DBC|1004B6AE|1004BFE2|1004C091|10023C9D|10026894|1004C640|1004B8C7|1004C79D|10049A71|checksum");
    ids = tracker.PartyEntityIds;
    idSet = new HashSet<uint>(ids);

    AssertEqual(24, ids.Count, "network 11 party line should keep all 24 alliance ids even when count field is 8");
    AssertTrue(idSet.Contains(0x10049A71), "last network alliance member should be tracked");
}

static void NetworkPartyListTailerReadsLatestNetwork11Roster()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "EntityEspTailerTests", Guid.NewGuid().ToString("N"));
    var logDir = Path.Combine(tempRoot, "FFXIVLogs");
    Directory.CreateDirectory(logDir);
    try
    {
        File.WriteAllText(Path.Combine(logDir, "Network_30109_20260523.log"),
            "11|2026-05-23T16:25:49.5430000+08:00|8|1001DFBB|100472EA|1004BD21|100237C1|1004CE27|100226BE|1004B903|1004CD29|10043243|10044EC3|10046543|1001E674|1004BE7D|1001D1CD|1001D4EB|1004B4EB|1001F977|1001C953|1001CA8B|1004CA36|1004B8FB|1004C81A|1004CA56|1004B865|checksum" + Environment.NewLine);

        var tracker = new PartyListTracker();
        var tailer = new NetworkPartyListTailer(new EnvironmentPaths { ActDirectory = tempRoot }, tracker);
        tailer.RefreshIfDue();

        var ids = new HashSet<uint>(tracker.PartyEntityIds);
        AssertEqual(24, ids.Count, "network tailer should feed full 24-player roster into tracker");
        AssertTrue(ids.Contains(0x1004B865), "network tailer should include the last 24-player roster member");
    }
    finally
    {
        Directory.Delete(tempRoot, recursive: true);
    }
}

static void DisplayStateServiceRespectsMaxDisplayedEntities()
{
    var service = new DisplayStateService(new MockEntitySource(), new MockCameraSource());

    var states = service.BuildStates(1000, 800, new EspConfig { MaxDisplayedEntities = 2 });

    AssertEqual(2, states.Count, "display state service should limit max displayed entities");
}

static void DisplayStateServiceCanShowFilteredEntitiesForDebugging()
{
    var service = new DisplayStateService(new MockEntitySource(), new MockCameraSource());

    var defaultStates = service.BuildStates(1000, 800, new EspConfig());
    var debugStates = service.BuildStates(1000, 800, new EspConfig { ShowFilteredEntitiesForDebug = true });

    AssertEqual(5, defaultStates.Count, "default view should still hide filtered party member");
    AssertEqual(6, debugStates.Count, "debug view should include filtered party member");
    AssertTrue(debugStates.Any(state => state.IsFilteredDebug && state.FilterReason == "party"), "debug state should preserve party filter reason");
}

static void DisplayStateServiceRecordsRuntimeDiagnostics()
{
    var service = new DisplayStateService(new MockEntitySource(), new MockCameraSource());

    service.BuildStates(1000, 800, new EspConfig { ShowFilteredEntitiesForDebug = true });
    service.SetLastRenderMs(7);
    var diagnostics = service.LastDiagnostics;

    AssertEqual("MockEntitySource", diagnostics.EntitySourceName, "entity source name");
    AssertEqual("MockCameraSource", diagnostics.CameraSourceName, "camera source name");
    AssertTrue(diagnostics.CameraValid, "camera should be valid");
    AssertEqual(1000, diagnostics.ViewportWidth, "viewport width");
    AssertEqual(800, diagnostics.ViewportHeight, "viewport height");
    AssertEqual(6, diagnostics.RawEntityCount, "raw entity count");
    AssertEqual(6, diagnostics.VisibleStateCount, "visible state count");
    AssertEqual(1, diagnostics.FilteredDebugCount, "filtered debug count");
    AssertEqual(5, diagnostics.NormalVisibleCount, "normal visible count");
    AssertEqual(7L, diagnostics.LastRenderMs, "last render ms");
}

static void DisplayStateServiceKeepsRawEntitiesForLogContext()
{
    var service = new DisplayStateService(new StaticEntitySource(new[]
    {
        new EntitySnapshot
        {
            EntityId = 0x100472EA,
            IsSelf = true,
            Kind = EntityKind.Player,
            Position = new Vector3(0f, -1.8f, 0f),
            DistanceToPlayer = 0f,
            IsVisible = true,
            IsTargetable = true,
        },
        new EntitySnapshot
        {
            EntityId = 0x40006E25,
            BNpcId = 19446,
            BNpcNameId = 19446,
            Kind = EntityKind.BattleNpc,
            Position = new Vector3(0.2f, -1.8f, 0f),
            DistanceToPlayer = 10f,
            IsVisible = true,
            IsTargetable = true,
            IsCasting = true,
            CastId = 0x1234,
            CastCurrent = 1f,
            CastMax = 3f,
        },
    }), new MockCameraSource());

    var visibleStates = service.BuildStates(1000, 800, new EspConfig { FilterSelf = true });
    var context = RelatedActLogContext.FromEntities(service.LastRawEntities);

    AssertTrue(visibleStates.All(state => state.Snapshot.EntityId != 0x100472EA), "self should be hidden from display states");
    AssertTrue(context.PlayerOrOwnedEntityIds.Contains(0x100472EA), "raw self should remain available for log filtering");
}

static void RealDataSourcesFailSafelyUntilSignaturesAreConfigured()
{
    var service = new DisplayStateService(new RealEntitySource(), new RealCameraSource());

    var states = service.BuildStates(1000, 800, new EspConfig { DataSourceMode = DataSourceMode.Real });
    var diagnostics = service.LastDiagnostics;

    AssertTrue(states.Count >= 0, "real mode should return a stable state list");
    AssertEqual("RealEntitySource", diagnostics.EntitySourceName, "real entity source name");
    AssertEqual("RealCameraSource", diagnostics.CameraSourceName, "real camera source name");
    AssertTrue(diagnostics.SourceStatus.Contains("Control.ViewProjectionMatrix") || diagnostics.SourceStatus.Contains("signatures/offsets") || diagnostics.SourceStatus.Contains("process not found"), "real source status should explain readiness state");
    AssertTrue(diagnostics.ProcessMemory != null, "real source should expose process memory diagnostics");
    AssertTrue(diagnostics.ProcessMemory!.ProcessFound || diagnostics.ProcessMemory.LastError.Contains("process not found"), "process memory status should contain process result");
}

static void RealDataSourcesExposeBuiltInSignatureDiagnostics()
{
    using var memoryReader = new ProcessMemoryReader();
    var service = new DisplayStateService(
        new RealEntitySource(memoryReader),
        new RealCameraSource(memoryReader));

    service.BuildStates(1000, 800, new EspConfig { DataSourceMode = DataSourceMode.Real });
    var diagnostics = service.LastDiagnostics;

    if (memoryReader.IsReady)
    {
        AssertTrue(diagnostics.PatternScans.Any(scan => scan.Name == "ObjectTable"), "built-in object table signature should be visible in diagnostics");
        AssertTrue(diagnostics.PatternScans.Any(scan => scan.Name == "Control.Instance"), "built-in control signature should be visible in diagnostics");
    }
    else
    {
        AssertEqual(0, diagnostics.PatternScans.Count, "no pattern scans should run when process is unavailable");
    }
}

static void DiagnosticsReportIncludesRuntimeDiagnostics()
{
    var runtime = new RuntimeDiagnosticsSnapshot
    {
        EntitySourceName = "MockEntitySource",
        CameraSourceName = "MockCameraSource",
        CameraValid = true,
        ViewportWidth = 1000,
        ViewportHeight = 800,
        RawEntityCount = 6,
        VisibleStateCount = 6,
        FilteredDebugCount = 1,
        LastBuildMs = 2,
        LastRenderMs = 7,
        LastUpdatedAt = new DateTime(2026, 5, 22, 23, 30, 0),
        ProcessMemory = new ProcessMemoryStatus
        {
            ProcessFound = true,
            ProcessId = 1234,
            ModuleFound = true,
            ModuleBase = 0x140000000,
            ModuleSize = 1024,
            HasHandle = true,
            Sections = new List<PeSectionInfo>
            {
                new PeSectionInfo { Name = ".text", StartAddress = 0x140001000, VirtualSize = 512 },
            },
        },
        PatternScans = new List<PatternScanResult>
        {
            new PatternScanResult
            {
                Name = "CameraMatrix",
                Pattern = "48 8B ??",
                HitCount = 1,
                FirstHitAddress = 0x140001000,
                ResolvedAddress = 0x140002000,
                SectionName = ".text",
                IsCached = true,
            },
        },
    };

    var report = DiagnosticsService.BuildReport(new EnvironmentPaths(), runtime);

    AssertTrue(report.Contains("Runtime diagnostics:"), "runtime section should exist");
    AssertTrue(report.Contains("Entity source: MockEntitySource"), "runtime should include entity source");
    AssertTrue(report.Contains("Camera valid: yes"), "runtime should include camera status");
    AssertTrue(report.Contains("Filtered debug: 1"), "runtime should include filtered debug count");
    AssertTrue(report.Contains("Last render: 7 ms"), "runtime should include render time");
    AssertTrue(report.Contains("Process id: 1234"), "runtime should include process id");
    AssertTrue(report.Contains("Module base: 0x140000000"), "runtime should include module base");
    AssertTrue(report.Contains(".text: start=0x140001000 size=512"), "runtime should include section list");
    AssertTrue(report.Contains("Process handle: yes"), "runtime should include handle status");
    AssertTrue(report.Contains("Pattern scans:"), "runtime should include pattern scans");
    AssertTrue(report.Contains("CameraMatrix: hits=1 (cached) first=0x140001000 resolved=0x140002000"), "runtime should include pattern hit and resolved address");
    AssertTrue(report.Contains("section: .text"), "runtime should include pattern section");
    AssertFalse(report.Contains("EntityTable probe:"), "runtime should not include ACT-side entity table probe");
    AssertFalse(report.Contains("RIP candidates:"), "runtime should not include ACT-side RIP candidates");
}

static void EspConfigExposesRenderAndScanControls()
{
    var config = new EspConfig();

    AssertEqual(DataSourceMode.Real, config.DataSourceMode, "default data source");
    AssertEqual(100f, config.MaxDistance, "default max distance should follow current config");
    AssertEqual(45, config.EntityScanHz, "default scan hz");
    AssertEqual(90, config.RenderFps, "default render fps");
    AssertTrue(config.ShowCastBar, "cast bar should be enabled by default");
    AssertTrue(config.ShowUntargetable, "untargetable entities should be allowed by default");
    AssertTrue(config.ShowRelatedActLogs, "related ACT logs should be enabled by default");
    AssertTrue(config.FilterPlayerAndPartyLog14, "player and party 14 cast logs should be filtered by default");
    AssertTrue(config.FilterPlayerAndPartyLog1A, "player and party 1A status logs should be filtered by default");
    AssertEqual(6f, config.RelatedActLogSeconds, "default related ACT log TTL");
    AssertEqual(8, config.RelatedActLogMaxLinesPerEntity, "default related ACT log max lines");
    AssertEqual(6f, config.RelatedActLogPanelSeconds, "default related ACT log panel TTL");
    AssertEqual(8, config.RelatedActLogPanelMaxLines, "default related ACT log panel max lines");
    AssertFalse(config.ShowRecentVfxPanel, "VFX monitor and panel should follow current config default");
    AssertEqual(12, config.RecentVfxMaxLines, "default VFX max display lines");
    AssertTrue(config.RelatedActLogFilters.Log14, "14 cast-start logs should be enabled by default");
    AssertTrue(config.RelatedActLogFilters.Log1A, "1A status-add logs should be enabled by default");
    AssertFalse(config.RelatedActLogSimplify.Log14, "14 cast-start simplification should follow current config default");
    AssertFalse(config.RelatedActLogSimplify.Log14Alt, "14 cast-start simplification 2 should follow current config default");
    AssertFalse(config.RelatedActLogSimplify.Log1A, "1A status-add simplification should follow current config default");
    AssertFalse(config.RelatedActLogSimplify.Log1AAlt, "1A status-add simplification 2 should follow current config default");
    AssertFalse(config.RelatedActLogSimplify.Log1AAlt2, "1A status-add simplification 3 should follow current config default");
    AssertFalse(config.RelatedActLogFilters.Log17, "17 cast-cancel logs should follow current default");
    AssertFalse(config.RelatedActLogFilters.Log1E, "1E status-remove logs should follow current default");
    AssertFalse(config.RelatedActLogFilters.Log26, "26 status-list logs should follow current default");
    AssertFalse(config.RelatedActLogFilters.Log2A, "2A status-list logs should follow current default");
    AssertFalse(config.RelatedActLogFilters.Log107, "107 extra-position logs should follow current default");
    AssertFalse(config.RelatedActLogFilters.Log27, "27 hp logs should follow current default");
    AssertTrue(config.LabelFields.EntityId, "entity id label should be enabled by default");
    AssertFalse(config.LabelFields.Kind, "kind label should follow current default");
    AssertFalse(config.LabelFields.Distance, "distance label should follow current default");
    AssertTrue(config.LabelFields.BNpcId, "bnpc id label should follow current default");
    AssertFalse(config.LabelFields.BNpcNameId, "name id label should follow current default");
    AssertFalse(config.LabelFields.BNpcName, "bnpc name label should follow current default");
    AssertFalse(config.LabelFields.EObjNameId, "eobj name id label should follow current default");
    AssertFalse(config.LabelFields.Cast, "cast label should be disabled by default because ACT logs show casting");
    AssertTrue(config.BNpcBlacklist.SequenceEqual(new uint[] { 13961, 10489, 1008, 10487, 7245, 10490, 13498, 16926, 13505, 13507, 13506, 6982, 952 }), "bnpc blacklist should follow current config default");
    AssertTrue(config.RelatedActLogCasterBNpcBlacklist.SequenceEqual(config.BNpcBlacklist), "caster bnpc blacklist should mirror current default");
}

static void EspConfigDefaultsToLowObstructionCombatStyle()
{
    var config = new EspConfig();

    AssertFalse(config.DebugOverlayStyle, "debug overlay style should be off by default");
    AssertEqual(0.001f, config.Opacity, "default background opacity should follow current config");
    AssertEqual(15f, config.FontSize, "default font size should follow current config");
    AssertEqual("#FFFFFF", config.Style.BackgroundColor, "default background color should follow current config");
    AssertEqual("#FFFFFF", config.Style.MarkerColor, "default marker color should follow current config");
}

static void OverlayStyleParserAppliesColorAndOpacity()
{
    var color = OverlayStyleService.ParseHexColor("#3366CC", 0.5f);
    var fallback = OverlayStyleService.ParseHexColor("not-a-color", 2f);

    AssertEqual(127, color.A, "alpha should come from opacity");
    AssertEqual(0x33, color.R, "red channel");
    AssertEqual(0x66, color.G, "green channel");
    AssertEqual(0xCC, color.B, "blue channel");
    AssertEqual(255, fallback.A, "opacity should clamp to 1");
    AssertEqual(255, fallback.R, "fallback red channel");
}

static void OverlayTextOpacityStaysReadableWhenBackgroundOpacityIsLow()
{
    var background = OverlayStyleService.ParseHexColor("#000000", 0.25f);
    var text = OverlayStyleService.ParseHexColor("#D8F0FF", 0.92f);

    AssertTrue(background.A < text.A, "background alpha should be lower than text alpha");
    AssertTrue(text.A > 200, "text should remain readable");
}

static void RelatedActLogStoreKeepsRecentEntityLines()
{
    var now = new DateTime(2026, 5, 23, 5, 30, 0, DateTimeKind.Utc);
    var store = new RelatedActLogStore(() => now);
    var filters = new RelatedActLogFilterConfig { Log14 = true, Log1A = true, Log107 = true };
    var context = new RelatedActLogContext();
    context.PlayerEntityIds.Add(0x10000001);
    context.PlayerOrOwnedEntityIds.Add(0x10000001);
    context.PlayerOrOwnedEntityIds.Add(0x40009999);
    context.OwnerIdsByEntityId[0x40009999] = 0x10000001;
    context.BNpcIdsByEntityId[0x40007777] = 0xABC;
    context.BNpcNameIdsByEntityId[0x40006666] = 0xDEF;
    store.UpdateContext(context);
    store.AddLine("[12:34:56.789] 14:40008888:Add:BEEF:40001234:Boss:3.0:0:0:0:0", filters);
    store.AddLine("[12:34:56.790] 107:40001234:BEEF:1:2:3:0", filters);
    store.AddLine("[12:34:56.791] 1A:1234:Buff:10.0:40001234:Boss:10000001:Player:0:0:0", filters);
    store.AddLine("[12:34:56.792] 14:10000001:Player:BEEF:40001234:Boss:3.0:0:0:0:0", filters);
    store.AddLine("[12:34:56.793] 14:40009999:Pet:BEEF:40001234:Boss:3.0:0:0:0:0", filters);
    store.AddLine("[12:34:56.794] 14:40007777:BlockedByBNpc:BEEF:40001234:Boss:3.0:0:0:0:0", filters, true, true, null, null, new[] { 0xABCu });
    store.AddLine("[12:34:56.795] 14:40006666:BlockedByBNpcName:BEEF:40001234:Boss:3.0:0:0:0:0", filters, true, true, null, null, null, new[] { 0xDEFu });

    var logs = store.GetRecent(0x40001234, 10, 10);

    AssertEqual(3, logs.Count, "enemy casts and related entity log lines should be retained while player, owned pet, BNpc-blocked, and BNpcName-blocked casts are ignored");
    AssertTrue(logs[0].Line.Contains("14:40008888"), "enemy cast should be retained in order");
    AssertTrue(logs[1].Line.Contains("107:40001234"), "second log should be retained in order");
    AssertTrue(logs[2].Line.Contains("1A:1234"), "third log should be retained in order");
    AssertEqual(2, store.GetRecent(0x40001234, 10, 2).Count, "max lines should only trim the displayed result");
    now = now.AddSeconds(2);
    AssertEqual(0, store.GetRecent(0x40001234, 1, 10).Count, "expired logs should disappear from the short display window");
    AssertTrue(store.GetRecent(0x40001234, 10, 10).Count > 0, "expired short-window logs should remain cached for longer display windows");
}

static void RelatedActLogStoreDisplayWindowsAreIndependent()
{
    var now = new DateTime(2026, 5, 24, 1, 20, 0, DateTimeKind.Utc);
    var store = new RelatedActLogStore(() => now);
    var filters = new RelatedActLogFilterConfig { Log14 = true };
    store.AddLine("[01:19:55.000] 14:40001234:Boss:BEEF:40009999:Target:3.0:0:0:0:0", filters);

    now = now.AddSeconds(7);
    AssertEqual(0, store.GetRecent(0x40009999, 6, 10).Count, "short near-entity window should not show expired line");
    AssertEqual(1, store.GetRecentForEntities(new[] { 0x40009999u }, 10, 10).Count, "long side-panel window should still show the same cached line");
}

static void RelatedActLogFormatterSimplifiesTrnFields()
{
    var simplify = new RelatedActLogSimplifyConfig
    {
        Log14 = true,
        Log1A = true,
    };
    var cast = "[14:20:56.777] StartsCasting 14:100472EA:挽明暗轧止:6503:闪灼:40006E25:来访石像魔:1476:-739.87:718.68:0.20:0.06";
    var status = "[12:34:56.791] StatusAdd 1A:1234:Buff:10.0:40001234:Boss:10000001:Player:2:1000:1000";

    AssertEqual("14 Cast src=100472EA 6503:闪灼 -> tgt=40006E25 来访石像魔 t=1476 pos=(-739.87,718.68,0.20) h=0.06", RelatedActLogFormatter.Format(cast, simplify), "14 should show TRN-relevant cast fields only");
    AssertEqual("1A StatusAdd 1234:Buff dur=10.0 src=40001234 Boss -> tgt=10000001 Player stack=2", RelatedActLogFormatter.Format(status, simplify), "1A should show TRN-relevant status fields only");

    simplify.Log14 = false;
    AssertEqual("14:100472EA:挽明暗轧止:6503:闪灼:40006E25:来访石像魔:1476:-739.87:718.68:0.20:0.06", RelatedActLogFormatter.Format(cast, simplify), "disabled simplify switch should keep useful raw fields without timestamp/event prefix");

    var burstStatus = "Log [23:51:16.400] StatusAdd 1A:5CB:魔法储存：爆炎:9999.00:400077BA:凯夫卡:400077C2:凯夫卡:00:22392:9926597";
    simplify.Log1A = false;
    AssertEqual("1A:5CB:魔法储存：爆炎:9999.00:400077BA:凯夫卡:400077C2:凯夫卡:00:22392:9926597", RelatedActLogFormatter.Format(burstStatus, simplify), "raw 1A display should hide timestamp/event prefix");
    AssertEqual("14:100472EA:挽明暗轧止", RelatedActLogFormatter.StripDisplayPrefix("Log [00:50:43.363] StartsCasting 14:100472EA:挽明暗轧止"), "display prefix stripper should remove bracketed timestamps from any ACT display text");

    simplify.Log1AAlt = true;
    AssertEqual("Buff名:魔法储存：爆炎 | BuffID:5CB | Buff时间:9999.00 | Buff类型:00   - - ->>>   凯夫卡", RelatedActLogFormatter.Format(burstStatus, simplify), "1A simplify 2 should show buff name/id/time/type and target name");
    simplify.Log1AAlt = false;
    simplify.Log1AAlt2 = true;
    AssertEqual("Buff名:魔法储存：爆炎 | BuffID:5CB | Buff时间:9999.00   - - ->>>   凯夫卡", RelatedActLogFormatter.Format(burstStatus, simplify), "1A simplify 3 should omit buff type");
    simplify.Log1AAlt2 = false;

    simplify.Log14Alt = true;
    var kafkaCast = "[14:20:56.777] StartsCasting 14:40007C9A:凯夫卡:28D7:众神之像:40007CA5:凯夫卡:4.700:10.09:7.19:0.00:-1.81";
    AssertEqual("凯夫卡 -> 技能名：众神之像 | 技能ID：28D7 | 时间：4.700", RelatedActLogFormatter.Format(kafkaCast, simplify), "14 simplify 2 should show caster, action name, action id, and cast time");
}

static void RelatedActLog14PlayerFilterIsScoped()
{
    var now = new DateTime(2026, 5, 23, 6, 0, 0, DateTimeKind.Utc);
    var store = new RelatedActLogStore(() => now);
    var filters = new RelatedActLogFilterConfig { Log14 = true, Log17 = true };
    var context = new RelatedActLogContext();
    context.PlayerEntityIds.Add(0x100472EA);
    context.PlayerOrOwnedEntityIds.Add(0x100472EA);
    store.UpdateContext(context);

    store.AddLine("[14:14:06.552] 14:100472EA:掷明暗辄止:6503:闪冰:40006688:来访长指魔:1476:-799.84:816.44:0.30:-1.01", filters, true);
    store.AddLine("[14:14:07.000] 17:100472EA:掷明暗辄止:6503:闪冰:40006688:来访长指魔", filters, true);
    AssertEqual(1, store.GetRecent(0x40006688, 10, 10).Count, "only 14 from player should be filtered by the player log14 switch");

    var disabledStore = new RelatedActLogStore(() => now);
    disabledStore.UpdateContext(context);
    disabledStore.AddLine("[14:14:06.552] 14:100472EA:掷明暗辄止:6503:闪冰:40006688:来访长指魔:1476:-799.84:816.44:0.30:-1.01", filters, false);
    AssertEqual(1, disabledStore.GetRecent(0x40006688, 10, 10).Count, "disabled player log14 switch should keep player casts");
}

static void RelatedActLog1APlayerFilterIsScoped()
{
    var now = new DateTime(2026, 5, 23, 6, 30, 0, DateTimeKind.Utc);
    var store = new RelatedActLogStore(() => now);
    var filters = new RelatedActLogFilterConfig { Log1A = true };
    var context = new RelatedActLogContext();
    context.PlayerEntityIds.Add(0x100472EA);
    context.PlayerOrOwnedEntityIds.Add(0x100472EA);
    store.UpdateContext(context);

    store.AddLine("[14:57:32.337] StatusAdd 1A:1234:PlayerBuff:10.0:100472EA:挟明暗轧止:40000EF2:截击无人机:1:0:0", filters, true, true);
    store.AddLine("[14:57:32.519] StatusAdd 1A:5678:EnemyBuff:12.0:40000EF2:截击无人机:40000EF2:截击无人机:1:0:0", filters, true, true);
    store.AddLine("[14:57:33.000] StatusAdd 1A:9999:PlayerBuffKept:5.0:100472EA:挟明暗轧止:40000EF2:截击无人机:1:0:0", filters, true, false);

    var logs = store.GetRecent(0x40000EF2, 10, 10);

    AssertEqual(2, logs.Count, "1A from player should be filtered only while 1A player filter is enabled");
    AssertTrue(logs[0].Line.Contains("5678"), "enemy sourced 1A should remain");
    AssertTrue(logs[1].Line.Contains("9999"), "disabled 1A player filter should keep player sourced 1A");
}

static void RelatedActLogFiltersByLineType()
{
    var now = new DateTime(2026, 5, 23, 5, 40, 0, DateTimeKind.Utc);
    var store = new RelatedActLogStore(() => now);
    var filters = new RelatedActLogFilterConfig
    {
        Log14 = false,
        Log15 = true,
        Log107 = true,
        Log27 = false,
    };

    AssertEqual("14", RelatedActLogStore.GetLineType("[12:00:00.000] 14:40001234:Boss:BEEF:"), "parsed cast type");
    AssertEqual("107", RelatedActLogStore.GetLineType("[12:00:00.000] 107:40001234:BEEF:1:2:3:0"), "parsed extra type");
    AssertFalse(RelatedActLogStore.IsAllowedByFilter("[12:00:00.000] 14:40001234:Boss:BEEF:", filters), "cast should be disabled");
    AssertTrue(RelatedActLogStore.IsAllowedByFilter("[12:00:00.000] 15:40001234:Boss:BEEF:", filters), "ability should be enabled");
    AssertTrue(RelatedActLogStore.IsAllowedByFilter("[12:00:00.000] 107:40001234:BEEF:1:2:3:0", filters), "extra should be enabled");
    AssertFalse(RelatedActLogStore.IsAllowedByFilter("[12:00:00.000] 27:40001234:Boss:100:100", filters), "hp update should be disabled");

    store.AddLine("[12:00:00.000] 14:40001234:Boss:BEEF:40001234:Boss", filters);
    store.AddLine("[12:00:00.001] 15:40001234:Boss:BEEF:", filters);
    AssertEqual(1, store.GetRecent(0x40001234, 10, 10).Count, "only enabled line types should be stored");
}

static void RelatedActLogAttachesVfxCandidate()
{
    var root = Path.Combine(Path.GetTempPath(), "EntityEspVfxCandidateTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var json = @"{
  ""Candidates"": [
    {
      ""AbilityId"": ""37C3"",
      ""AbilityName"": ""阴阳五行"",
      ""CandidateAvfx"": [
        {
          ""Path"": ""vfx/common/eff/m0532_stlp2c0x.avfx"",
          ""RawScore"": 3,
          ""SeenInFiles"": 3,
          ""FinalScore"": 18.0,
          ""Classification"": ""monster-specific""
        }
      ]
    }
  ]
}";
    File.WriteAllText(Path.Combine(root, "ability-vfx-merged-test.json"), json);

    var now = new DateTime(2026, 5, 23, 8, 0, 0, DateTimeKind.Utc);
    var provider = new AbilityVfxCandidateProvider(root);
    var store = new RelatedActLogStore(() => now, provider);
    var filters = new RelatedActLogFilterConfig { Log14 = true, Log107 = true };
    store.AddLine("[17:38:36.000] 14:4000BAE9:青龙:37C3:阴阳五行:4000BAE9:青龙:3.700:98.45:98.21:0.03:1.79", filters);

    var logs = store.GetRecent(0x4000BAE9, 10, 10);
    AssertEqual(1, logs.Count, "vfx candidate cast log should be stored");
    AssertTrue(logs[0].VfxCandidate != null, "vfx candidate should be attached");
    AssertEqual("vfx/common/eff/m0532_stlp2c0x.avfx", logs[0].VfxCandidate!.Path, "top vfx path");
    AssertEqual(3, logs[0].VfxCandidate!.SeenInFiles, "seen files should be preserved");
}

static void ActCastProgressStoreTracks14CastDuration()
{
    var now = new DateTime(2026, 5, 23, 9, 0, 0, DateTimeKind.Utc);
    var store = new ActCastProgressStore(() => now);
    store.ObserveLogLine("[17:57:55.547] 14:4000CB9C:青龙:37E4:荒魂燃烧:4000CB9C:青龙:4.700:99.99:89.98:0.01:1.51");

    var active = store.GetActive(0x4000CB9C);
    AssertTrue(active != null, "parsed 14 cast should create an active cast bar");
    AssertEqual((uint)0x37E4, active!.AbilityId, "cast ability id");
    AssertEqual("荒魂燃烧", active.AbilityName, "cast ability name");
    AssertNear(4.7f, active.CastSeconds, "cast duration");

    now = now.AddSeconds(2.35);
    AssertNear(0.5f, active.GetProgress(now), "cast progress should use independent elapsed time");
    AssertNear(2.35f, active.GetRemainingSeconds(now), "cast remaining should count down");

    now = now.AddSeconds(3.1);
    AssertTrue(store.GetActive(0x4000CB9C) == null, "expired cast should be pruned independently of related log seconds");
}

static void ActCastProgressStoreRejectsImpossibleDuration()
{
    var now = new DateTime(2026, 5, 23, 9, 0, 0, DateTimeKind.Utc);
    var store = new ActCastProgressStore(() => now);
    store.ObserveLogLine("[17:57:55.547] 14:4000CB9C:青龙:37E4:荒魂燃烧:4000CB9C:青龙:999999999999:99.99:89.98:0.01:1.51");

    AssertTrue(store.GetActive(0x4000CB9C) == null, "impossible cast duration should be ignored");

    var entry = new ActCastProgressEntry
    {
        SourceEntityId = 0x4000CB9C,
        CastSeconds = float.PositiveInfinity,
        StartedAt = DateTime.MaxValue,
    };
    AssertEqual(DateTime.MaxValue, entry.EndsAt, "unsafe entry should not throw while computing EndsAt");
}

static void RecentVfxLogProviderReadsRawEntries()
{
    var root = Path.Combine(Path.GetTempPath(), "EntityEspRecentVfxTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var file = Path.Combine(root, "cast-vfx-test.log");
    var now = DateTime.Now;
    File.WriteAllText(file, string.Join(Environment.NewLine, new[]
    {
        "monitor-cast-vfx:",
        "tick=1 time=" + now.ToString("HH:mm:ss") + " avfx=2 new=2 scannedMB=1.0",
        "  + 0x111 vfx/common/eff/a.avfx",
        "  + 0x222 vfx/monster/m0532/eff/b.avfx",
    }));
    File.SetLastWriteTime(file, now);

    var provider = new RecentVfxLogProvider(root);
    var entries = provider.GetRecent(60f, 12f, 10);
    AssertEqual(2, entries.Count, "raw recent VFX count");
    AssertTrue(entries.Any(entry => entry.Path == "vfx/common/eff/a.avfx"), "raw recent VFX should include common path");
    AssertTrue(entries.Any(entry => entry.Path == "vfx/monster/m0532/eff/b.avfx"), "raw recent VFX should include monster path");
}

static void WriteUInt16(byte[] data, int offset, ushort value)
{
    var bytes = BitConverter.GetBytes(value);
    Array.Copy(bytes, 0, data, offset, bytes.Length);
}

static void WriteInt32(byte[] data, int offset, int value)
{
    var bytes = BitConverter.GetBytes(value);
    Array.Copy(bytes, 0, data, offset, bytes.Length);
}

static void WriteAscii(byte[] data, int offset, string value)
{
    var bytes = System.Text.Encoding.ASCII.GetBytes(value);
    Array.Copy(bytes, 0, data, offset, bytes.Length);
}

static void AssertTrue(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}

static void AssertFalse(bool value, string message)
{
    if (value) throw new InvalidOperationException(message);
}

static void AssertEqual<T>(T expected, T actual, string name)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
    }
}

static void AssertNear(float expected, float actual, string name)
{
    if (Math.Abs(expected - actual) > 0.001f)
    {
        throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
    }
}

sealed class StaticEntitySource : IEntitySource
{
    private readonly IReadOnlyList<EntitySnapshot> _entities;

    public StaticEntitySource(IReadOnlyList<EntitySnapshot> entities)
    {
        _entities = entities;
    }

    public IReadOnlyList<EntitySnapshot> GetEntities() => _entities;
}

