using BFGA.Canvas.Tools;
using BFGA.Core;
using BFGA.Core.Models;
using SkiaSharp;

namespace BFGA.Canvas.Rendering;

public static class SelectionOverlayRenderer
{
    public static void Draw(SKCanvas canvas, BoardState board, SelectionOverlayState? overlay, float zoom = 1f)
    {
        if (overlay is null)
            return;

        float scale = zoom > 0f ? 1f / zoom : 1f;
        DrawSelectionOutlines(canvas, board, overlay.SelectedElementIds, scale);
        DrawSelectionHandles(canvas, overlay.Handles, scale);
        DrawSelectionBox(canvas, overlay.SelectionBox, scale);
    }

    private static float GetScaledVisualSize(float size, float scale) => size * scale;

    private static void DrawSelectionOutlines(SKCanvas canvas, BoardState board, IReadOnlyCollection<Guid> selectedElementIds, float scale)
    {
        if (selectedElementIds.Count == 0)
            return;

        using var outlinePaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = GetScaledVisualSize(1f, scale),
        };

        foreach (var element in board.Elements)
        {
            if (!selectedElementIds.Contains(element.Id))
                continue;

            canvas.DrawRect(ElementBoundsHelper.GetBounds(element), outlinePaint);
        }
    }

    private static void DrawSelectionHandles(SKCanvas canvas, IReadOnlyList<SelectionHandle> handles, float scale)
    {
        if (handles.Count == 0)
            return;

        using var fillPaint = new SKPaint { Color = SKColors.White, IsAntialias = false, Style = SKPaintStyle.Fill };
        using var borderPaint = new SKPaint
        {
            Color = ThemeColors.BgSurface,
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = GetScaledVisualSize(1f, scale),
        };

        foreach (var handle in handles)
        {
            float radius = GetScaledVisualSize(handle.HitRadius * 0.5f, scale);
            canvas.DrawCircle(handle.Position.X, handle.Position.Y, radius, fillPaint);
            canvas.DrawCircle(handle.Position.X, handle.Position.Y, radius, borderPaint);
        }
    }

    private static void DrawSelectionBox(SKCanvas canvas, SKRect? selectionBox, float scale)
    {
        if (selectionBox is null)
            return;

        var rect = NormalizeRect(selectionBox.Value);
        using var boxPaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(160),
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = GetScaledVisualSize(1f, scale),
            PathEffect = SKPathEffect.CreateDash(new[] { GetScaledVisualSize(4f, scale), GetScaledVisualSize(4f, scale) }, 0f),
        };

        canvas.DrawRect(rect, boxPaint);
    }

    private static SKRect NormalizeRect(SKRect rect)
    {
        float left = Math.Min(rect.Left, rect.Right);
        float right = Math.Max(rect.Left, rect.Right);
        float top = Math.Min(rect.Top, rect.Bottom);
        float bottom = Math.Max(rect.Top, rect.Bottom);
        return new SKRect(left, top, right, bottom);
    }
}
