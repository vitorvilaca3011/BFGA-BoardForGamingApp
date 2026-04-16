using System.Numerics;
using SkiaSharp;

namespace BFGA.Canvas.Rendering;

public static class LaserTrailRenderer
{
    private const float DefaultDecayMs = 1500f;

    public static void DrawLaserTrails(
        SKCanvas canvas,
        IReadOnlyDictionary<Guid, RemoteLaserState>? lasers,
        long currentTimestampMs)
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
                    DrawLaserDot(canvas, points[0].Position, state.Color, alpha);
                continue;
            }

            using var paint = new SKPaint
            {
                StrokeWidth = 3f,
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

    private static void DrawLaserDot(SKCanvas canvas, Vector2 position, SKColor color, float alpha)
    {
        using var paint = new SKPaint
        {
            Color = color.WithAlpha((byte)(alpha * 255)),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawCircle(position.X, position.Y, 4f, paint);
    }
}
