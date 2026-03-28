using System.Numerics;
using SkiaSharp;

namespace BFGA.Canvas.Rendering;

public static class CollaboratorOverlayHelper
{
    public const byte RemoteStrokePreviewAlpha = 153;

    public static SKColor GetRemoteStrokePreviewColor(SKColor assignedColor)
    {
        return assignedColor.WithAlpha(RemoteStrokePreviewAlpha);
    }

    public static SKPoint[] GetRemoteCursorArrowPoints(Vector2 position)
    {
        var x = position.X;
        var y = position.Y;

        return
        [
            new SKPoint(x, y),
            new SKPoint(x + 13f, y + 8f),
            new SKPoint(x + 8f, y + 10f),
            new SKPoint(x + 11f, y + 18f),
            new SKPoint(x + 7f, y + 19f),
            new SKPoint(x + 4f, y + 11f),
            new SKPoint(x - 1f, y + 16f),
        ];
    }

    public static SKRect GetRemoteCursorLabelRect(Vector2 position, string displayName, SKFont font)
    {
        var labelWidth = font.MeasureText(displayName);
        return new SKRect(position.X + 12f, position.Y - 24f, position.X + 22f + labelWidth, position.Y - 6f);
    }

    public static void DrawRemoteStrokePreview(SKCanvas canvas, RemoteStrokePreviewState preview)
    {
        if (preview.Points.Count == 0)
            return;

        using var paint = new SKPaint
        {
            Color = GetRemoteStrokePreviewColor(preview.AssignedColor),
            StrokeWidth = 2f,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            PathEffect = SKPathEffect.CreateDash(new[] { 8f, 6f }, 0f)
        };

        var smoothed = StrokeSmoothingHelper.SmoothStroke(preview.Points);
        if (smoothed.Count < 2)
        {
            canvas.DrawCircle(preview.Points[0].X, preview.Points[0].Y, 4f, paint);
            return;
        }

        using var path = new SKPath();
        path.MoveTo(smoothed[0].X, smoothed[0].Y);
        for (var i = 1; i < smoothed.Count; i++)
        {
            path.LineTo(smoothed[i].X, smoothed[i].Y);
        }

        canvas.DrawPath(path, paint);
    }

    public static void DrawRemoteCursor(SKCanvas canvas, RemoteCursorState cursor)
    {
        using var markerPaint = new SKPaint
        {
            Color = cursor.AssignedColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f
        };

        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        using var font = new SKFont { Size = 12f };

        var arrowPoints = GetRemoteCursorArrowPoints(cursor.Position);
        using var arrowPath = new SKPath();
        arrowPath.MoveTo(arrowPoints[0]);
        for (var i = 1; i < arrowPoints.Length; i++)
        {
            arrowPath.LineTo(arrowPoints[i]);
        }

        arrowPath.Close();
        canvas.DrawPath(arrowPath, markerPaint);

        var labelRect = GetRemoteCursorLabelRect(cursor.Position, cursor.DisplayName, font);

        using var labelPaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(180),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        canvas.DrawRoundRect(labelRect, 4f, 4f, labelPaint);
        canvas.DrawText(cursor.DisplayName, labelRect.Left + 5f, labelRect.Bottom - 4f, SKTextAlign.Left, font, textPaint);
    }
}
