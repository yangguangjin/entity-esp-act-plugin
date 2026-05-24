using System.Numerics;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class MockCameraSource : ICameraSource
{
    public CameraSnapshot GetCamera(int width, int height)
    {
        return new CameraSnapshot
        {
            IsValid = true,
            ViewProjectionMatrix = Matrix4x4.Identity,
            Viewport = new ViewportRect(0, 0, width, height),
        };
    }
}
