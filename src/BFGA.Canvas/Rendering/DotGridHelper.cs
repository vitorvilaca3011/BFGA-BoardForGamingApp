using System.Numerics;
using SkiaSharp;

namespace BFGA.Canvas.Rendering;

public static class DotGridHelper
{
    public static IEnumerable<Vector2> GetDotPositions(SKRect bounds, Vector2 origin, float spacing)
        => GetDotPositions(bounds, origin, spacing, 1f);

    public static IEnumerable<Vector2> GetDotPositions(SKRect bounds, Vector2 origin, float spacing, float zoomScale)
    {
        if (spacing <= 0f || zoomScale <= 0f)
            yield break;

        var firstX = origin.X + MathF.Ceiling((bounds.Left - origin.X) / spacing) * spacing;
        var firstY = origin.Y + MathF.Ceiling((bounds.Top - origin.Y) / spacing) * spacing;

        for (var y = firstY; y <= bounds.Bottom; y += spacing)
        {
            for (var x = firstX; x <= bounds.Right; x += spacing)
            {
                yield return new Vector2(x, y);
            }
        }
    }

    public static float GetVisibleDotRadius(float baseRadius, float zoomScale)
        => zoomScale <= 0f ? baseRadius : baseRadius / MathF.Max(1f, zoomScale);

    public static SKRect GetVisibleBoardBounds(SKRect clipBounds)
    {
        if (clipBounds.IsEmpty)
            return clipBounds;

        return clipBounds;
    }

    public static void DrawDots(SKCanvas canvas, SKRect bounds, Vector2 origin, float spacing, SKColor dotColor, float dotRadius, float zoomScale = 1f)
    {
        using var paint = new SKPaint
        {
            Color = dotColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        foreach (var dot in GetDotPositions(bounds, origin, spacing, zoomScale))
        {
            canvas.DrawCircle(dot.X, dot.Y, Math.Max(0.5f, GetVisibleDotRadius(dotRadius, zoomScale)), paint);
        }
    }
}
