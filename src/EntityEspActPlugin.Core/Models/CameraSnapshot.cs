using System.Numerics;

namespace EntityEspActPlugin.Core.Models;

public readonly struct ViewportRect
{
    public ViewportRect(float left, float top, float width, float height)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    public float Left { get; }
    public float Top { get; }
    public float Width { get; }
    public float Height { get; }
}

public sealed class CameraSnapshot
{
    public Matrix4x4 ViewMatrix { get; set; }
    public Matrix4x4 ProjectionMatrix { get; set; }
    public Matrix4x4 ViewProjectionMatrix { get; set; }
    public Vector3 CameraPosition { get; set; }
    public ViewportRect Viewport { get; set; }
    public bool IsValid { get; set; }
}
