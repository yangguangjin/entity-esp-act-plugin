using System;
using System.Numerics;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public static class ProjectionService
{
    public static bool WorldToScreen(Vector3 world, CameraSnapshot camera, out Vector2 screen)
    {
        screen = default;
        if (!camera.IsValid || camera.Viewport.Width <= 0 || camera.Viewport.Height <= 0)
        {
            return false;
        }

        var clip = Vector4.Transform(new Vector4(world, 1f), camera.ViewProjectionMatrix);
        if (clip.W <= 0.01f)
        {
            return false;
        }

        var ndcX = clip.X / clip.W;
        var ndcY = clip.Y / clip.W;
        if (ndcX < -1f || ndcX > 1f || ndcY < -1f || ndcY > 1f)
        {
            return false;
        }

        screen = new Vector2(
            camera.Viewport.Left + (ndcX + 1f) * 0.5f * camera.Viewport.Width,
            camera.Viewport.Top + (1f - ndcY) * 0.5f * camera.Viewport.Height);
        return true;
    }

    public static Vector3 GetLabelWorldPosition(EntitySnapshot entity, bool yAxisIsHeight = true)
    {
        var headOffset = Math.Max(1.8f, entity.HitboxRadius * 1.2f);
        return yAxisIsHeight
            ? entity.Position + new Vector3(0, headOffset, 0)
            : entity.Position + new Vector3(0, 0, headOffset);
    }
}
