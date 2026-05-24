using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public static class DisplayStateBuilder
{
    public const uint NormalColor = 0xFFFFFFFF;
    public const uint CastingColor = 0xFFFF6040;
    public const uint PinnedColor = 0xFFFFCC00;

    public static EntityDisplayState Build(
        EntitySnapshot entity,
        Vector2 screenPosition,
        Vector2? centerScreenPosition,
        bool pinned,
        EspConfig config,
        DateTime? pinExpireAt = null,
        string filterReason = "")
    {
        return new EntityDisplayState
        {
            Snapshot = entity,
            ScreenPosition = screenPosition,
            CenterScreenPosition = centerScreenPosition ?? default,
            HasCenterScreenPosition = centerScreenPosition.HasValue,
            OnScreen = true,
            LabelText = BuildLabel(entity, config),
            Color = pinned ? PinnedColor : entity.IsCasting ? CastingColor : NormalColor,
            Pinned = pinned,
            PinExpireAt = pinExpireAt ?? DateTime.MinValue,
            FilterReason = config.ShowFilteredEntitiesForDebug ? filterReason : string.Empty,
            CastProgress = CalculateCastProgress(entity),
        };
    }

    public static string BuildLabel(EntitySnapshot entity, EspConfig? config = null)
    {
        var fields = config?.LabelFields ?? new LabelFieldConfig();
        var parts = new List<string>();
        if (fields.EntityId)
        {
            parts.Add("EntityId:" + entity.EntityIdHex);
        }

        if (fields.Kind)
        {
            parts.Add("Kind:" + entity.Kind);
        }

        if (fields.Distance)
        {
            parts.Add("Dist:" + entity.DistanceToPlayer.ToString("0.0", CultureInfo.InvariantCulture) + "m");
        }

        if (fields.BNpcId && entity.BNpcId != 0)
        {
            parts.Add("BNpcId:" + entity.BNpcId);
        }

        if (fields.BNpcNameId && entity.BNpcNameId != 0)
        {
            parts.Add("NameId:" + entity.BNpcNameId);
        }

        if (fields.BNpcName && !string.IsNullOrWhiteSpace(entity.Name))
        {
            parts.Add("BNpcName:" + entity.Name);
        }

        if (fields.EObjNameId && entity.EObjNameId != 0)
        {
            parts.Add("EObjNameId:" + entity.EObjNameId);
        }

        var lines = new List<string>();
        if (parts.Count > 0)
        {
            lines.Add(string.Join(" | ", parts));
        }

        if (entity.IsCasting && fields.Cast)
        {
            var castLine = string.Format(
                CultureInfo.InvariantCulture,
                "CastId:{0:X} Cast:{1:0.0}/{2:0.0}",
                entity.CastId,
                entity.CastCurrent,
                entity.CastMax);
            if (fields.CastTargetId && entity.CastTargetId != 0)
            {
                castLine += " Target:" + entity.CastTargetId.ToString("X8", CultureInfo.InvariantCulture);
            }

            lines.Add(castLine);
        }

        return lines.Count == 0 ? string.Empty : string.Join(Environment.NewLine, lines);
    }

    public static float CalculateCastProgress(EntitySnapshot entity)
    {
        if (!entity.IsCasting || entity.CastMax <= 0)
        {
            return 0f;
        }

        var progress = entity.CastCurrent / entity.CastMax;
        if (progress < 0f)
        {
            return 0f;
        }

        return progress > 1f ? 1f : progress;
    }
}
