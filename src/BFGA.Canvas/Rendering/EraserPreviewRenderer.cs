using SkiaSharp;

namespace BFGA.Canvas.Rendering;

public static class EraserPreviewRenderer
{
    public static void Draw(SKCanvas canvas, EraserPreviewState preview, float zoom)
    {
        if (!preview.IsActive || preview.Radius <= 0f)
            return;

        var safeZoom = zoom <= 0f ? 1f : zoom;
        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(255, 255, 255, 28)
        };
        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(255, 255, 255, 180),
            StrokeWidth = MathF.Max(1.25f / safeZoom, 0.75f)
        };

        canvas.DrawCircle(preview.Center.X, preview.Center.Y, preview.Radius, fillPaint);
        canvas.DrawCircle(preview.Center.X, preview.Center.Y, preview.Radius, strokePaint);
    }
}
