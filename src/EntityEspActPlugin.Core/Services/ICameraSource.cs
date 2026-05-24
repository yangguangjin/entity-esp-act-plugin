using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public interface ICameraSource
{
    CameraSnapshot GetCamera(int width, int height);
}
