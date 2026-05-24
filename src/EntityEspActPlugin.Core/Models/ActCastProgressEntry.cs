using System;

namespace EntityEspActPlugin.Core.Models;

public sealed class ActCastProgressEntry
{
    public uint SourceEntityId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public uint AbilityId { get; set; }
    public string AbilityName { get; set; } = string.Empty;
    private const float MinCastSeconds = 0.1f;
    private const float MaxCastSeconds = 3600f;

    public float CastSeconds { get; set; }
    public DateTime StartedAt { get; set; }

    public DateTime EndsAt
    {
        get
        {
            var seconds = GetClampedCastSeconds();
            if (StartedAt > DateTime.MaxValue.AddSeconds(-seconds))
            {
                return DateTime.MaxValue;
            }

            return StartedAt.AddSeconds(seconds);
        }
    }

    public float GetClampedCastSeconds()
    {
        if (float.IsNaN(CastSeconds) || float.IsInfinity(CastSeconds))
        {
            return MinCastSeconds;
        }

        return Math.Max(MinCastSeconds, Math.Min(MaxCastSeconds, CastSeconds));
    }

    public float GetProgress(DateTime now)
    {
        if (CastSeconds <= 0f)
        {
            return 1f;
        }

        var elapsed = (float)(now - StartedAt).TotalSeconds;
        if (elapsed <= 0f)
        {
            return 0f;
        }

        return Math.Max(0f, Math.Min(1f, elapsed / GetClampedCastSeconds()));
    }

    public float GetRemainingSeconds(DateTime now)
    {
        return Math.Max(0f, (float)(EndsAt - now).TotalSeconds);
    }
}
