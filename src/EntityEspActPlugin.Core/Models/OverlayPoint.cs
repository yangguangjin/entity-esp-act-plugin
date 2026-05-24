namespace EntityEspActPlugin.Core.Models;

public sealed class OverlayPoint
{
    public OverlayPoint(float x, float y, string label)
    {
        X = x;
        Y = y;
        Label = label;
    }

    public float X { get; }
    public float Y { get; }
    public string Label { get; }
}
