using System;
using System.Collections.Generic;
using System.Linq;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public static class EntityDisplayPipeline
{
    public static IReadOnlyList<EntityDisplayState> BuildVisibleStates(
        IEnumerable<EntitySnapshot> entities,
        CameraSnapshot camera,
        EspConfig config,
        PartyContext party,
        ISet<uint>? pinnedEntityIds = null)
    {
        pinnedEntityIds ??= new HashSet<uint>();
        var states = new List<EntityDisplayState>();

        foreach (var entity in entities)
        {
            var filter = FilterService.Evaluate(entity, config, party);
            var showFilteredDebug = !filter.ShouldDisplay && config.ShowFilteredEntitiesForDebug;
            if (!filter.ShouldDisplay && !showFilteredDebug)
            {
                continue;
            }

            var labelWorld = ProjectionService.GetLabelWorldPosition(entity);
            if (!ProjectionService.WorldToScreen(labelWorld, camera, out var screen))
            {
                continue;
            }

            var hasCenter = ProjectionService.WorldToScreen(entity.Position, camera, out var centerScreen);
            var pinned = pinnedEntityIds.Contains(entity.EntityId);
            var state = DisplayStateBuilder.Build(entity, screen, hasCenter ? centerScreen : null, pinned, config, filterReason: filter.Reason);
            state.IsFilteredDebug = showFilteredDebug;
            states.Add(state);
        }

        return states
            .OrderBy(state => state.ScreenPosition.Y)
            .Take(Math.Max(0, config.MaxDisplayedEntities))
            .ToArray();
    }
}
