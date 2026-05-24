using System;
using System.Collections.Generic;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public static class MockOverlayPointService
{
    public static IReadOnlyList<OverlayPoint> CreatePoints(int width, int height)
    {
        var safeWidth = Math.Max(1, width);
        var safeHeight = Math.Max(1, height);
        return new List<OverlayPoint>
        {
            new OverlayPoint(safeWidth * 0.5f, safeHeight * 0.5f, "Center mock"),
            new OverlayPoint(safeWidth * 0.25f, safeHeight * 0.35f, "Left mock"),
            new OverlayPoint(safeWidth * 0.75f, safeHeight * 0.35f, "Right mock"),
            new OverlayPoint(safeWidth * 0.35f, safeHeight * 0.65f, "Near mock"),
            new OverlayPoint(safeWidth * 0.65f, safeHeight * 0.65f, "Far mock"),
        };
    }
}
