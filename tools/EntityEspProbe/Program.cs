using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EntityEspActPlugin.Core.Models;
using EntityEspActPlugin.Core.Services;
using Newtonsoft.Json;

namespace EntityEspProbe;

internal sealed class RosterCandidate
{
    public RosterCandidate(string type, string time, List<uint> entityIds, string line)
    {
        Type = type;
        Time = time;
        EntityIds = entityIds;
        Line = line;
    }

    public string Type { get; }
    public string Time { get; }
    public List<uint> EntityIds { get; }
    public string Line { get; }
}

internal sealed class RosterSnapshot
{
    public RosterSnapshot(string source, string time, List<uint> entityIds, string line)
    {
        Source = source;
        Time = time;
        EntityIds = entityIds;
        Line = line;
    }

    public string Source { get; }
    public string Time { get; }
    public List<uint> EntityIds { get; }
    public string Line { get; }
}

internal sealed class AvfxMemoryHit
{
    public AvfxMemoryHit(long address, string path)
    {
        Address = address;
        Path = path;
    }

    public long Address { get; }
    public string Path { get; }
}

internal sealed class CastVfxAnalysis
{
    public string SourceLog { get; set; } = string.Empty;
    public string GeneratedAt { get; set; } = string.Empty;
    public string Note { get; set; } = "Candidates are time-window correlations, not confirmed one-to-one mappings.";
    public List<CastVfxTickGroup> Groups { get; set; } = new List<CastVfxTickGroup>();
    public List<AbilityVfxCandidate> Candidates { get; set; } = new List<AbilityVfxCandidate>();
}

internal sealed class CastVfxTickGroup
{
    public int Tick { get; set; }
    public string Time { get; set; } = string.Empty;
    public int NewCount { get; set; }
    public List<string> Paths { get; set; } = new List<string>();
    public List<AbilitySummary> NearAbilities { get; set; } = new List<AbilitySummary>();
}

internal sealed class AbilitySummary
{
    public string SourceName { get; set; } = string.Empty;
    public string AbilityId { get; set; } = string.Empty;
    public string AbilityName { get; set; } = string.Empty;
    public int Count { get; set; }
}

internal sealed class AbilityVfxCandidate
{
    public string AbilityId { get; set; } = string.Empty;
    public string AbilityName { get; set; } = string.Empty;
    public Dictionary<string, int> SourceNames { get; set; } = new Dictionary<string, int>();
    public List<VfxPathScore> CandidateAvfx { get; set; } = new List<VfxPathScore>();
    public List<AbilityExample> Examples { get; set; } = new List<AbilityExample>();
}

internal sealed class VfxPathScore
{
    public string Path { get; set; } = string.Empty;
    public int Score { get; set; }
}

internal sealed class AbilityExample
{
    public int Tick { get; set; }
    public string Time { get; set; } = string.Empty;
    public int NearCount { get; set; }
}

internal sealed class CastLogEntry
{
    public DateTime Time { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string AbilityId { get; set; } = string.Empty;
    public string AbilityName { get; set; } = string.Empty;
}

internal sealed class MergedCastVfxAnalysis
{
    public string GeneratedAt { get; set; } = string.Empty;
    public List<string> SourceFiles { get; set; } = new List<string>();
    public string Note { get; set; } = "Merged scores rank repeated, monster-specific paths higher and generic/common/player/lockon paths lower. Candidate still needs confirmation.";
    public List<MergedAbilityVfxCandidate> Candidates { get; set; } = new List<MergedAbilityVfxCandidate>();
}

internal sealed class MergedAbilityVfxCandidate
{
    public string AbilityId { get; set; } = string.Empty;
    public string AbilityName { get; set; } = string.Empty;
    public Dictionary<string, int> SourceNames { get; set; } = new Dictionary<string, int>();
    public int SeenInFiles { get; set; }
    public List<MergedVfxPathScore> CandidateAvfx { get; set; } = new List<MergedVfxPathScore>();
}

internal sealed class MergedVfxPathScore
{
    public string Path { get; set; } = string.Empty;
    public int RawScore { get; set; }
    public int SeenInFiles { get; set; }
    public double Weight { get; set; }
    public double FinalScore { get; set; }
    public string Classification { get; set; } = string.Empty;
}

internal static class Program
{
    private const int GameObjectSize = 0x1A0;
    private const int EntityIdOffset = 0x78;
    private const int BaseIdOffset = 0x84;
    private const int OwnerIdOffset = 0x88;
    private const int ObjectIndexOffset = 0x8C;
    private const int ObjectKindOffset = 0x90;
    private const int PositionOffset = 0xB0;
    private const int NameOffset = 0x30;
    private const int ControlTargetSystemOffset = 0x190;
    private const int TargetSystemHardTargetOffset = 0x80;

    private static int Main(string[] args)
    {
        var command = args.Length > 0 && !LooksLikePath(args[0]) ? args[0] : "summary";
        if (string.Equals(command, "analyze-cast-vfx", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            AnalyzeCastVfxCommand(args);
            return 0;
        }

        if (string.Equals(command, "merge-cast-vfx", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            MergeCastVfxCommand(args);
            return 0;
        }

        using var reader = new ProcessMemoryReader();
        reader.Refresh();
        PrintProcessStatus(reader.Status);
        if (!reader.IsReady)
        {
            return 2;
        }

        if (string.Equals(command, "scan-objects", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            ScanObjects(reader);
            return 0;
        }

        if (string.Equals(command, "dump-table", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            DumpTableCommand(reader, args);
            return 0;
        }

        if (string.Equals(command, "read-entities", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            ReadEntitiesCommand(reader);
            return 0;
        }

        if (string.Equals(command, "read-camera", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            ReadCameraCommand(reader);
            return 0;
        }

        if (string.Equals(command, "read-party-log", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            ReadPartyLogCommand(reader);
            return 0;
        }

        if (string.Equals(command, "capture-duty", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            CaptureDutyCommand(reader, args);
            return 0;
        }

        if (string.Equals(command, "scan-avfx-memory", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            ScanAvfxMemoryCommand(reader, args);
            return 0;
        }

        if (string.Equals(command, "capture-cast-vfx", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            CaptureCastVfxCommand(reader, args);
            return 0;
        }

        if (string.Equals(command, "monitor-cast-vfx", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            MonitorCastVfxCommand(reader, args);
            return 0;
        }

        if (string.Equals(command, "read-target", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            ReadTargetCommand(reader);
            return 0;
        }

        if (string.Equals(command, "probe-character-vfx", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            ProbeCharacterVfxCommand(reader, args);
            return 0;
        }

        if (string.Equals(command, "probe-vfx-object", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            ProbeVfxObjectCommand(reader, args);
            return 0;
        }

        if (string.Equals(command, "probe-vfx-chain", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            ProbeVfxChainCommand(reader, args);
            return 0;
        }

        if (string.Equals(command, "probe-vfx-functions", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            ProbeVfxFunctionsCommand(reader);
            return 0;
        }

        if (string.Equals(command, "update-audit", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            UpdateAuditCommand(reader);
            return 0;
        }

        if (string.Equals(command, "build-states", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            BuildStatesCommand(reader);
            return 0;
        }

        if (string.Equals(command, "find-objecttable-refs", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            FindObjectTableRefsCommand(reader, args);
            return 0;
        }

        PrintSummary(reader);
        return 0;
    }

    private static bool LooksLikePath(string value)
    {
        return value.Contains("\\") || value.Contains("/") || value.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintSummary(ProcessMemoryReader reader)
    {
        Console.WriteLine();
        Console.WriteLine("EntityEspProbe commands:");
        Console.WriteLine("  read-entities           Read current ObjectTable entities via the built-in reader");
        Console.WriteLine("  read-camera             Read Control.Instance camera matrix via the built-in reader");
        Console.WriteLine("  read-party-log          Read latest ACT network party-list style lines and compare ObjectTable players");
        Console.WriteLine("  capture-duty 4|8|24     Capture roster/entity snapshot for 4/8/24-player duties");
        Console.WriteLine("  scan-avfx-memory [text] Scan readable client memory for real .avfx path strings");
        Console.WriteLine("  capture-cast-vfx [sec]  Diff loaded .avfx paths around a boss cast and print recent 14/107 lines");
        Console.WriteLine("  monitor-cast-vfx [sec] [interval] Monitor casts and newly loaded .avfx paths for a time window");
        Console.WriteLine("  analyze-cast-vfx [file] Analyze a cast-vfx capture log into ability-vfx candidates JSON");
        Console.WriteLine("  merge-cast-vfx [glob]  Merge ability-vfx candidate JSON files and rank likely paths");
        Console.WriteLine("  read-target             Read current hard target via Control.TargetSystem");
        Console.WriteLine("  probe-character-vfx     Probe Character+VfxContainer slots from ObjectTable entities");
        Console.WriteLine("  probe-vfx-object        Scan candidate VfxObject structs and follow resource path pointers");
        Console.WriteLine("  probe-vfx-chain <addr>  Reverse-probe references from a known .avfx string address");
        Console.WriteLine("  update-audit            Run post-patch checks for paths, signatures, offsets, entities, camera, VFX");
        Console.WriteLine("  build-states            Project visible display states with current real data");
        Console.WriteLine("  scan-objects            Scan .data for GameObject pointer arrays");
        Console.WriteLine("  find-objecttable-refs   Locate .text RIP references to a verified ObjectTable address");
        Console.WriteLine();
        Console.WriteLine("Fast GameObject pointer heuristics:");
        ProbeGameObjectPointersFromDataSection(reader, maxPointers: 64, maxPrinted: 20);
    }

    private static void ScanObjects(ProcessMemoryReader reader)
    {
        Console.WriteLine("scan-objects:");
        Console.WriteLine("  scanning .data for pointer arrays and validating pointed values as FFXIVClientStructs GameObject layout");
        var data = reader.Status.Sections.FirstOrDefault(section => string.Equals(section.Name, ".data", StringComparison.OrdinalIgnoreCase));
        if (data == null || data.ScanSize <= 0)
        {
            Console.WriteLine("  .data section not found");
            return;
        }

        var bytesToRead = Math.Min(data.ScanSize, 0x600000);
        if (!reader.TryReadBytes(data.StartAddress, bytesToRead, out var bytes))
        {
            Console.WriteLine("  .data read failed: " + reader.Status.LastError);
            return;
        }

        var candidates = new List<ObjectPointerArrayCandidate>();
        for (var offset = 0; offset <= bytes.Length - 8 * 8; offset += 8)
        {
            var candidate = ScorePointerArray(reader, data.StartAddress + offset, bytes, offset, maxEntries: 96);
            if (candidate.ValidObjects >= 3 && candidate.Score >= 10)
            {
                candidates.Add(candidate);
            }
        }

        foreach (var candidate in candidates)
        {
            FinalizePointerArrayCandidate(candidate);
        }

        var top = candidates
            .Where(candidate => candidate.AlignmentHits >= 3 && candidate.ExpectedBaseAddress >= data.StartAddress && candidate.ExpectedBaseAddress < data.StartAddress + bytesToRead)
            .GroupBy(candidate => candidate.ExpectedBaseAddress)
            .Select(group => group.OrderByDescending(candidate => candidate.Score).ThenByDescending(candidate => candidate.AlignmentHits).ThenByDescending(candidate => candidate.ValidObjects).First())
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.AlignmentHits)
            .ThenByDescending(candidate => candidate.ValidObjects)
            .Take(20)
            .ToList();

        Console.WriteLine("  rawCandidates=" + candidates.Count + " deduped=" + top.Count);
        foreach (var candidate in top)
        {
            Console.WriteLine("  tableBase=.data+0x" + (candidate.ExpectedBaseAddress - data.StartAddress).ToString("X") + " addr=0x" + candidate.ExpectedBaseAddress.ToString("X") + " window=.data+0x" + (candidate.Address - data.StartAddress).ToString("X") + " valid=" + candidate.ValidObjects + " align=" + candidate.AlignmentHits + " span=" + candidate.Span + " score=" + candidate.Score);
            foreach (var obj in candidate.Objects.Take(8))
            {
                PrintObjectLine("    ", obj);
            }

            DumpTableSlots(reader, candidate.ExpectedBaseAddress, 0, 12);
            DumpTableSlots(reader, candidate.ExpectedBaseAddress, 480, 16);
        }

        if (top.Count == 0)
        {
            Console.WriteLine("  no pointer-array candidates found; next step: scan heap regions or derive ObjectTable signature from Dalamud/FFXIVClientStructs");
        }
    }

    private static void FindObjectTableRefsCommand(ProcessMemoryReader reader, string[] args)
    {
        var target = args.Length > 1 ? ParseAddress(reader, args[1]) : reader.Status.ModuleBase + GameObjectTableReader.ExperimentalObjectTableOffset;
        var text = reader.Status.Sections.FirstOrDefault(section => string.Equals(section.Name, ".text", StringComparison.OrdinalIgnoreCase));
        Console.WriteLine("find-objecttable-refs:");
        Console.WriteLine("  target=0x" + target.ToString("X") + " module+0x" + (target - reader.Status.ModuleBase).ToString("X"));
        if (text == null || text.ScanSize <= 0)
        {
            Console.WriteLine("  .text section not found");
            return;
        }

        if (!reader.TryReadBytes(text.StartAddress, text.ScanSize, out var bytes))
        {
            Console.WriteLine("  .text read failed: " + reader.Status.LastError);
            return;
        }

        var refs = new List<RipRefCandidate>();
        for (var i = 0; i <= bytes.Length - 7; i++)
        {
            if (!IsRipRelativeCandidate(bytes, i, out var instructionLength, out var op))
            {
                continue;
            }

            var instruction = text.StartAddress + i;
            var displacement = BitConverter.ToInt32(bytes, i + 3);
            var resolved = instruction + instructionLength + displacement;
            if (resolved != target)
            {
                continue;
            }

            refs.Add(new RipRefCandidate
            {
                Instruction = instruction,
                Offset = i,
                InstructionLength = instructionLength,
                Op = op,
                Signature = BuildSignature(bytes, i, 16),
            });
        }

        Console.WriteLine("  refs=" + refs.Count);
        foreach (var candidate in refs.Take(40))
        {
            var scan = reader.ScanMainModule("ObjectTableRef", candidate.Signature, 3, candidate.InstructionLength, ".text");
            Console.WriteLine("  first=0x" + candidate.Instruction.ToString("X") + " text+0x" + candidate.Offset.ToString("X") + " op=" + candidate.Op + " len=" + candidate.InstructionLength + " sigHits=" + scan.HitCount + " resolved=0x" + scan.ResolvedAddress.ToString("X"));
            Console.WriteLine("    sig=\"" + candidate.Signature + "\"");
        }
    }

    private static bool IsRipRelativeCandidate(byte[] bytes, int offset, out int instructionLength, out string op)
    {
        instructionLength = 7;
        op = string.Empty;
        if (bytes[offset] == 0x48 && (bytes[offset + 1] == 0x8B || bytes[offset + 1] == 0x89 || bytes[offset + 1] == 0x8D) && bytes[offset + 2] == 0x05)
        {
            op = bytes[offset + 1] == 0x8B ? "48 8B 05" : bytes[offset + 1] == 0x89 ? "48 89 05" : "48 8D 05";
            return true;
        }

        if (bytes[offset] == 0x48 && bytes[offset + 1] == 0x8D && bytes[offset + 2] == 0x0D)
        {
            op = "48 8D 0D";
            return true;
        }

        if (bytes[offset] == 0x4C && bytes[offset + 1] == 0x8D && bytes[offset + 2] >= 0x05 && bytes[offset + 2] <= 0x3D)
        {
            op = "4C 8D " + bytes[offset + 2].ToString("X2");
            return true;
        }

        return false;
    }

    private static string BuildSignature(byte[] bytes, int offset, int length)
    {
        var parts = new List<string>();
        var end = Math.Min(bytes.Length, offset + length);
        for (var i = offset; i < end; i++)
        {
            parts.Add(i >= offset + 3 && i < offset + 7 ? "??" : bytes[i].ToString("X2"));
        }

        return string.Join(" ", parts);
    }

    private static void ReadCameraCommand(ProcessMemoryReader reader)
    {
        var source = new RealCameraSource(reader);
        var camera = source.GetCamera(1839, 1185);
        Console.WriteLine("read-camera:");
        Console.WriteLine("  ready=" + source.IsReady + " valid=" + camera.IsValid + " status=" + source.Status);
        Console.WriteLine("  matrix=[" + FormatFloat(camera.ViewProjectionMatrix.M11) + " " + FormatFloat(camera.ViewProjectionMatrix.M12) + " " + FormatFloat(camera.ViewProjectionMatrix.M13) + " " + FormatFloat(camera.ViewProjectionMatrix.M14) + "; "
            + FormatFloat(camera.ViewProjectionMatrix.M21) + " " + FormatFloat(camera.ViewProjectionMatrix.M22) + " " + FormatFloat(camera.ViewProjectionMatrix.M23) + " " + FormatFloat(camera.ViewProjectionMatrix.M24) + "; "
            + FormatFloat(camera.ViewProjectionMatrix.M31) + " " + FormatFloat(camera.ViewProjectionMatrix.M32) + " " + FormatFloat(camera.ViewProjectionMatrix.M33) + " " + FormatFloat(camera.ViewProjectionMatrix.M34) + "; "
            + FormatFloat(camera.ViewProjectionMatrix.M41) + " " + FormatFloat(camera.ViewProjectionMatrix.M42) + " " + FormatFloat(camera.ViewProjectionMatrix.M43) + " " + FormatFloat(camera.ViewProjectionMatrix.M44) + "]");
    }

    private static void ReadPartyLogCommand(ProcessMemoryReader reader)
    {
        Console.WriteLine("read-party-log:");
        var logFile = FindLatestNetworkLog();
        if (logFile == null)
        {
            Console.WriteLine("  network log not found");
        }
        else
        {
            Console.WriteLine("  log=" + logFile.FullName);
            var tailLines = ReadTailLines(logFile.FullName, 50000).ToList();
            var rosterCandidates = tailLines
                .Select(ParseRosterCandidate)
                .Where(candidate => candidate != null && candidate.EntityIds.Count >= 8)
                .Cast<RosterCandidate>()
                .GroupBy(candidate => candidate.Type)
                .Select(group => group.Last())
                .OrderByDescending(candidate => candidate.EntityIds.Count)
                .ThenBy(candidate => candidate.Type)
                .Take(20)
                .ToList();
            Console.WriteLine("  roster candidate types=" + rosterCandidates.Count);
            foreach (var candidate in rosterCandidates)
            {
                Console.WriteLine("  type=" + candidate.Type + " time=" + candidate.Time + " ids=" + candidate.EntityIds.Count + " sample=" + string.Join(", ", candidate.EntityIds.Take(12).Select(id => "0x" + id.ToString("X8"))));
                Console.WriteLine("    line=" + Trim(candidate.Line, 260));
            }
        }

        var source = new RealEntitySource(reader);
        var entities = source.GetEntities();
        var players = entities.Where(entity => entity.Kind == EntityKind.Player).OrderBy(entity => entity.DistanceToPlayer).ToList();
        Console.WriteLine("  objectTable players=" + players.Count + " sourceReady=" + source.IsReady + " status=" + source.Status);
        foreach (var entity in players.Take(80))
        {
            Console.WriteLine("  player id=0x" + entity.EntityId.ToString("X8") + " dist=" + FormatFloat(entity.DistanceToPlayer) + " owner=0x" + entity.OwnerId.ToString("X8") + " self=" + entity.IsSelf + " name='" + entity.Name.Replace("'", "?") + "'");
        }
    }

    private static RosterCandidate? ParseRosterCandidate(string line)
    {
        var fields = line.Split('|');
        if (fields.Length < 3)
        {
            return null;
        }

        var ids = new HashSet<uint>();
        for (var i = 2; i < fields.Length; i++)
        {
            var token = fields[i];
            if (token.Length == 8 && IsLikelyEntityIdToken(token) && uint.TryParse(token, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var id))
            {
                ids.Add(id);
            }
        }

        return new RosterCandidate(fields[0], fields.Length > 1 ? fields[1] : string.Empty, ids.ToList(), line);
    }

    private static bool IsRosterLineType(string type)
    {
        return string.Equals(type, "11", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "0B", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyEntityIdToken(string token)
    {
        return token.Length == 8 && (token[0] == '1' || token[0] == '4') && token.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'));
    }

    private static string Trim(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
    }

    private static FileInfo? FindLatestNetworkLog()
    {
        var actDir = EnvironmentPathResolver.FindActDirectory();
        var dirs = new[]
        {
            string.IsNullOrWhiteSpace(actDir) ? string.Empty : Path.Combine(actDir, "FFXIVLogs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Advanced Combat Tracker", "FFXIVLogs"),
        };
        return dirs
            .Where(dir => !string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            .SelectMany(dir => new DirectoryInfo(dir).GetFiles("Network_*.log"))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static IEnumerable<string> ReadTailLines(string path, int maxLines)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var maxBytes = Math.Min(stream.Length, 4 * 1024 * 1024);
            stream.Seek(-maxBytes, SeekOrigin.End);
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            return text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(line => line.Length > 0)
                .Reverse()
                .Take(maxLines)
                .Reverse()
                .ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine("  log tail read failed: " + ex.GetType().Name + ": " + ex.Message);
            return Array.Empty<string>();
        }
    }

    private static string Field(string[] fields, int index)
    {
        return index >= 0 && index < fields.Length ? fields[index] : string.Empty;
    }

    private static void CaptureDutyCommand(ProcessMemoryReader reader, string[] args)
    {
        var expected = args.Length > 1 && int.TryParse(args[1], out var parsed) ? parsed : 0;
        if (expected != 4 && expected != 8 && expected != 24)
        {
            Console.WriteLine("capture-duty:");
            Console.WriteLine("  usage: capture-duty 4|8|24");
            return;
        }

        Console.WriteLine("capture-duty:");
        Console.WriteLine("  expectedRoster=" + expected);
        var roster = ReadLatestRoster(expected);
        Console.WriteLine("  rosterSource=" + roster.Source + " rosterCount=" + roster.EntityIds.Count + " time=" + roster.Time);
        Console.WriteLine("  rosterIds=" + (roster.EntityIds.Count == 0 ? "<none>" : string.Join(", ", roster.EntityIds.Select(id => "0x" + id.ToString("X8")))));
        if (!string.IsNullOrEmpty(roster.Line))
        {
            Console.WriteLine("  rosterLine=" + Trim(roster.Line, 320));
        }

        var source = new RealEntitySource(reader);
        var entities = source.GetEntities();
        var players = entities.Where(entity => entity.Kind == EntityKind.Player).OrderBy(entity => entity.DistanceToPlayer).ToList();
        var npcs = entities.Where(entity => entity.Kind == EntityKind.BattleNpc).OrderBy(entity => entity.DistanceToPlayer).ToList();
        var rosterSet = new HashSet<uint>(roster.EntityIds);
        Console.WriteLine("  objectTable sourceReady=" + source.IsReady + " status=" + source.Status);
        Console.WriteLine("  objectTable total=" + entities.Count + " players=" + players.Count + " battleNpcs=" + npcs.Count);
        Console.WriteLine("  matchedRosterPlayers=" + players.Count(player => rosterSet.Contains(player.EntityId)) + " / " + roster.EntityIds.Count);
        foreach (var player in players.Take(80))
        {
            var tag = rosterSet.Contains(player.EntityId) ? "party" : player.IsSelf ? "self" : "other";
            Console.WriteLine("  player[" + tag + "] id=0x" + player.EntityId.ToString("X8") + " dist=" + FormatFloat(player.DistanceToPlayer) + " owner=0x" + player.OwnerId.ToString("X8") + " name='" + player.Name.Replace("'", "?") + "'");
        }

        foreach (var npc in npcs.Take(40))
        {
            Console.WriteLine("  npc id=0x" + npc.EntityId.ToString("X8") + " bnpc=0x" + npc.BNpcId.ToString("X") + " nameId=0x" + npc.BNpcNameId.ToString("X") + " owner=0x" + npc.OwnerId.ToString("X8") + " dist=" + FormatFloat(npc.DistanceToPlayer) + " name='" + npc.Name.Replace("'", "?") + "'");
        }
    }

    private static RosterSnapshot ReadLatestRoster(int expected)
    {
        var logFile = FindLatestNetworkLog();
        if (logFile == null)
        {
            return new RosterSnapshot("<none>", string.Empty, new List<uint>(), string.Empty);
        }

        var candidates = ReadTailLines(logFile.FullName, 100000)
            .Select(ParseRosterCandidate)
            .Where(candidate => candidate != null && IsRosterLineType(candidate.Type) && candidate.EntityIds.Count > 0)
            .Cast<RosterCandidate>()
            .ToList();
        var exact = candidates.LastOrDefault(candidate => candidate.EntityIds.Count == expected);
        if (exact != null)
        {
            return new RosterSnapshot("network type " + exact.Type, exact.Time, exact.EntityIds, exact.Line);
        }

        var best = candidates
            .OrderBy(candidate => Math.Abs(candidate.EntityIds.Count - expected))
            .ThenByDescending(candidate => candidate.EntityIds.Count)
            .LastOrDefault();
        return best == null
            ? new RosterSnapshot(logFile.FullName, string.Empty, new List<uint>(), string.Empty)
            : new RosterSnapshot("network type " + best.Type + " (nearest)", best.Time, best.EntityIds, best.Line);
    }

    private static void ScanAvfxMemoryCommand(ProcessMemoryReader reader, string[] args)
    {
        var filter = args.Length > 1 ? args[1] : string.Empty;
        var maxPrinted = args.Length > 2 && int.TryParse(args[2], out var parsedLimit) ? Math.Max(1, parsedLimit) : 80;
        Console.WriteLine("scan-avfx-memory:");
        Console.WriteLine("  filter=" + (string.IsNullOrWhiteSpace(filter) ? "<none>" : filter));
        var hits = ScanAvfxPaths(reader, filter, out var scannedBytes, out var regionCount);
        Console.WriteLine("  readableRegions=" + regionCount);
        Console.WriteLine("  scannedMB=" + (scannedBytes / 1024.0 / 1024.0).ToString("0.0"));
        Console.WriteLine("  uniqueAvfx=" + hits.Count);
        foreach (var item in hits.OrderBy(item => item.Key).Take(maxPrinted))
        {
            Console.WriteLine("  0x" + item.Value.ToString("X") + " " + item.Key);
        }
    }

    private static void CaptureCastVfxCommand(ProcessMemoryReader reader, string[] args)
    {
        var seconds = args.Length > 1 && int.TryParse(args[1], out var parsedSeconds) ? Math.Max(1, Math.Min(30, parsedSeconds)) : 8;
        Console.WriteLine("capture-cast-vfx:");
        Console.WriteLine("  durationSeconds=" + seconds);
        var before = ScanAvfxPaths(reader, string.Empty, out var beforeBytes, out var beforeRegions);
        var startTime = DateTime.UtcNow;
        Console.WriteLine("  beforeAvfx=" + before.Count + " scannedMB=" + (beforeBytes / 1024.0 / 1024.0).ToString("0.0") + " regions=" + beforeRegions);
        System.Threading.Thread.Sleep(seconds * 1000);
        var after = ScanAvfxPaths(reader, string.Empty, out var afterBytes, out var afterRegions);
        Console.WriteLine("  afterAvfx=" + after.Count + " scannedMB=" + (afterBytes / 1024.0 / 1024.0).ToString("0.0") + " regions=" + afterRegions);
        PrintNewAvfxPaths(before, after, 120, "  + ");
        PrintRecentCastLines(startTime.AddSeconds(-2), DateTime.UtcNow.AddSeconds(1));
    }

    private static void MonitorCastVfxCommand(ProcessMemoryReader reader, string[] args)
    {
        var seconds = args.Length > 1 && int.TryParse(args[1], out var parsedSeconds) ? Math.Max(0, Math.Min(86400, parsedSeconds)) : 0;
        var intervalSeconds = args.Length > 2 && int.TryParse(args[2], out var parsedInterval) ? Math.Max(1, Math.Min(60, parsedInterval)) : 2;
        var outputDir = Path.Combine("tools", "EntityEspProbe", "captures");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.GetFullPath(Path.Combine(outputDir, "cast-vfx-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log"));
        using var writer = new StreamWriter(outputPath, append: false);
        void WriteBoth(string text)
        {
            Console.WriteLine(text);
            writer.WriteLine(text);
            writer.Flush();
        }

        WriteBoth("monitor-cast-vfx:");
        WriteBoth("  durationSeconds=" + seconds + " intervalSeconds=" + intervalSeconds);
        WriteBoth("  output=" + outputPath);
        var known = ScanAvfxPaths(reader, string.Empty, out var scannedBytes, out var regionCount);
        WriteBoth("  baselineAvfx=" + known.Count + " scannedMB=" + (scannedBytes / 1024.0 / 1024.0).ToString("0.0") + " regions=" + regionCount);
        var start = DateTime.UtcNow;
        var end = seconds <= 0 ? DateTime.MaxValue : start.AddSeconds(seconds);
        var seenLogLines = new HashSet<string>(StringComparer.Ordinal);
        var tick = 0;
        while (DateTime.UtcNow < end)
        {
            tick++;
            System.Threading.Thread.Sleep(intervalSeconds * 1000);
            var tickStart = DateTime.UtcNow;
            var current = ScanAvfxPaths(reader, string.Empty, out var tickBytes, out var tickRegions);
            var newPaths = current.Keys.Where(path => !known.ContainsKey(path)).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
            WriteBoth("tick=" + tick + " time=" + DateTime.Now.ToString("HH:mm:ss") + " avfx=" + current.Count + " new=" + newPaths.Count + " scannedMB=" + (tickBytes / 1024.0 / 1024.0).ToString("0.0"));
            foreach (var path in newPaths.Take(120))
            {
                WriteBoth("  + 0x" + current[path].ToString("X") + " " + path);
                known[path] = current[path];
            }

            foreach (var line in GetRecentCastLines(start.AddSeconds(-2), tickStart.AddSeconds(1)))
            {
                if (seenLogLines.Add(line))
                {
                    WriteBoth("  log " + line);
                }
            }
        }

        WriteBoth("done output=" + outputPath);
        try
        {
            var analysisPath = AnalyzeCastVfxLog(outputPath);
            WriteBoth("analysis=" + analysisPath);
        }
        catch (Exception ex)
        {
            WriteBoth("analysis skipped: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void MergeCastVfxCommand(string[] args)
    {
        try
        {
            var pattern = args.Length > 1 ? args[1] : Path.Combine("tools", "EntityEspProbe", "captures", "ability-vfx-candidates-*.json");
            var files = ResolveGlob(pattern)
                .Where(path => Path.GetFileName(path).IndexOf("merged", StringComparison.OrdinalIgnoreCase) < 0)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (files.Count == 0)
            {
                Console.WriteLine("merge-cast-vfx: no candidate JSON files found for " + pattern);
                return;
            }

            var merged = MergeCastVfxCandidates(files);
            var output = Path.Combine("tools", "EntityEspProbe", "captures", "ability-vfx-merged-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
            File.WriteAllText(output, JsonConvert.SerializeObject(merged, Formatting.Indented));
            Console.WriteLine("merge-cast-vfx:");
            Console.WriteLine("  files=" + files.Count);
            Console.WriteLine("  output=" + Path.GetFullPath(output));
            foreach (var candidate in merged.Candidates.Take(20))
            {
                Console.WriteLine("  " + candidate.AbilityId + " " + candidate.AbilityName + " files=" + candidate.SeenInFiles);
                foreach (var path in candidate.CandidateAvfx.Take(3))
                {
                    Console.WriteLine("    " + path.FinalScore.ToString("0.00") + " raw=" + path.RawScore + " files=" + path.SeenInFiles + " " + path.Classification + " " + path.Path);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("merge-cast-vfx failed: " + ex.GetType().FullName + ": " + ex.Message);
        }
    }

    private static MergedCastVfxAnalysis MergeCastVfxCandidates(IReadOnlyList<string> files)
    {
        var byAbility = new Dictionary<string, MergedAbilityVfxCandidate>(StringComparer.OrdinalIgnoreCase);
        var seenAbilityFiles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var seenPathFiles = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var analysis = JsonConvert.DeserializeObject<CastVfxAnalysis>(File.ReadAllText(file));
            if (analysis?.Candidates == null)
            {
                continue;
            }

            foreach (var source in analysis.Candidates)
            {
                if (string.IsNullOrWhiteSpace(source.AbilityId))
                {
                    continue;
                }

                if (!byAbility.TryGetValue(source.AbilityId, out var merged))
                {
                    merged = new MergedAbilityVfxCandidate
                    {
                        AbilityId = source.AbilityId,
                        AbilityName = source.AbilityName,
                    };
                    byAbility.Add(source.AbilityId, merged);
                    seenAbilityFiles[source.AbilityId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    seenPathFiles[source.AbilityId] = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                }

                seenAbilityFiles[source.AbilityId].Add(file);
                foreach (var sourceName in source.SourceNames)
                {
                    if (!merged.SourceNames.ContainsKey(sourceName.Key))
                    {
                        merged.SourceNames[sourceName.Key] = 0;
                    }

                    merged.SourceNames[sourceName.Key] += sourceName.Value;
                }

                foreach (var pathScore in source.CandidateAvfx)
                {
                    var target = merged.CandidateAvfx.FirstOrDefault(item => string.Equals(item.Path, pathScore.Path, StringComparison.OrdinalIgnoreCase));
                    if (target == null)
                    {
                        target = new MergedVfxPathScore { Path = pathScore.Path, Classification = ClassifyAvfxPath(pathScore.Path), Weight = GetAvfxPathWeight(pathScore.Path) };
                        merged.CandidateAvfx.Add(target);
                        seenPathFiles[source.AbilityId][pathScore.Path] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }

                    target.RawScore += pathScore.Score;
                    seenPathFiles[source.AbilityId][pathScore.Path].Add(file);
                }
            }
        }

        foreach (var ability in byAbility.Values)
        {
            ability.SeenInFiles = seenAbilityFiles.TryGetValue(ability.AbilityId, out var abilityFiles) ? abilityFiles.Count : 0;
            foreach (var path in ability.CandidateAvfx)
            {
                path.SeenInFiles = seenPathFiles[ability.AbilityId].TryGetValue(path.Path, out var pathFiles) ? pathFiles.Count : 0;
                path.FinalScore = (path.RawScore + path.SeenInFiles * 2.0) * path.Weight;
            }

            ability.CandidateAvfx = ability.CandidateAvfx
                .OrderByDescending(path => path.FinalScore)
                .ThenByDescending(path => path.SeenInFiles)
                .ThenByDescending(path => path.RawScore)
                .ThenBy(path => path.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return new MergedCastVfxAnalysis
        {
            GeneratedAt = DateTime.Now.ToString("O"),
            SourceFiles = files.Select(Path.GetFullPath).ToList(),
            Candidates = byAbility.Values
                .OrderByDescending(candidate => candidate.SeenInFiles)
                .ThenBy(candidate => candidate.AbilityName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.AbilityId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    private static List<string> ResolveGlob(string pattern)
    {
        var dirPart = Path.GetDirectoryName(pattern);
        var filePattern = Path.GetFileName(pattern);
        if (string.IsNullOrWhiteSpace(dirPart))
        {
            dirPart = ".";
        }

        var dir = Path.GetFullPath(dirPart);
        if (string.IsNullOrWhiteSpace(filePattern))
        {
            filePattern = "*.json";
        }

        if (!Directory.Exists(dir))
        {
            return new List<string>();
        }

        return Directory.GetFiles(dir, filePattern).ToList();
    }

    private static string ClassifyAvfxPath(string path)
    {
        var lower = path.ToLowerInvariant();
        if (lower.Contains("/monster/") || lower.Contains("m0532")) return "monster-specific";
        if (lower.Contains("/pc_common/") || lower.Contains("/action/")) return "player/action-generic";
        if (lower.Contains("/lockon/")) return "lockon-generic";
        if (lower.Contains("cmcs_") || lower.Contains("mon_eisyo") || lower.Contains("classchng")) return "cast-generic";
        if (lower.Contains("/common/")) return "common";
        return "unknown";
    }

    private static double GetAvfxPathWeight(string path)
    {
        var classification = ClassifyAvfxPath(path);
        switch (classification)
        {
            case "monster-specific": return 2.0;
            case "common": return 1.0;
            case "unknown": return 0.9;
            case "cast-generic": return 0.35;
            case "lockon-generic": return 0.25;
            case "player/action-generic": return 0.2;
            default: return 0.5;
        }
    }

    private static void AnalyzeCastVfxCommand(string[] args)
    {
        var path = args.Length > 1 ? args[1] : FindLatestCastVfxCaptureLog();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Console.WriteLine("analyze-cast-vfx: capture log not found");
            return;
        }

        var analysisPath = AnalyzeCastVfxLog(path);
        Console.WriteLine("analyze-cast-vfx:");
        Console.WriteLine("  source=" + Path.GetFullPath(path));
        Console.WriteLine("  output=" + analysisPath);
    }

    private static string AnalyzeCastVfxLog(string path)
    {
        string[] lines;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(stream))
        {
            lines = reader.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }
        var groups = ParseCastVfxTickGroups(lines);
        var casts = ParseCastLogEntries(lines);
        foreach (var group in groups)
        {
            if (!TimeSpan.TryParse(group.Time, out var groupTime))
            {
                continue;
            }

            var near = casts
                .Where(cast => Math.Abs((cast.Time.TimeOfDay - groupTime).TotalSeconds) <= 12)
                .GroupBy(cast => cast.SourceName + "\u001F" + cast.AbilityId + "\u001F" + cast.AbilityName)
                .Select(g =>
                {
                    var parts = g.Key.Split('\u001F');
                    return new AbilitySummary
                    {
                        SourceName = parts[0],
                        AbilityId = parts[1],
                        AbilityName = parts[2],
                        Count = g.Count(),
                    };
                })
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.AbilityId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            group.NearAbilities = near;
        }

        var candidateMap = new Dictionary<string, AbilityVfxCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups.Where(g => g.Paths.Count > 0))
        {
            foreach (var ability in group.NearAbilities)
            {
                if (!candidateMap.TryGetValue(ability.AbilityId, out var candidate))
                {
                    candidate = new AbilityVfxCandidate
                    {
                        AbilityId = ability.AbilityId,
                        AbilityName = ability.AbilityName,
                    };
                    candidateMap.Add(ability.AbilityId, candidate);
                }

                if (!candidate.SourceNames.ContainsKey(ability.SourceName))
                {
                    candidate.SourceNames[ability.SourceName] = 0;
                }

                candidate.SourceNames[ability.SourceName] += ability.Count;
                candidate.Examples.Add(new AbilityExample { Tick = group.Tick, Time = group.Time, NearCount = ability.Count });
                foreach (var avfx in group.Paths)
                {
                    var score = candidate.CandidateAvfx.FirstOrDefault(item => string.Equals(item.Path, avfx, StringComparison.OrdinalIgnoreCase));
                    if (score == null)
                    {
                        score = new VfxPathScore { Path = avfx };
                        candidate.CandidateAvfx.Add(score);
                    }

                    score.Score++;
                }
            }
        }

        var analysis = new CastVfxAnalysis
        {
            SourceLog = Path.GetFullPath(path),
            GeneratedAt = DateTime.Now.ToString("O"),
            Groups = groups,
            Candidates = candidateMap.Values
                .OrderBy(item => item.AbilityName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.AbilityId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
        foreach (var candidate in analysis.Candidates)
        {
            candidate.CandidateAvfx = candidate.CandidateAvfx
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var output = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".", "ability-vfx-candidates-" + Path.GetFileNameWithoutExtension(path).Replace("cast-vfx-", string.Empty) + ".json");
        File.WriteAllText(output, JsonConvert.SerializeObject(analysis, Formatting.Indented));
        return output;
    }

    private static List<CastVfxTickGroup> ParseCastVfxTickGroups(string[] lines)
    {
        var groups = new List<CastVfxTickGroup>();
        CastVfxTickGroup? current = null;
        foreach (var line in lines)
        {
            if (line.StartsWith("tick=", StringComparison.Ordinal))
            {
                var parts = line.Split(' ');
                current = new CastVfxTickGroup();
                foreach (var part in parts)
                {
                    var pair = part.Split(new[] { '=' }, 2);
                    if (pair.Length != 2)
                    {
                        continue;
                    }

                    if (pair[0] == "tick" && int.TryParse(pair[1], out var tick)) current.Tick = tick;
                    if (pair[0] == "time") current.Time = pair[1];
                    if (pair[0] == "new" && int.TryParse(pair[1], out var newCount)) current.NewCount = newCount;
                }

                groups.Add(current);
                continue;
            }

            if (current != null && line.StartsWith("  + ", StringComparison.Ordinal))
            {
                var marker = ".avfx";
                var end = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (end >= 0)
                {
                    var prefix = line.LastIndexOf(' ', end);
                    if (prefix >= 0)
                    {
                        current.Paths.Add(line.Substring(prefix + 1, end - prefix - 1 + marker.Length));
                    }
                }
            }
        }

        return groups;
    }

    private static List<CastLogEntry> ParseCastLogEntries(string[] lines)
    {
        var casts = new List<CastLogEntry>();
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("log 20|", StringComparison.Ordinal))
            {
                continue;
            }

            var fields = trimmed.Substring(4).Split('|');
            if (fields.Length < 6 || !DateTimeOffset.TryParse(fields[1], out var time))
            {
                continue;
            }

            casts.Add(new CastLogEntry
            {
                Time = time.DateTime,
                SourceId = fields.Length > 2 ? fields[2] : string.Empty,
                SourceName = fields.Length > 3 ? fields[3] : string.Empty,
                AbilityId = fields.Length > 4 ? fields[4] : string.Empty,
                AbilityName = fields.Length > 5 ? fields[5] : string.Empty,
            });
        }

        return casts;
    }

    private static string FindLatestCastVfxCaptureLog()
    {
        var dir = Path.Combine("tools", "EntityEspProbe", "captures");
        if (!Directory.Exists(dir))
        {
            return string.Empty;
        }

        return Directory.GetFiles(dir, "cast-vfx-*.log")
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault() ?? string.Empty;
    }

    private static void PrintRecentCastLines(DateTime fromUtc, DateTime toUtc)
    {
        var logFile = FindLatestNetworkLog();
        if (logFile == null)
        {
            Console.WriteLine("  recentCastLines=<no ACT Network_*.log found>");
            return;
        }

        Console.WriteLine("  recentCastLines=" + logFile.FullName);
        foreach (var line in GetRecentCastLines(fromUtc, toUtc))
        {
            Console.WriteLine("  log " + line);
        }
    }

    private static IEnumerable<string> GetRecentCastLines(DateTime fromUtc, DateTime toUtc)
    {
        var logFile = FindLatestNetworkLog();
        if (logFile == null)
        {
            yield break;
        }

        foreach (var line in ReadTailLines(logFile.FullName, 12000))
        {
            var fields = line.Split('|');
            if (fields.Length < 3 || (fields[0] != "20" && fields[0] != "263" && fields[0] != "264"))
            {
                continue;
            }

            if (DateTimeOffset.TryParse(fields[1], out var dto))
            {
                var utc = dto.UtcDateTime;
                if (utc < fromUtc || utc > toUtc)
                {
                    continue;
                }
            }

            yield return line;
        }
    }

    private static void PrintNewAvfxPaths(Dictionary<string, long> before, Dictionary<string, long> after, int limit, string prefix)
    {
        var newPaths = after.Keys
            .Where(path => !before.ContainsKey(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
        Console.WriteLine("  newAvfx=" + newPaths.Count);
        foreach (var path in newPaths)
        {
            Console.WriteLine(prefix + "0x" + after[path].ToString("X") + " " + path);
        }
    }

    private static Dictionary<string, long> ScanAvfxPaths(ProcessMemoryReader reader, string filter, out long scannedBytes, out int regionCount)
    {
        var regions = reader.GetReadableMemoryRegions(maxRegionSize: 32 * 1024 * 1024)
            .Where(region => region.Size >= 4096)
            .ToList();
        regionCount = regions.Count;
        scannedBytes = 0;
        var hits = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var region in regions)
        {
            var remaining = region.Size;
            var address = region.BaseAddress;
            while (remaining > 0)
            {
                var chunkSize = (int)Math.Min(4 * 1024 * 1024, remaining);
                if (reader.TryReadBytes(address, chunkSize, out var bytes))
                {
                    scannedBytes += bytes.Length;
                    foreach (var hit in ExtractAvfxPaths(bytes, address))
                    {
                        if (!string.IsNullOrWhiteSpace(filter) && hit.Path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }

                        if (!hits.ContainsKey(hit.Path))
                        {
                            hits.Add(hit.Path, hit.Address);
                        }
                    }
                }

                address += chunkSize;
                remaining -= chunkSize;
                if (hits.Count >= 4000)
                {
                    return hits;
                }
            }
        }

        return hits;
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
            if (length < 8 || length > 260)
            {
                continue;
            }

            var path = System.Text.Encoding.ASCII.GetString(bytes, start, length).Replace('\\', '/');
            if (!path.StartsWith("vfx/", StringComparison.OrdinalIgnoreCase) || !path.EndsWith(".avfx", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new AvfxMemoryHit(baseAddress + start, path);
        }
    }

    private static bool IsAvfxAt(byte[] bytes, int index)
    {
        return (bytes[index] == (byte)'.')
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

    private static void ProbeVfxFunctionsCommand(ProcessMemoryReader reader)
    {
        Console.WriteLine("probe-vfx-functions:");
        Console.WriteLine("  goal: locate VFX create/remove functions from VFXEditor/FFXIVClientStructs signatures");
        var scans = new[]
        {
            reader.ScanMainModule("ActorVfxCreate", "40 53 55 56 57 48 81 EC ?? ?? ?? ?? 0F 29 B4 24 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 0F B6 AC 24 ?? ?? ?? ?? 0F 28 F3 49 8B F8", 0, 0, ".text"),
            reader.ScanMainModule("ActorVfxRemove", "0F 11 48 10 48 8D 05", 0, 0, ".text"),
            reader.ScanMainModule("StaticVfxRun", "E8 ?? ?? ?? ?? B0 02 EB 02", 1, 5, ".text"),
            reader.ScanMainModule("StaticVfxRemove", "40 53 48 83 EC 20 48 8B D9 48 8B 89 ?? ?? ?? ?? 48 85 C9 74 28 33 D2 E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ?? 48 85 C9", 0, 0, ".text"),
            reader.ScanMainModule("VfxObjectCreate", "E8 ?? ?? ?? ?? F3 0F 10 35 ?? ?? ?? ?? 48 89 43 08", 1, 5, ".text"),
            reader.ScanMainModule("CallTrigger", "E8 ?? ?? ?? ?? 0F B7 43 56", 1, 5, ".text"),
        };

        foreach (var scan in scans)
        {
            Console.WriteLine("  " + scan.Name
                + " hits=" + scan.HitCount
                + " first=0x" + scan.FirstHitAddress.ToString("X")
                + " resolved=0x" + scan.ResolvedAddress.ToString("X")
                + (scan.ResolvedAddress != 0 ? " module+0x" + (scan.ResolvedAddress - reader.Status.ModuleBase).ToString("X") : string.Empty)
                + (string.IsNullOrWhiteSpace(scan.Error) ? string.Empty : " error=" + scan.Error));
        }

        Console.WriteLine("  next:");
        Console.WriteLine("    if ActorVfxCreate/StaticVfxRun resolve, build a probe that records create return VfxObject*, path, caster/target args, then validates VfxObject offsets");
        Console.WriteLine("    ACT plugin cannot safely hook FF14 functions out-of-process; runtime integration likely needs injected helper/Dalamud helper, or keep ACT using network logs + helper feed");
    }

    private static void ProbeVfxChainCommand(ProcessMemoryReader reader, string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("probe-vfx-chain requires a .avfx string address, e.g. probe-vfx-chain 0x1E545001F00");
            return;
        }

        var pathAddress = ParseAddress(reader, args[1]);
        Console.WriteLine("probe-vfx-chain:");
        Console.WriteLine("  pathAddress=0x" + pathAddress.ToString("X"));
        DumpPathAddressNeighborhood(reader, pathAddress);

        var refs = FindPointerReferences(reader, pathAddress, maxRegions: 260, maxRefs: 80);
        Console.WriteLine("  directRefs=" + refs.Count);
        foreach (var r in refs.Take(30))
        {
            Console.WriteLine("    ref=0x" + r.ToString("X") + " base? resourceHandle=0x" + (r - 0x48).ToString("X") + " section=" + FindSectionName(reader, r));
            ProbePossibleResourceHandle(reader, r - 0x48, "      ");
        }

        var nearStdStringRefs = FindPointerReferences(reader, pathAddress - 0x0, maxRegions: 260, maxRefs: 20);
        _ = nearStdStringRefs;
        Console.WriteLine("  next: if resourceHandle candidates resolve FileName, scan refs to that handle, then VfxResourceUnk/Instance/VfxObject");
    }

    private static void DumpPathAddressNeighborhood(ProcessMemoryReader reader, long pathAddress)
    {
        var start = pathAddress - 0x80;
        if (!reader.TryReadBytes(start, 0x180, out var bytes))
        {
            Console.WriteLine("  neighborhood read failed: " + reader.Status.LastError);
            return;
        }

        var relative = (int)(pathAddress - start);
        var text = ReadAscii(bytes, relative, Math.Min(180, bytes.Length - relative));
        Console.WriteLine("  pathText=" + text);
        for (var offset = 0; offset <= bytes.Length - 0x20; offset += 8)
        {
            var len = BitConverter.ToUInt64(bytes, offset + 0x10);
            var cap = BitConverter.ToUInt64(bytes, offset + 0x18);
            if (len == 0 || len > 240 || cap < len || cap > 4096)
            {
                continue;
            }

            var candidateAddress = start + offset;
            if (TryReadStdString(reader, candidateAddress, out var candidateText))
            {
                Console.WriteLine("  stdStringCandidate=0x" + candidateAddress.ToString("X") + " len=" + len + " cap=" + cap + " text=" + candidateText);
                Console.WriteLine("    possible ResourceHandle=0x" + (candidateAddress - 0x48).ToString("X"));
                ProbePossibleResourceHandle(reader, candidateAddress - 0x48, "    ");
            }
        }
    }

    private static List<long> FindPointerReferences(ProcessMemoryReader reader, long pointerValue, int maxRegions, int maxRefs)
    {
        var refs = new List<long>();
        var needle = BitConverter.GetBytes(pointerValue);
        var regions = reader.GetReadableMemoryRegions(maxRegionSize: 16 * 1024 * 1024)
            .Where(region => FindSectionName(reader, region.BaseAddress) == "<outside>")
            .Take(maxRegions)
            .ToList();
        foreach (var region in regions)
        {
            var size = (int)Math.Min(region.Size, 16 * 1024 * 1024);
            if (!reader.TryReadBytes(region.BaseAddress, size, out var bytes))
            {
                continue;
            }

            for (var i = 0; i <= bytes.Length - 8; i++)
            {
                if (bytes[i] != needle[0])
                {
                    continue;
                }

                var match = true;
                for (var j = 1; j < 8; j++)
                {
                    if (bytes[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    refs.Add(region.BaseAddress + i);
                    if (refs.Count >= maxRefs)
                    {
                        return refs;
                    }
                }
            }
        }

        return refs;
    }

    private static void ProbePossibleResourceHandle(ProcessMemoryReader reader, long handleAddress, string indent)
    {
        if (TryReadStdString(reader, handleAddress + 0x48, out var path))
        {
            Console.WriteLine(indent + "ResourceHandle.FileName OK path=" + path);
            var handleRefs = FindPointerReferences(reader, handleAddress, maxRegions: 260, maxRefs: 20);
            Console.WriteLine(indent + "handleRefs=" + handleRefs.Count);
            foreach (var handleRef in handleRefs.Take(8))
            {
                Console.WriteLine(indent + "  handleRef=0x" + handleRef.ToString("X") + " maybeVfxResourceUnk=0x" + (handleRef - 0x18).ToString("X"));
            }
        }
    }

    private static void ProbeVfxObjectCommand(ProcessMemoryReader reader, string[] args)
    {
        var maxRegions = args.Length > 1 && int.TryParse(args[1], out var parsedRegions) ? Math.Max(1, parsedRegions) : 160;
        var maxPrinted = args.Length > 2 && int.TryParse(args[2], out var parsedPrinted) ? Math.Max(1, parsedPrinted) : 30;
        Console.WriteLine("probe-vfx-object:");
        Console.WriteLine("  goal: scan readable heap regions for candidate Client::Graphics::Scene::VfxObject structs");
        Console.WriteLine("  checks: pos@0x50, caster@0x128, target@0x130, resourceInstance@0x2A0 -> +0x08 -> +0x18 -> ResourceHandle.FileName@0x48");
        Console.WriteLine("  maxRegions=" + maxRegions + " maxPrinted=" + maxPrinted);
        var regions = reader.GetReadableMemoryRegions(maxRegionSize: 16 * 1024 * 1024)
            .Where(region => region.Size >= 0x390 && FindSectionName(reader, region.BaseAddress) == "<outside>")
            .OrderBy(region => region.Size)
            .Take(maxRegions)
            .ToList();
        var candidates = new List<VfxObjectProbeResult>();
        long scannedBytes = 0;
        foreach (var region in regions)
        {
            var readSize = (int)Math.Min(region.Size, 16 * 1024 * 1024);
            if (!reader.TryReadBytes(region.BaseAddress, readSize, out var bytes))
            {
                continue;
            }

            scannedBytes += bytes.Length;
            for (var offset = 0; offset <= bytes.Length - 0x390; offset += 0x10)
            {
                var address = region.BaseAddress + offset;
                if (!LooksLikeVfxObjectCandidate(reader, bytes, offset, address, out var result))
                {
                    continue;
                }

                candidates.Add(result);
                if (candidates.Count >= maxPrinted * 8)
                {
                    break;
                }
            }
        }

        Console.WriteLine("  regionsScanned=" + regions.Count + " scannedMB=" + (scannedBytes / 1024.0 / 1024.0).ToString("0.0") + " candidates=" + candidates.Count);
        foreach (var item in candidates.OrderByDescending(candidate => !string.IsNullOrWhiteSpace(candidate.Path)).ThenByDescending(candidate => candidate.Score).Take(maxPrinted))
        {
            Console.WriteLine("  vfx=0x" + item.Address.ToString("X")
                + " score=" + item.Score
                + " pos=(" + FormatFloat(item.X) + "," + FormatFloat(item.Y) + "," + FormatFloat(item.Z) + ")"
                + " caster=" + item.ActorCaster
                + " target=" + item.ActorTarget
                + " staticCaster=" + item.StaticCaster
                + " staticTarget=" + item.StaticTarget
                + " res=0x" + item.ResourceInstance.ToString("X")
                + (string.IsNullOrWhiteSpace(item.Path) ? " path=<unresolved>" : " path=" + item.Path));
        }

        Console.WriteLine("  next:");
        Console.WriteLine("    if candidates with path appear, replace string scan with active VfxObject traversal after finding the owning list/root");
        Console.WriteLine("    if candidates are noisy, find VfxObject vtable/signature or scene object list before enabling runtime use");
    }

    private static bool LooksLikeVfxObjectCandidate(ProcessMemoryReader reader, byte[] bytes, int offset, long address, out VfxObjectProbeResult result)
    {
        result = new VfxObjectProbeResult { Address = address };
        var x = BitConverter.ToSingle(bytes, offset + 0x50);
        var y = BitConverter.ToSingle(bytes, offset + 0x54);
        var z = BitConverter.ToSingle(bytes, offset + 0x58);
        if (!IsReasonablePosition(x, y, z))
        {
            return false;
        }

        var resourceInstance = BitConverter.ToInt64(bytes, offset + 0x2A0);
        if (!LooksLikeUserModePointer(resourceInstance))
        {
            return false;
        }

        result.X = x;
        result.Y = y;
        result.Z = z;
        result.ActorCaster = BitConverter.ToInt32(bytes, offset + 0x128);
        result.ActorTarget = BitConverter.ToInt32(bytes, offset + 0x130);
        result.StaticCaster = BitConverter.ToInt32(bytes, offset + 0x1B8);
        result.StaticTarget = BitConverter.ToInt32(bytes, offset + 0x1C0);
        result.ResourceInstance = resourceInstance;
        result.Score = 1;
        if (TryResolveVfxResourcePath(reader, resourceInstance, out var path))
        {
            result.Path = path;
            result.Score += 10;
        }

        if (result.ActorCaster > 0 || result.ActorTarget > 0 || result.StaticCaster > 0 || result.StaticTarget > 0)
        {
            result.Score += 2;
        }

        return result.Score >= 3 || !string.IsNullOrWhiteSpace(result.Path);
    }

    private static bool TryResolveVfxResourcePath(ProcessMemoryReader reader, long resourceInstance, out string path)
    {
        path = string.Empty;
        if (!TryReadPointer(reader, resourceInstance + 0x08, out var vfxResourceUnk) || !LooksLikeUserModePointer(vfxResourceUnk))
        {
            return false;
        }

        if (!TryReadPointer(reader, vfxResourceUnk + 0x18, out var apricotHandle) || !LooksLikeUserModePointer(apricotHandle))
        {
            return false;
        }

        return TryReadStdString(reader, apricotHandle + 0x48, out path) && path.Contains(".avfx");
    }

    private static bool TryReadStdString(ProcessMemoryReader reader, long stdStringAddress, out string value)
    {
        value = string.Empty;
        if (!reader.TryReadBytes(stdStringAddress, 0x20, out var bytes))
        {
            return false;
        }

        var length = BitConverter.ToUInt64(bytes, 0x10);
        var capacity = BitConverter.ToUInt64(bytes, 0x18);
        if (length == 0 || length > 240 || capacity < length || capacity > 4096)
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
            if (!LooksLikeUserModePointer(ptr) || !reader.TryReadBytes(ptr, (int)length, out stringBytes))
            {
                return false;
            }
        }

        value = System.Text.Encoding.UTF8.GetString(stringBytes).Replace('\\', '/').ToLowerInvariant();
        return value.Contains("vfx/");
    }

    private static bool TryReadPointer(ProcessMemoryReader reader, long address, out long pointer)
    {
        pointer = 0;
        if (!reader.TryReadBytes(address, 8, out var bytes))
        {
            return false;
        }

        pointer = BitConverter.ToInt64(bytes, 0);
        return true;
    }

    private sealed class VfxObjectProbeResult
    {
        public long Address { get; set; }
        public int Score { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public int ActorCaster { get; set; }
        public int ActorTarget { get; set; }
        public int StaticCaster { get; set; }
        public int StaticTarget { get; set; }
        public long ResourceInstance { get; set; }
        public string Path { get; set; } = string.Empty;
    }

    private static void ProbeCharacterVfxCommand(ProcessMemoryReader reader, string[] args)
    {
        var maxEntities = args.Length > 1 && int.TryParse(args[1], out var parsed) ? Math.Max(1, parsed) : 40;
        Console.WriteLine("probe-character-vfx:");
        Console.WriteLine("  goal: validate Character+0x1988 VfxContainer and VfxData[14] pointers from ObjectTable entities");
        Console.WriteLine("  offsets: Character.VfxContainer=0x1988, VfxContainer.VfxData=0x18, slots=14, pointerSize=8");
        var tableReader = new GameObjectTableReader(reader);
        var entities = tableReader.ReadExperimentalObjectTable(out var matchedSlots).Take(maxEntities).ToList();
        Console.WriteLine("  entities=" + entities.Count + " matchedSlots=" + matchedSlots + " max=" + maxEntities);
        var printed = 0;
        foreach (var entity in entities)
        {
            if (entity.Kind != EntityKind.Player && entity.Kind != EntityKind.BattleNpc && entity.Kind != EntityKind.EventNpc)
            {
                continue;
            }

            var baseAddress = unchecked((long)entity.Address);
            var containerAddress = baseAddress + 0x1988;
            var slotPointers = new List<long>();
            var readableSlots = 0;
            var nonZeroSlots = 0;
            for (var slot = 0; slot < 14; slot++)
            {
                var ptrAddress = containerAddress + 0x18 + slot * 8L;
                if (!reader.TryReadBytes(ptrAddress, 8, out var bytes))
                {
                    slotPointers.Add(0);
                    continue;
                }

                readableSlots++;
                var ptr = BitConverter.ToInt64(bytes, 0);
                slotPointers.Add(ptr);
                if (LooksLikeUserModePointer(ptr))
                {
                    nonZeroSlots++;
                }
            }

            if (nonZeroSlots == 0 && printed >= 10)
            {
                continue;
            }

            printed++;
            Console.WriteLine("  entity=0x" + entity.EntityId.ToString("X8")
                + " kind=" + entity.Kind
                + " bNpc=" + entity.BNpcId
                + " name='" + entity.Name.Replace("'", "?") + "'"
                + " obj=0x" + baseAddress.ToString("X")
                + " vfxContainer=0x" + containerAddress.ToString("X")
                + " readableSlots=" + readableSlots
                + " nonZero=" + nonZeroSlots);
            for (var slot = 0; slot < slotPointers.Count; slot++)
            {
                var ptr = slotPointers[slot];
                if (LooksLikeUserModePointer(ptr))
                {
                    Console.WriteLine("    slot[" + slot.ToString("00") + "] ptr=0x" + ptr.ToString("X") + " sample=" + ProbePointerBytes(reader, ptr));
                    ProbeVfxDataNeighborhood(reader, ptr, "      ");
                }
            }
        }

        Console.WriteLine("  next:");
        Console.WriteLine("    if many nonZero slots appear during visible effects, inspect VfxData layout and follow resource pointers");
        Console.WriteLine("    if all slots stay zero, Character+0x1988 may be wrong for this build or only covers special actor-bound VFX");
    }

    private static void ProbeVfxDataNeighborhood(ProcessMemoryReader reader, long address, string indent)
    {
        if (!reader.TryReadBytes(address, 0x200, out var bytes))
        {
            Console.WriteLine(indent + "neighborhood read failed: " + reader.Status.LastError);
            return;
        }

        var pointerHits = new List<string>();
        for (var offset = 0; offset <= bytes.Length - 8; offset += 8)
        {
            var ptr = BitConverter.ToInt64(bytes, offset);
            if (!LooksLikeUserModePointer(ptr))
            {
                continue;
            }

            var note = string.Empty;
            if (TryReadAsciiNear(reader, ptr, out var text))
            {
                note = " ascii='" + text.Replace("'", "?") + "'";
            }

            pointerHits.Add("+0x" + offset.ToString("X") + "->0x" + ptr.ToString("X") + note);
            if (pointerHits.Count >= 12)
            {
                break;
            }
        }

        foreach (var line in pointerHits)
        {
            Console.WriteLine(indent + line);
        }
    }

    private static bool TryReadAsciiNear(ProcessMemoryReader reader, long address, out string text)
    {
        text = string.Empty;
        if (!reader.TryReadBytes(address, 0x180, out var bytes))
        {
            return false;
        }

        var raw = ReadAscii(bytes, 0, bytes.Length).Replace('\\', '/').ToLowerInvariant();
        if (raw.Contains(".avfx") || raw.Contains("vfx/"))
        {
            text = raw.Length > 160 ? raw.Substring(0, 160) : raw;
            return true;
        }

        for (var i = 0; i < bytes.Length - 8; i++)
        {
            if (bytes[i] == (byte)'v' && i + 4 < bytes.Length && bytes[i + 1] == (byte)'f' && bytes[i + 2] == (byte)'x' && bytes[i + 3] == (byte)'/')
            {
                text = ReadAscii(bytes, i, Math.Min(160, bytes.Length - i));
                return true;
            }
        }

        return false;
    }

    private static string ProbePointerBytes(ProcessMemoryReader reader, long address)
    {
        if (!reader.TryReadBytes(address, 0x40, out var bytes))
        {
            return "read-failed:" + reader.Status.LastError;
        }

        var firstPtr = bytes.Length >= 8 ? BitConverter.ToInt64(bytes, 0) : 0;
        var secondPtr = bytes.Length >= 16 ? BitConverter.ToInt64(bytes, 8) : 0;
        var hex = string.Join(" ", bytes.Take(16).Select(value => value.ToString("X2")));
        return "p0=0x" + firstPtr.ToString("X") + " p8=0x" + secondPtr.ToString("X") + " bytes=" + hex;
    }

    private static void UpdateAuditCommand(ProcessMemoryReader reader)
    {
        Console.WriteLine("update-audit:");
        Console.WriteLine("  purpose: run this after every FF14 game update before shipping the plugin");
        Console.WriteLine("  gameProcess: " + (reader.Status.ProcessFound ? "OK pid=" + reader.Status.ProcessId : "missing"));
        Console.WriteLine("  module: " + (reader.Status.ModuleFound ? "OK base=0x" + reader.Status.ModuleBase.ToString("X") + " size=" + reader.Status.ModuleSize : "missing"));
        Console.WriteLine("  sections: " + string.Join(", ", reader.Status.Sections.Select(section => section.Name + "=0x" + section.ScanSize.ToString("X"))));

        var objectReader = new GameObjectTableReader(reader);
        var entities = objectReader.ReadExperimentalObjectTable(out var matchedSlots);
        Console.WriteLine("  ObjectTable:");
        PrintAuditScan(objectReader.LastObjectTableScan);
        Console.WriteLine("    status=" + objectReader.LastStatus);
        Console.WriteLine("    matchedSlots=" + matchedSlots + " entities=" + entities.Count + " players=" + entities.Count(entity => entity.Kind == EntityKind.Player) + " bnpcs=" + entities.Count(entity => entity.Kind == EntityKind.BattleNpc));
        if (entities.Count > 0)
        {
            var first = entities[0];
            Console.WriteLine("    sample=EntityId:0x" + first.EntityId.ToString("X8") + " Kind:" + first.Kind + " BNpcId:" + first.BNpcId + " Pos:" + first.Position.X.ToString("0.00") + "," + first.Position.Y.ToString("0.00") + "," + first.Position.Z.ToString("0.00"));
        }

        var cameraReader = new ControlCameraReader(reader);
        var camera = cameraReader.TryReadCamera(1839, 1185, out var cameraSnapshot, out var cameraScan, out var cameraStatus);
        Console.WriteLine("  Control/Camera:");
        PrintAuditScan(cameraScan);
        Console.WriteLine("    matrix=" + (camera ? "OK" : "FAIL") + " status=" + cameraStatus);

        var targetReader = new CurrentTargetReader(reader, cameraReader);
        Console.WriteLine("  TargetSystem:");
        Console.WriteLine("    hardTarget=" + (targetReader.TryReadCurrentTargetEntityId(out var targetId) ? "OK 0x" + targetId.ToString("X8") : "unavailable") + " status=" + targetReader.Status);

        var avfx = ScanAvfxPaths(reader, string.Empty, out var scannedBytes, out var regionCount);
        Console.WriteLine("  VFX string scan:");
        Console.WriteLine("    paths=" + avfx.Count + " scannedMB=" + (scannedBytes / 1024.0 / 1024.0).ToString("0.0") + " regions=" + regionCount);
        foreach (var item in avfx.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).Take(8))
        {
            Console.WriteLine("    " + item);
        }

        Console.WriteLine("  Update checklist:");
        Console.WriteLine("    if ObjectTable hits=0 or entities=0: update ObjectTableSignature / fixed fallback offset / GameObject field offsets");
        Console.WriteLine("    if Camera matrix FAIL: update ControlInstanceSignature or ViewProjectionMatrixOffset");
        Console.WriteLine("    if TargetSystem unavailable but camera OK: update ControlTargetSystemOffset or TargetSystemHardTargetOffset");
        Console.WriteLine("    if VFX paths=0: update readable-memory filter or switch to active VfxObject probe");
        Console.WriteLine("    active VFX instance support still needs VfxObject/scene graph verification before enabling in ACT runtime");
    }

    private static void PrintAuditScan(PatternScanResult? scan)
    {
        if (scan == null)
        {
            Console.WriteLine("    scan=<none>");
            return;
        }

        Console.WriteLine("    scan=" + scan.Name + " hits=" + scan.HitCount + " resolved=0x" + scan.ResolvedAddress.ToString("X") + " pattern=" + scan.Pattern + (string.IsNullOrWhiteSpace(scan.Error) ? string.Empty : " error=" + scan.Error));
    }

    private static void ReadTargetCommand(ProcessMemoryReader reader)
    {
        Console.WriteLine("read-target:");
        var controlReader = new ControlCameraReader(reader);
        var scan = controlReader.ScanControlInstance();
        Console.WriteLine("  controlScan success=" + scan.Success + " hits=" + scan.HitCount + " first=0x" + scan.FirstHitAddress.ToString("X") + " resolved=0x" + scan.ResolvedAddress.ToString("X") + " cached=" + scan.IsCached + " error=" + scan.Error);
        if (!scan.Success || scan.ResolvedAddress == 0)
        {
            return;
        }

        var targetPointerAddress = scan.ResolvedAddress + ControlTargetSystemOffset + TargetSystemHardTargetOffset;
        if (!reader.TryReadBytes(targetPointerAddress, 8, out var pointerBytes))
        {
            Console.WriteLine("  targetPtr read failed at 0x" + targetPointerAddress.ToString("X") + ": " + reader.Status.LastError);
            return;
        }

        var targetPointer = BitConverter.ToInt64(pointerBytes, 0);
        Console.WriteLine("  targetPtrAddress=0x" + targetPointerAddress.ToString("X") + " module+0x" + (targetPointerAddress - reader.Status.ModuleBase).ToString("X") + " targetPtr=0x" + targetPointer.ToString("X"));
        if (targetPointer <= 0)
        {
            Console.WriteLine("  no hard target");
            return;
        }

        if (!reader.TryReadBytes(targetPointer + EntityIdOffset, 4, out var idBytes))
        {
            Console.WriteLine("  target EntityId read failed at 0x" + (targetPointer + EntityIdOffset).ToString("X") + ": " + reader.Status.LastError);
            return;
        }

        var entityId = BitConverter.ToUInt32(idBytes, 0);
        Console.WriteLine("  target EntityId=0x" + entityId.ToString("X8"));
        if (reader.TryReadBytes(targetPointer + BaseIdOffset, 4, out var baseBytes))
        {
            Console.WriteLine("  target BaseId=0x" + BitConverter.ToUInt32(baseBytes, 0).ToString("X"));
        }

        if (reader.TryReadBytes(targetPointer + ObjectKindOffset, 1, out var kindBytes))
        {
            Console.WriteLine("  target ObjectKind=" + kindBytes[0]);
        }
    }

    private static void BuildStatesCommand(ProcessMemoryReader reader)
    {
        var service = new DisplayStateService(new RealEntitySource(reader), new RealCameraSource(reader));
        var states = service.BuildStates(1839, 1185, new EspConfig { DataSourceMode = DataSourceMode.Real, MaxDisplayedEntities = 100, ShowUntargetable = true, MaxDistance = 10f });
        var diagnostics = service.LastDiagnostics;
        Console.WriteLine("build-states:");
        Console.WriteLine("  raw=" + diagnostics.RawEntityCount + " cameraValid=" + diagnostics.CameraValid + " entityReady=" + diagnostics.EntitySourceReady + " cameraReady=" + diagnostics.CameraSourceReady + " visible=" + states.Count);
        Console.WriteLine("  status=" + diagnostics.SourceStatus);
        foreach (var state in states.Take(40))
        {
            Console.WriteLine("  screen=(" + FormatFloat(state.ScreenPosition.X) + "," + FormatFloat(state.ScreenPosition.Y) + ") label='" + state.LabelText.Replace("'", "?") + "' kind=" + state.Snapshot.Kind + " dist=" + FormatFloat(state.Snapshot.DistanceToPlayer) + " pos=(" + FormatFloat(state.Snapshot.Position.X) + "," + FormatFloat(state.Snapshot.Position.Y) + "," + FormatFloat(state.Snapshot.Position.Z) + ")");
        }
    }

    private static void ReadEntitiesCommand(ProcessMemoryReader reader)
    {
        var source = new RealEntitySource(reader);
        var entities = source.GetEntities();
        Console.WriteLine("read-entities:");
        Console.WriteLine("  ready=" + source.IsReady + " status=" + source.Status);
        Console.WriteLine("  count=" + entities.Count);
        foreach (var entity in entities.Take(40))
        {
            Console.WriteLine("  addr=0x" + entity.Address.ToString("X") + " id=0x" + entity.EntityId.ToString("X8") + " kind=" + entity.Kind + " base=0x" + entity.BNpcId.ToString("X") + " nameId=0x" + entity.BNpcNameId.ToString("X") + " owner=0x" + entity.OwnerId.ToString("X8") + " dist=" + FormatFloat(entity.DistanceToPlayer) + " pos=(" + FormatFloat(entity.Position.X) + "," + FormatFloat(entity.Position.Y) + "," + FormatFloat(entity.Position.Z) + ") self=" + entity.IsSelf + " name='" + entity.Name.Replace("'", "?") + "'");
        }
    }

    private static void DumpTableCommand(ProcessMemoryReader reader, string[] args)
    {
        var tableBase = args.Length > 1 ? ParseAddress(reader, args[1]) : reader.Status.ModuleBase + 0x2895040;
        var start = args.Length > 2 && int.TryParse(args[2], out var parsedStart) ? Math.Max(0, parsedStart) : 0;
        var count = args.Length > 3 && int.TryParse(args[3], out var parsedCount) ? Math.Max(1, parsedCount) : 700;
        Console.WriteLine("dump-table:");
        Console.WriteLine("  tableBase=0x" + tableBase.ToString("X") + " module+0x" + (tableBase - reader.Status.ModuleBase).ToString("X") + " start=" + start + " count=" + count);

        var objects = ReadTableObjects(reader, tableBase, start, count);
        var valid = objects.Count(obj => obj.Score >= 3 && obj.ObjectIndex == obj.ArrayIndex);
        Console.WriteLine("  validIndexMatches=" + valid + " / " + count);
        Console.WriteLine("  kindCounts=" + FormatKindCounts(objects));
        Console.WriteLine("  firstObjects:");
        foreach (var obj in objects.Where(obj => obj.Score >= 3).Take(20))
        {
            PrintObjectLine("    ", obj);
        }

        Console.WriteLine("  highSlots:");
        foreach (var obj in objects.Where(obj => obj.Score >= 3 && obj.ArrayIndex >= 480).Take(40))
        {
            PrintObjectLine("    ", obj);
        }
    }

    private static long ParseAddress(ProcessMemoryReader reader, string value)
    {
        var text = value.Trim();
        if (text.StartsWith("module+", StringComparison.OrdinalIgnoreCase))
        {
            return reader.Status.ModuleBase + ParseHexOrDecimal(text.Substring("module+".Length));
        }

        if (text.StartsWith(".data+", StringComparison.OrdinalIgnoreCase))
        {
            var data = reader.Status.Sections.FirstOrDefault(section => string.Equals(section.Name, ".data", StringComparison.OrdinalIgnoreCase));
            if (data == null)
            {
                throw new InvalidOperationException(".data section not found");
            }

            return data.StartAddress + ParseHexOrDecimal(text.Substring(".data+".Length));
        }

        return ParseHexOrDecimal(text);
    }

    private static long ParseHexOrDecimal(string value)
    {
        var text = value.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt64(text.Substring(2), 16);
        }

        if (text.Any(ch => (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f')))
        {
            return Convert.ToInt64(text, 16);
        }

        return Convert.ToInt64(text, 10);
    }

    private static List<GameObjectCandidate> ReadTableObjects(ProcessMemoryReader reader, long tableBase, int startIndex, int count)
    {
        var objects = new List<GameObjectCandidate>();
        for (var index = startIndex; index < startIndex + count; index++)
        {
            var slotAddress = tableBase + index * 8L;
            if (!reader.TryReadBytes(slotAddress, 8, out var slotBytes))
            {
                continue;
            }

            var ptr = BitConverter.ToInt64(slotBytes, 0);
            if (!LooksLikeHeapPointer(reader, ptr))
            {
                continue;
            }

            var obj = ReadGameObjectCandidate(reader, ptr, index);
            objects.Add(obj);
        }

        return objects;
    }

    private static string FormatKindCounts(List<GameObjectCandidate> objects)
    {
        var parts = objects
            .Where(obj => obj.Score >= 3 && obj.ObjectIndex == obj.ArrayIndex)
            .GroupBy(obj => obj.ObjectKind)
            .OrderBy(group => group.Key)
            .Select(group => "kind" + group.Key + "=" + group.Count())
            .ToList();
        return parts.Count == 0 ? "<none>" : string.Join(" ", parts);
    }

    private static void DumpTableSlots(ProcessMemoryReader reader, long tableBase, int startIndex, int count)
    {
        Console.WriteLine("    slots " + startIndex + ".." + (startIndex + count - 1) + ":");
        for (var index = startIndex; index < startIndex + count; index++)
        {
            var slotAddress = tableBase + index * 8L;
            if (!reader.TryReadBytes(slotAddress, 8, out var slotBytes))
            {
                Console.WriteLine("      [" + index + "] slot read failed");
                continue;
            }

            var ptr = BitConverter.ToInt64(slotBytes, 0);
            if (!LooksLikeHeapPointer(reader, ptr))
            {
                Console.WriteLine("      [" + index + "] ptr=0x" + ptr.ToString("X") + " empty/invalid");
                continue;
            }

            var obj = ReadGameObjectCandidate(reader, ptr, index);
            PrintObjectLine("      ", obj);
        }
    }

    private static void PrintObjectLine(string indent, GameObjectCandidate obj)
    {
        Console.WriteLine(indent + "[" + obj.ArrayIndex + "] ptr=0x" + obj.Address.ToString("X") + " score=" + obj.Score + " index=" + obj.ObjectIndex + " kind=" + obj.ObjectKind + " entity=0x" + obj.EntityId.ToString("X") + " base=0x" + obj.BaseId.ToString("X") + " owner=0x" + obj.OwnerId.ToString("X") + " pos=(" + FormatFloat(obj.X) + "," + FormatFloat(obj.Y) + "," + FormatFloat(obj.Z) + ") name='" + obj.Name.Replace("'", "?") + "'");
    }

    private static ObjectPointerArrayCandidate ScorePointerArray(ProcessMemoryReader reader, long address, byte[] dataBytes, int offset, int maxEntries)
    {
        var candidate = new ObjectPointerArrayCandidate { Address = address };
        var consecutiveMisses = 0;
        for (var i = 0; i < maxEntries && offset + i * 8 <= dataBytes.Length - 8; i++)
        {
            var ptr = BitConverter.ToInt64(dataBytes, offset + i * 8);
            if (!LooksLikeHeapPointer(reader, ptr))
            {
                consecutiveMisses++;
                if (candidate.Objects.Count > 0 && consecutiveMisses >= 8)
                {
                    break;
                }

                continue;
            }

            var obj = ReadGameObjectCandidate(reader, ptr, i);
            if (obj.Score >= 3)
            {
                candidate.Objects.Add(obj);
                candidate.ValidObjects++;
                candidate.Score += obj.Score;
                candidate.Span = i + 1;
                consecutiveMisses = 0;
            }
            else
            {
                consecutiveMisses++;
                if (candidate.Objects.Count > 0 && consecutiveMisses >= 8)
                {
                    break;
                }
            }
        }

        if (candidate.ValidObjects >= 2)
        {
            var sequential = candidate.Objects.Zip(candidate.Objects.Skip(1), (a, b) => b.ObjectIndex == a.ObjectIndex + 1).Count(value => value);
            candidate.Score += sequential * 2;
        }

        return candidate;
    }

    private static void FinalizePointerArrayCandidate(ObjectPointerArrayCandidate candidate)
    {
        var baseVotes = new Dictionary<long, int>();
        foreach (var obj in candidate.Objects)
        {
            var expectedBase = candidate.Address + ((long)obj.ArrayIndex - obj.ObjectIndex) * 8;
            if (!baseVotes.ContainsKey(expectedBase))
            {
                baseVotes[expectedBase] = 0;
            }

            baseVotes[expectedBase]++;
        }

        if (baseVotes.Count == 0)
        {
            return;
        }

        var winner = baseVotes.OrderByDescending(pair => pair.Value).First();
        candidate.ExpectedBaseAddress = winner.Key;
        candidate.AlignmentHits = winner.Value;
        candidate.Score += winner.Value * 5;
    }

    private static GameObjectCandidate ReadGameObjectCandidate(ProcessMemoryReader reader, long address, int arrayIndex)
    {
        var candidate = new GameObjectCandidate { Address = address, ArrayIndex = arrayIndex };
        if (!LooksLikeUserModePointer(address) || !reader.TryReadBytes(address, GameObjectSize, out var bytes))
        {
            return candidate;
        }

        candidate.EntityId = BitConverter.ToUInt32(bytes, EntityIdOffset);
        candidate.BaseId = BitConverter.ToUInt32(bytes, BaseIdOffset);
        candidate.OwnerId = BitConverter.ToUInt32(bytes, OwnerIdOffset);
        candidate.ObjectIndex = BitConverter.ToUInt16(bytes, ObjectIndexOffset);
        candidate.ObjectKind = bytes[ObjectKindOffset];
        candidate.X = BitConverter.ToSingle(bytes, PositionOffset);
        candidate.Y = BitConverter.ToSingle(bytes, PositionOffset + 4);
        candidate.Z = BitConverter.ToSingle(bytes, PositionOffset + 8);
        candidate.Name = ReadAscii(bytes, NameOffset, 64);

        if (candidate.ObjectIndex < 1000) candidate.Score++;
        if (IsLikelyObjectKind(candidate.ObjectKind)) candidate.Score += 2;
        if (IsReasonablePosition(candidate.X, candidate.Y, candidate.Z)) candidate.Score += 2;
        if (!string.IsNullOrWhiteSpace(candidate.Name)) candidate.Score += 2;
        if (candidate.EntityId != 0 && candidate.EntityId != 0xE0000000) candidate.Score++;
        if (candidate.BaseId < 2000000) candidate.Score++;
        return candidate;
    }

    private static void PrintProcessStatus(ProcessMemoryStatus status)
    {
        Console.WriteLine("Process:");
        Console.WriteLine("  found=" + status.ProcessFound + " pid=" + status.ProcessId + " handle=" + status.HasHandle);
        Console.WriteLine("  module=" + status.ModuleFound + " base=0x" + status.ModuleBase.ToString("X") + " size=" + status.ModuleSize);
        if (!string.IsNullOrWhiteSpace(status.LastError))
        {
            Console.WriteLine("  error=" + status.LastError);
        }

        foreach (var section in status.Sections)
        {
            Console.WriteLine("  " + section.Name + ": start=0x" + section.StartAddress.ToString("X") + " size=" + section.ScanSize);
        }
    }

    private static void PrintPatternScan(PatternScanResult scan)
    {
        Console.WriteLine("Pattern scan:");
        Console.WriteLine("  " + scan.Name + ": hits=" + scan.HitCount + " first=0x" + scan.FirstHitAddress.ToString("X") + " resolved=0x" + scan.ResolvedAddress.ToString("X"));
        Console.WriteLine("  section=" + scan.SectionName);
        if (!string.IsNullOrWhiteSpace(scan.Error))
        {
            Console.WriteLine("  error=" + scan.Error);
        }
    }

    private static void ProbeVerifiedSlot(ProcessMemoryReader reader, long slotAddress)
    {
        Console.WriteLine("Verified slot probe:");
        if (!reader.TryReadBytes(slotAddress, 8, out var slotBytes))
        {
            Console.WriteLine("  slot=0x" + slotAddress.ToString("X") + " read failed: " + reader.Status.LastError);
            return;
        }

        var ptr = BitConverter.ToInt64(slotBytes, 0);
        Console.WriteLine("  slot=0x" + slotAddress.ToString("X") + " ptr=0x" + ptr.ToString("X"));
        PrintGameObjectCandidate(reader, ptr, "slot-ptr");
    }

    private static void ProbeGameObjectPointersFromDataSection(ProcessMemoryReader reader, int maxPointers, int maxPrinted)
    {
        var data = reader.Status.Sections.FirstOrDefault(section => string.Equals(section.Name, ".data", StringComparison.OrdinalIgnoreCase));
        if (data == null || data.ScanSize <= 0)
        {
            Console.WriteLine("  .data section not found");
            return;
        }

        var bytesToRead = Math.Min(data.ScanSize, 0x200000);
        if (!reader.TryReadBytes(data.StartAddress, bytesToRead, out var bytes))
        {
            Console.WriteLine("  .data read failed: " + reader.Status.LastError);
            return;
        }

        var printed = 0;
        var checkedPointers = 0;
        for (var offset = 0; offset <= bytes.Length - 8 && checkedPointers < maxPointers && printed < maxPrinted; offset += 8)
        {
            var ptr = BitConverter.ToInt64(bytes, offset);
            if (!LooksLikeUserModePointer(ptr))
            {
                continue;
            }

            checkedPointers++;
            var score = ScoreGameObjectCandidate(reader, ptr, out var summary);
            if (score < 3)
            {
                continue;
            }

            Console.WriteLine("  .data+0x" + offset.ToString("X") + " -> 0x" + ptr.ToString("X") + " score=" + score + " " + summary);
            printed++;
        }

        if (printed == 0)
        {
            Console.WriteLine("  no strong GameObject pointer candidates in first 0x" + bytesToRead.ToString("X") + " bytes of .data");
        }
    }

    private static void PrintGameObjectCandidate(ProcessMemoryReader reader, long address, string label)
    {
        var score = ScoreGameObjectCandidate(reader, address, out var summary);
        Console.WriteLine("  " + label + " score=" + score + " " + summary);
    }

    private static int ScoreGameObjectCandidate(ProcessMemoryReader reader, long address, out string summary)
    {
        var candidate = ReadGameObjectCandidate(reader, address, 0);
        summary = "addr=0x" + address.ToString("X");
        if (candidate.Score == 0)
        {
            summary += " read=fail";
            return 0;
        }

        summary += " index=" + candidate.ObjectIndex + " kind=" + candidate.ObjectKind + " pos=(" + FormatFloat(candidate.X) + "," + FormatFloat(candidate.Y) + "," + FormatFloat(candidate.Z) + ") name='" + candidate.Name.Replace("'", "?") + "'";
        return candidate.Score;
    }

    private static bool LooksLikeUserModePointer(long value)
    {
        return value > 0x100000000L && value < 0x0000800000000000L;
    }

    private static bool LooksLikeHeapPointer(ProcessMemoryReader reader, long value)
    {
        return LooksLikeUserModePointer(value) && FindSectionName(reader, value) == "<outside>";
    }

    private static string FindSectionName(ProcessMemoryReader reader, long address)
    {
        foreach (var section in reader.Status.Sections)
        {
            if (section.ScanSize <= 0)
            {
                continue;
            }

            if (address >= section.StartAddress && address < section.StartAddress + section.ScanSize)
            {
                return section.Name;
            }
        }

        return "<outside>";
    }

    private static bool IsLikelyObjectKind(byte value)
    {
        return value >= 1 && value <= 14;
    }

    private static bool IsReasonablePosition(float x, float y, float z)
    {
        return IsFinite(x) && IsFinite(y) && IsFinite(z)
            && Math.Abs(x) < 100000f
            && Math.Abs(y) < 100000f
            && Math.Abs(z) < 100000f
            && (Math.Abs(x) + Math.Abs(y) + Math.Abs(z)) > 0.001f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("G4", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ReadAscii(byte[] bytes, int offset, int maxLength)
    {
        var chars = new List<char>();
        for (var i = 0; i < maxLength && offset + i < bytes.Length; i++)
        {
            var b = bytes[offset + i];
            if (b == 0)
            {
                break;
            }

            if (b < 32 || b > 126)
            {
                return string.Empty;
            }

            chars.Add((char)b);
        }

        return new string(chars.ToArray());
    }

    private sealed class RipRefCandidate
    {
        public long Instruction { get; set; }
        public int Offset { get; set; }
        public int InstructionLength { get; set; }
        public string Op { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }

    private sealed class ObjectPointerArrayCandidate
    {
        public long Address { get; set; }
        public int ValidObjects { get; set; }
        public int Span { get; set; }
        public int Score { get; set; }
        public long ExpectedBaseAddress { get; set; }
        public int AlignmentHits { get; set; }
        public List<GameObjectCandidate> Objects { get; } = new List<GameObjectCandidate>();
    }

    private sealed class GameObjectCandidate
    {
        public long Address { get; set; }
        public int ArrayIndex { get; set; }
        public int Score { get; set; }
        public uint EntityId { get; set; }
        public uint BaseId { get; set; }
        public uint OwnerId { get; set; }
        public ushort ObjectIndex { get; set; }
        public byte ObjectKind { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
