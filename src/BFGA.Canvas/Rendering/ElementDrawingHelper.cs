using System.Numerics;
using BFGA.Core.Models;
using SkiaSharp;

namespace BFGA.Canvas.Rendering;

/// <summary>
/// Provides SkiaSharp drawing routines for each board element type.
/// All methods operate on an SKCanvas and draw in board coordinates.
/// </summary>
public static class ElementDrawingHelper
{
    /// <summary>
    /// Draws a board element onto the given SKCanvas.
    /// </summary>
    public static void DrawElement(SKCanvas canvas, BoardElement element, ImageDecodeCache? imageCache = null)
    {
        switch (element)
        {
            case StrokeElement stroke:
                DrawStroke(canvas, stroke);
                break;
            case ShapeElement shape:
                DrawShape(canvas, shape);
                break;
            case ImageElement image:
                DrawImage(canvas, image, imageCache);
                break;
            case TextElement text:
                DrawText(canvas, text);
                break;
        }
    }

    /// <summary>
    /// Draws a stroke element using smoothed Catmull-Rom interpolation.
    /// </summary>
    public static void DrawStroke(SKCanvas canvas, StrokeElement stroke)
    {
        if (stroke.Points.Count == 0)
            return;

        using var paint = new SKPaint
        {
            Color = stroke.Color,
            StrokeWidth = stroke.Thickness,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        var smoothed = StrokeSmoothingHelper.SmoothStroke(stroke.Points);

        if (smoothed.Count < 2)
        {
            canvas.DrawCircle(stroke.Position.X + stroke.Points[0].X, stroke.Position.Y + stroke.Points[0].Y,
                MathF.Max(stroke.Thickness / 2f, 1f), paint);
            return;
        }

        using var path = new SKPath();
        path.MoveTo(stroke.Position.X + smoothed[0].X, stroke.Position.Y + smoothed[0].Y);

        for (int i = 1; i < smoothed.Count; i++)
        {
            path.LineTo(stroke.Position.X + smoothed[i].X, stroke.Position.Y + smoothed[i].Y);
        }

        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Draws a shape element (Rectangle, Ellipse, Line, Arrow).
    /// </summary>
    public static void DrawShape(SKCanvas canvas, ShapeElement shape)
    {
        using var strokePaint = new SKPaint
        {
            Color = shape.StrokeColor,
            StrokeWidth = shape.StrokeWidth,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round
        };

        using var fillPaint = new SKPaint
        {
            Color = shape.FillColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var rect = ElementBoundsHelper.CreateNormalizedRect(shape.Position, shape.Size);
        var center = ElementBoundsHelper.GetCenter(shape);
        var supportsRotation = ElementBoundsHelper.SupportsRotation(shape) && MathF.Abs(shape.Rotation) > float.Epsilon;

        if (supportsRotation)
            canvas.Save();

        if (supportsRotation)
        {
            canvas.Translate(center.X, center.Y);
            canvas.RotateDegrees(shape.Rotation);
            canvas.Translate(-center.X, -center.Y);
        }

        switch (shape.Type)
        {
            case ShapeType.Rectangle:
                if (shape.FillColor.Alpha > 0)
                    canvas.DrawRect(rect, fillPaint);
                canvas.DrawRect(rect, strokePaint);
                break;

            case ShapeType.Ellipse:
                if (shape.FillColor.Alpha > 0)
                    canvas.DrawOval(rect, fillPaint);
                canvas.DrawOval(rect, strokePaint);
                break;

            case ShapeType.Line:
                canvas.DrawLine(
                    shape.Position.X, shape.Position.Y,
                    shape.Position.X + shape.Size.X, shape.Position.Y + shape.Size.Y,
                    strokePaint);
                break;

            case ShapeType.Arrow:
                DrawArrow(canvas, shape, strokePaint);
                break;
        }

        if (supportsRotation)
            canvas.Restore();
    }

    /// <summary>
    /// Draws an image element. Falls back to a placeholder if decoding fails.
    /// Uses a bitmap cache to avoid re-decoding on every paint frame.
    /// </summary>
    public static void DrawImage(SKCanvas canvas, ImageElement image, ImageDecodeCache? imageCache = null)
    {
        if (image.ImageData.Length == 0)
            return;

        var supportsRotation = ElementBoundsHelper.SupportsRotation(image) && MathF.Abs(image.Rotation) > float.Epsilon;
        if (supportsRotation)
        {
            var center = ElementBoundsHelper.GetCenter(image);
            canvas.Save();
            canvas.Translate(center.X, center.Y);
            canvas.RotateDegrees(image.Rotation);
            canvas.Translate(-center.X, -center.Y);
        }

        if (imageCache is not null)
        {
            var cachedImage = imageCache.GetOrAdd(image.Id, image.ImageData);
            if (cachedImage is null)
            {
                DrawPlaceholderRect(canvas, image);
                if (supportsRotation)
                    canvas.Restore();
                return;
            }

            DrawSkImage(canvas, image, cachedImage);
            if (supportsRotation)
                canvas.Restore();
            return;
        }

        // No cache: decode inline. Use SKImage.FromEncodedData (not SKBitmap) to avoid
        // the sk_image_new_from_bitmap crash on the Windows WinUI compositor.
        using var skImage = DecodeToSkImage(image.ImageData);
        if (skImage is null)
        {
            DrawPlaceholderRect(canvas, image);
            if (supportsRotation)
                canvas.Restore();
            return;
        }

        DrawSkImage(canvas, image, skImage);
        if (supportsRotation)
            canvas.Restore();
    }

    private static void DrawSkImage(SKCanvas canvas, ImageElement image, SKImage skImage)
    {
        var destRect = ElementBoundsHelper.CreateNormalizedRect(image.Position, image.Size);
        canvas.DrawImage(skImage, destRect);
    }

    /// <summary>
    /// Decodes raw image bytes into an SKImage using the safest available path.
    /// Prefers SKImage.FromEncodedData to avoid pixel-format mismatches with GPU backends.
    /// </summary>
    private static SKImage? DecodeToSkImage(byte[] imageData)
    {
        var image = SKImage.FromEncodedData(imageData);
        if (image is not null)
            return image;

        // Fallback for formats not handled by FromEncodedData.
        using var bitmap = SKBitmap.Decode(imageData);
        if (bitmap is null)
            return null;

        return SKImage.FromBitmap(bitmap);
    }

    /// <summary>
    /// Draws a text element.
    /// </summary>
    public static void DrawText(SKCanvas canvas, TextElement text)
    {
        if (string.IsNullOrEmpty(text.Text))
            return;

        using var paint = new SKPaint
        {
            Color = text.Color,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        using var font = new SKFont
        {
            Size = text.FontSize
        };

        // Use a default typeface if the specified family is not available
        if (!string.IsNullOrEmpty(text.FontFamily))
        {
            using var typeface = SKTypeface.FromFamilyName(text.FontFamily);
            if (typeface is not null)
                font.Typeface = typeface;
        }

        // Draw text line by line
        var lines = text.Text.Split('\n');
        float lineHeight = text.FontSize * 1.2f;
        float y = text.Position.Y + text.FontSize; // baseline for first line

        foreach (var line in lines)
        {
            canvas.DrawText(line, text.Position.X, y, SKTextAlign.Left, font, paint);
            y += lineHeight;
        }
    }

    private static void DrawArrow(SKCanvas canvas, ShapeElement shape, SKPaint paint)
    {
        float x1 = shape.Position.X;
        float y1 = shape.Position.Y;
        float x2 = shape.Position.X + shape.Size.X;
        float y2 = shape.Position.Y + shape.Size.Y;

        // Draw the shaft
        canvas.DrawLine(x1, y1, x2, y2, paint);

        // Draw arrowhead
        float angle = MathF.Atan2(y2 - y1, x2 - x1);
        float headLength = MathF.Max(shape.StrokeWidth * 4f, 10f);
        float headAngle = MathF.PI / 6f; // 30 degrees

        var head1 = new SKPoint(
            x2 - headLength * MathF.Cos(angle - headAngle),
            y2 - headLength * MathF.Sin(angle - headAngle));

        var head2 = new SKPoint(
            x2 - headLength * MathF.Cos(angle + headAngle),
            y2 - headLength * MathF.Sin(angle + headAngle));

        canvas.DrawLine(x2, y2, head1.X, head1.Y, paint);
        canvas.DrawLine(x2, y2, head2.X, head2.Y, paint);
    }

    private static void DrawPlaceholderRect(SKCanvas canvas, ImageElement image)
    {
        using var paint = new SKPaint
        {
            Color = SKColors.Gray,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            PathEffect = SKPathEffect.CreateDash(new float[] { 8, 4 }, 0)
        };

        var rect = ElementBoundsHelper.CreateNormalizedRect(image.Position, image.Size);

        canvas.DrawRect(rect, paint);

        // Draw an X through the placeholder
        canvas.DrawLine(rect.Left, rect.Top, rect.Right, rect.Bottom, paint);
        canvas.DrawLine(rect.Right, rect.Top, rect.Left, rect.Bottom, paint);
    }
}
