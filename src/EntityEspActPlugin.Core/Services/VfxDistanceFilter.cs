using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public static class VfxDistanceFilter
{
    public static IReadOnlyList<VfxMonitorEntry> Filter(
        IEnumerable<VfxMonitorEntry> entries,
        VfxAnchorContext anchor,
        float maxDistance,
        bool showFallbackWithoutPosition)
    {
        if (entries == null)
        {
            return Array.Empty<VfxMonitorEntry>();
        }

        var effectiveMaxDistance = Math.Max(0f, maxDistance);
        var result = new List<VfxMonitorEntry>();
        foreach (var entry in entries)
        {
            if (entry == null)
            {
                continue;
            }

            var clone = entry.Clone();
            if (anchor.Mode == VfxAnchorMode.AllField)
            {
                result.Add(clone);
                continue;
            }

            if (clone.Position.HasValue && anchor.HasPosition)
            {
                clone.Distance = DistanceXZ(anchor.Position, clone.Position.Value);
                if (clone.Distance.Value <= effectiveMaxDistance)
                {
                    result.Add(clone);
                }

                continue;
            }

            clone.Distance = null;
            if (clone.Source == VfxEntrySource.PathScanFallback && showFallbackWithoutPosition)
            {
                result.Add(clone);
            }
        }

        return result
            .OrderBy(entry => entry.Distance.HasValue ? 0 : 1)
            .ThenBy(entry => entry.Distance ?? float.MaxValue)
            .ThenByDescending(entry => entry.LastSeenAt)
            .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static float DistanceXZ(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return (float)Math.Sqrt(dx * dx + dz * dz);
    }
}
