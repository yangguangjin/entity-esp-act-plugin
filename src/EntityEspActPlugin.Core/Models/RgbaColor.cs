namespace EntityEspActPlugin.Core.Models;

public readonly struct RgbaColor
{
    public RgbaColor(int a, int r, int g, int b)
    {
        A = a;
        R = r;
        G = g;
        B = b;
    }

    public int A { get; }
    public int R { get; }
    public int G { get; }
    public int B { get; }
}
