using System.Numerics;
using SkiaSharp;

namespace BFGA.Canvas.Rendering;

public static class LaserTrailRenderer
{
    private const float DefaultDecayMs = 1500f;
    private const float LocalDotScreenRadius = 5f;
    private const float LocalTrailScreenWidth = 3f;
    private const float PingCenterDotScreenRadius = 4f;
    private const float PingRingStartScreenRadius = 10f;
    private const float PingRingEndScreenRadius = 24f;
    private const float DefaultPingLifetimeMs = 2400f;

    public static void DrawLaserTrails(
        SKCanvas canvas,
        IReadOnlyDictionary<Guid, RemoteLaserState>? lasers,
        long currentTimestampMs,
        float zoom = 1f)
    {
        if (lasers is null || lasers.Count == 0)
            return;

        foreach (var state in lasers.Values)
        {
            var points = state.Trail.GetPoints();
            if (points.Length == 0)
                continue;

            if (points.Length == 1)
            {
                float age = currentTimestampMs - points[0].TimestampMs;
                float t = Math.Clamp(age / DefaultDecayMs, 0f, 1f);
                float alpha = 1f - (t * t);
                if (alpha > 0f)
                    DrawLaserDot(canvas, points[0].Position, state.Color, alpha, GetWorldSize(LocalDotScreenRadius, zoom));
                continue;
            }

            using var paint = new SKPaint
            {
                StrokeWidth = GetWorldSize(LocalTrailScreenWidth, zoom),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round
            };

            for (int i = 1; i < points.Length; i++)
            {
                var prev = points[i - 1];
                var curr = points[i];

                float age = currentTimestampMs - curr.TimestampMs;
                float t = Math.Clamp(age / DefaultDecayMs, 0f, 1f);
                float alpha = 1f - (t * t);

                if (alpha <= 0f)
                    continue;

                paint.Color = state.Color.WithAlpha((byte)(alpha * 255));
                canvas.DrawLine(prev.Position.X, prev.Position.Y, curr.Position.X, curr.Position.Y, paint);
            }
        }
    }

    public static bool HasVisibleTrails(
        IReadOnlyDictionary<Guid, RemoteLaserState>? lasers,
        long currentTimestampMs,
        float decayMs = DefaultDecayMs)
    {
        if (lasers is null || lasers.Count == 0)
            return false;

        foreach (var state in lasers.Values)
        {
            var points = state.Trail.GetPoints();
            for (int i = 0; i < points.Length; i++)
            {
                float age = currentTimestampMs - points[i].TimestampMs;
                if (age < decayMs)
                    return true;
            }
        }

        return false;
    }

    public static float GetWorldSize(float screenSize, float zoom)
    {
        return zoom <= 0f ? screenSize : screenSize / zoom;
    }

    public static bool HasVisibleLocalLaser(
        LocalLaserState? laser,
        long currentTimestampMs,
        float decayMs = DefaultDecayMs)
    {
        if (laser is null)
            return false;

        if (laser.IsActive)
            return true;

        var points = laser.Trail.GetPoints();
        for (int i = 0; i < points.Length; i++)
        {
            float age = currentTimestampMs - points[i].TimestampMs;
            if (age < decayMs)
                return true;
        }

        return false;
    }

    public static bool HasVisiblePing(
        PingMarkerState? ping,
        long currentTimestampMs,
        float lifetimeMs = DefaultPingLifetimeMs)
    {
        if (ping is null)
            return false;

        return currentTimestampMs - ping.StartedAtMs < lifetimeMs;
    }

    public static float GetPingRingScreenRadius(
        long startedAtMs,
        long currentTimestampMs,
        float lifetimeMs = DefaultPingLifetimeMs)
    {
        float elapsedMs = Math.Max(0f, currentTimestampMs - startedAtMs);
        float progress = lifetimeMs <= 0f ? 1f : Math.Clamp(elapsedMs / lifetimeMs, 0f, 1f);
        return PingRingStartScreenRadius + ((PingRingEndScreenRadius - PingRingStartScreenRadius) * progress);
    }

    public static void DrawLocalLaser(
        SKCanvas canvas,
        LocalLaserState? laser,
        long currentTimestampMs,
        float zoom)
    {
        if (laser is null)
            return;

        var points = laser.Trail.GetPoints();
        if (points.Length > 1)
        {
            using var paint = new SKPaint
            {
                StrokeWidth = GetWorldSize(3f, zoom),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round
            };

            for (int i = 1; i < points.Length; i++)
            {
                var prev = points[i - 1];
                var curr = points[i];

                float age = currentTimestampMs - curr.TimestampMs;
                float t = Math.Clamp(age / DefaultDecayMs, 0f, 1f);
                float alpha = 1f - (t * t);
                if (alpha <= 0f)
                    continue;

                paint.Color = laser.Color.WithAlpha((byte)(alpha * 255));
                canvas.DrawLine(prev.Position.X, prev.Position.Y, curr.Position.X, curr.Position.Y, paint);
            }
        }
        else if (points.Length == 1 && !laser.IsActive)
        {
            float age = currentTimestampMs - points[0].TimestampMs;
            float t = Math.Clamp(age / DefaultDecayMs, 0f, 1f);
            float alpha = 1f - (t * t);
            if (alpha > 0f)
            {
                DrawLaserDot(
                    canvas,
                    points[0].Position,
                    laser.Color,
                    alpha,
                    GetWorldSize(5f, zoom));
            }
        }

        if (laser.IsActive)
        {
            DrawLaserDot(
                canvas,
                laser.HeadPosition,
                laser.Color,
                1f,
                GetWorldSize(5f, zoom));
        }
    }

    public static void DrawPingMarker(
        SKCanvas canvas,
        PingMarkerState? ping,
        long currentTimestampMs,
        float zoom)
    {
        if (ping is null)
            return;

        float elapsedMs = Math.Max(0f, currentTimestampMs - ping.StartedAtMs);
        float progress = Math.Clamp(elapsedMs / DefaultPingLifetimeMs, 0f, 1f);
        float alpha = 1f - progress;
        if (alpha <= 0f)
            return;

        byte pingAlpha = (byte)(alpha * 255);

        using var ringPaint = new SKPaint
        {
            Color = ping.Color.WithAlpha(pingAlpha),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = GetWorldSize(2f, zoom)
        };

        canvas.DrawCircle(
            ping.Position.X,
            ping.Position.Y,
            GetWorldSize(GetPingRingScreenRadius(ping.StartedAtMs, currentTimestampMs), zoom),
            ringPaint);

        DrawLaserDot(
            canvas,
            ping.Position,
            ping.Color,
            alpha,
            GetWorldSize(PingCenterDotScreenRadius, zoom));
    }

    private static void DrawLaserDot(
        SKCanvas canvas,
        Vector2 position,
        SKColor color,
        float alpha,
        float radius = 4f)
    {
        using var paint = new SKPaint
        {
            Color = color.WithAlpha((byte)(alpha * 255)),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawCircle(position.X, position.Y, radius, paint);
    }
}
