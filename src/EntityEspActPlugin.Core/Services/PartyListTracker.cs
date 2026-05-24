using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EntityEspActPlugin.Core.Services;

public sealed class PartyListTracker
{
    private readonly object _syncRoot = new object();
    private readonly HashSet<uint> _partyEntityIds = new HashSet<uint>();

    public IReadOnlyCollection<uint> PartyEntityIds
    {
        get
        {
            lock (_syncRoot)
            {
                return _partyEntityIds.ToArray();
            }
        }
    }

    public void ObserveLogLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        HashSet<uint>? ids = null;
        if (RelatedActLogStore.GetLineType(line) == "0B")
        {
            ids = ParseParsedPartyList(line);
        }
        else if (line.StartsWith("11|", StringComparison.Ordinal))
        {
            ids = ParseNetworkPartyList(line);
        }

        if (ids == null || ids.Count == 0)
        {
            return;
        }

        lock (_syncRoot)
        {
            _partyEntityIds.Clear();
            foreach (var id in ids)
            {
                _partyEntityIds.Add(id);
            }
        }
    }

    private static HashSet<uint>? ParseParsedPartyList(string line)
    {
        var fields = ExtractFields(line, "0B");
        if (fields.Length < 2 || !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
        {
            return null;
        }

        return ParseEntityIds(fields.Skip(1), count);
    }

    private static HashSet<uint>? ParseNetworkPartyList(string line)
    {
        var fields = line.Split('|');
        if (fields.Length < 4 || !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
        {
            return null;
        }

        return ParseEntityIds(fields.Skip(3), count == 8 && fields.Length >= 27 ? 24 : count);
    }

    private static HashSet<uint> ParseEntityIds(IEnumerable<string> tokens, int count)
    {
        var ids = new HashSet<uint>();
        var limit = Math.Min(Math.Max(count, 0), 24);
        foreach (var token in tokens.Take(limit))
        {
            if (token.Length == 8 && uint.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id) && id != 0)
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static string[] ExtractFields(string line, string type)
    {
        var marker = type + ":";
        var searchStart = 0;
        var timestampEnd = line.IndexOf(']');
        if (timestampEnd >= 0 && timestampEnd + 1 < line.Length)
        {
            searchStart = timestampEnd + 1;
        }

        var start = line.IndexOf(marker, searchStart, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return Array.Empty<string>();
        }

        return line.Substring(start + marker.Length).Split(':');
    }
}
