using System;
using System.Collections.Generic;
using System.Linq;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public static class RipCandidateScanner
{
    private static readonly byte[][] Prefixes =
    {
        new byte[] { 0x48, 0x8B, 0x05 },
        new byte[] { 0x48, 0x8D, 0x0D },
        new byte[] { 0x48, 0x89, 0x05 },
    };

    public static RipCandidateScanResult Scan(byte[] data, long baseAddress, IReadOnlyList<PeSectionInfo> sections, string sectionName = ".text", int limit = 40, int minRefs = 1, int maxRefs = int.MaxValue)
    {
        var result = new RipCandidateScanResult
        {
            SectionName = sectionName,
            ScannedBytes = data?.Length ?? 0,
            MinRefs = minRefs,
            MaxRefs = maxRefs,
        };

        if (data == null || data.Length < 7)
        {
            result.Error = "not enough bytes to scan";
            return result;
        }

        var byResolved = new Dictionary<long, RipCandidate>();
        for (var i = 0; i <= data.Length - 7; i++)
        {
            var prefix = Prefixes.FirstOrDefault(item => Matches(data, i, item));
            if (prefix == null)
            {
                continue;
            }

            var instructionAddress = baseAddress + i;
            if (!RelativeAddressResolver.TryResolveRipRelative(data, i, instructionAddress, 3, 7, out var resolved, out _))
            {
                continue;
            }

            var opcode = BitConverter.ToString(prefix).Replace('-', ' ');
            if (!byResolved.TryGetValue(resolved, out var candidate))
            {
                candidate = new RipCandidate
                {
                    Opcode = opcode,
                    Opcodes = new List<string> { opcode },
                    InstructionAddress = instructionAddress,
                    ResolvedAddress = resolved,
                    TargetSection = FindTargetSection(sections, resolved),
                    ReferenceCount = 1,
                };
                byResolved[resolved] = candidate;
            }
            else
            {
                candidate.ReferenceCount++;
                if (!candidate.Opcodes.Contains(opcode))
                {
                    candidate.Opcodes.Add(opcode);
                }
            }
        }

        var ordered = byResolved.Values
            .OrderByDescending(candidate => candidate.ReferenceCount)
            .ThenBy(candidate => candidate.ResolvedAddress)
            .ToList();
        result.TotalGroups = ordered.Count;
        var filtered = ordered
            .Where(candidate => candidate.ReferenceCount >= minRefs && candidate.ReferenceCount <= maxRefs)
            .ToList();
        result.FilteredGroups = filtered.Count;
        result.Candidates = filtered.Take(limit).ToList();
        PopulateDisplayedCandidateSignatures(data, baseAddress, result.Candidates);
        return result;
    }

    private static void PopulateDisplayedCandidateSignatures(byte[] data, long baseAddress, IReadOnlyList<RipCandidate> candidates)
    {
        foreach (var candidate in candidates)
        {
            var offset = checked((int)(candidate.InstructionAddress - baseAddress));
            candidate.CandidateSignature = BuildCandidateSignature(data, offset);
            candidate.CandidateSignatureHits = CountSignatureHits(data, candidate.CandidateSignature);
        }
    }

    private static string BuildCandidateSignature(byte[] data, int offset)
    {
        var length = Math.Min(16, data.Length - offset);
        var parts = new List<string>();
        for (var i = 0; i < length; i++)
        {
            parts.Add(i >= 3 && i <= 6 ? "??" : data[offset + i].ToString("X2"));
        }

        return string.Join(" ", parts);
    }

    private static int CountSignatureHits(byte[] data, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return 0;
        }

        var pattern = signature.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var hits = 0;
        for (var offset = 0; offset <= data.Length - pattern.Length; offset++)
        {
            var matches = true;
            for (var i = 0; i < pattern.Length; i++)
            {
                if (pattern[i] == "?" || pattern[i] == "??")
                {
                    continue;
                }

                if (data[offset + i] != Convert.ToByte(pattern[i], 16))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                hits++;
                if (hits > 99)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    private static bool Matches(byte[] data, int offset, byte[] prefix)
    {
        if (offset + prefix.Length > data.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix.Length; i++)
        {
            if (data[offset + i] != prefix[i])
            {
                return false;
            }
        }

        return true;
    }

    private static string FindTargetSection(IReadOnlyList<PeSectionInfo> sections, long address)
    {
        foreach (var section in sections)
        {
            var start = section.StartAddress;
            var end = start + Math.Max(0, section.ScanSize);
            if (address >= start && address < end)
            {
                return section.Name;
            }
        }

        return "<outside>";
    }
}
