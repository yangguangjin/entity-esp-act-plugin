using System.Collections.Generic;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public interface IEntitySource
{
    IReadOnlyList<EntitySnapshot> GetEntities();
}
