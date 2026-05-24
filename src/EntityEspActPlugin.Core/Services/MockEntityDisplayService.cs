using System.Collections.Generic;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public static class MockEntityDisplayService
{
    public static IReadOnlyList<EntityDisplayState> BuildStates(int width, int height)
    {
        return new DisplayStateService(new MockEntitySource(), new MockCameraSource())
            .BuildStates(width, height, new EspConfig { MaxDisplayedEntities = 10, ShowCastBar = true });
    }
}
