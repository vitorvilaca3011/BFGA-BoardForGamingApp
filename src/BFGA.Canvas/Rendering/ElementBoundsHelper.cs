using System.Numerics;
using BFGA.Core.Models;
using SkiaSharp;

namespace BFGA.Canvas.Rendering;

/// <summary>
/// Provides bounding-box and point-containment calculations for board elements.
/// </summary>
public static class ElementBoundsHelper
{
    /// <summary>
    /// Gets the axis-aligned bounding rectangle of an element in board coordinates.
    /// </summary>
    public static SKRect GetBounds(BoardElement element)
    {
        return element switch
        {
            StrokeElement stroke => GetStrokeBounds(stroke),
            TextElement text => GetTextBounds(text),
            _ => GetTransformedBounds(element)
        };
    }

    /// <summary>
    /// Determines whether a board-space point is contained within the element's bounds.
    /// Uses shape-specific tests for ellipse and line/arrow to reduce false positives.
    /// </summary>
    public static bool ContainsPoint(BoardElement element, Vector2 point)
    {
        return element switch
        {
            StrokeElement stroke => HitTestHelper.HitTestStroke(stroke, point),
            ShapeElement shape => ContainsPointShape(shape, point),
            TextElement text => ContainsPointRect(GetBounds(text), point),
            _ => ContainsPointBBox(element, point)
        };
    }

    private static bool ContainsPointShape(ShapeElement shape, Vector2 point)
    {
        return shape.Type switch
        {
            ShapeType.Ellipse => HitTestEllipse(shape, point),
            ShapeType.Line => HitTestLineOrArrow(shape, point),
            ShapeType.Arrow => HitTestLineOrArrow(shape, point),
            _ => ContainsPointBBox(shape, point) // Rectangle uses bounding box
        };
    }

    /// <summary>
    /// Tests whether a point is inside an ellipse using the normalized ellipse equation.
    /// </summary>
    private static bool HitTestEllipse(ShapeElement ellipse, Vector2 point)
    {
        if (SupportsRotation(ellipse) && MathF.Abs(ellipse.Rotation) > float.Epsilon)
            point = RotatePoint(point, GetCenter(ellipse), -ellipse.Rotation);

        float left = MathF.Min(ellipse.Position.X, ellipse.Position.X + ellipse.Size.X);
        float top = MathF.Min(ellipse.Position.Y, ellipse.Position.Y + ellipse.Size.Y);
        float right = MathF.Max(ellipse.Position.X, ellipse.Position.X + ellipse.Size.X);
        float bottom = MathF.Max(ellipse.Position.Y, ellipse.Position.Y + ellipse.Size.Y);

        float cx = (left + right) / 2f;
        float cy = (top + bottom) / 2f;
        float rx = MathF.Abs(ellipse.Size.X) / 2f;
        float ry = MathF.Abs(ellipse.Size.Y) / 2f;

        if (rx < float.Epsilon || ry < float.Epsilon)
            return false;

        float dx = point.X - cx;
        float dy = point.Y - cy;
        float normalized = (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry);
        return normalized <= 1f;
    }

    /// <summary>
    /// Tests whether a point is near a line or arrow segment (within stroke tolerance).
    /// For arrows, also checks the arrowhead segments.
    /// </summary>
    private static bool HitTestLineOrArrow(ShapeElement shape, Vector2 point)
    {
        float x1 = shape.Position.X;
        float y1 = shape.Position.Y;
        float x2 = shape.Position.X + shape.Size.X;
        float y2 = shape.Position.Y + shape.Size.Y;
        float hitRadius = shape.StrokeWidth / 2f + 2f; // small tolerance

        var a = new Vector2(x1, y1);
        var b = new Vector2(x2, y2);

        if (PointToSegmentDistance(point, a, b) <= hitRadius)
            return true;

        // For arrows, also check the arrowhead segments
        if (shape.Type == ShapeType.Arrow)
        {
            float angle = MathF.Atan2(y2 - y1, x2 - x1);
            float headLength = MathF.Max(shape.StrokeWidth * 4f, 10f);
            float headAngle = MathF.PI / 6f;

            var head1 = new Vector2(
                x2 - headLength * MathF.Cos(angle - headAngle),
                y2 - headLength * MathF.Sin(angle - headAngle));
            var head2 = new Vector2(
                x2 - headLength * MathF.Cos(angle + headAngle),
                y2 - headLength * MathF.Sin(angle + headAngle));

            if (PointToSegmentDistance(point, b, head1) <= hitRadius ||
                PointToSegmentDistance(point, b, head2) <= hitRadius)
                return true;
        }

        return false;
    }

    private static bool ContainsPointBBox(BoardElement element, Vector2 point)
    {
        var bounds = CreateNormalizedRect(element.Position, element.Size);
        if (SupportsRotation(element) && MathF.Abs(element.Rotation) > float.Epsilon)
            point = InverseRotatePoint(point, GetCenter(element), element.Rotation);

        return ContainsPointRect(bounds, point);
    }

    private static bool ContainsPointRect(SKRect bounds, Vector2 point)
        => point.X >= bounds.Left && point.X <= bounds.Right
            && point.Y >= bounds.Top && point.Y <= bounds.Bottom;

    private static SKRect GetTransformedBounds(BoardElement element)
    {
        var rect = CreateNormalizedRect(element.Position, element.Size);
        if (!SupportsRotation(element) || MathF.Abs(element.Rotation) <= float.Epsilon)
            return rect;

        var center = GetCenter(element);
        var corners = new[]
        {
            new Vector2(rect.Left, rect.Top),
            new Vector2(rect.Right, rect.Top),
            new Vector2(rect.Right, rect.Bottom),
            new Vector2(rect.Left, rect.Bottom)
        };

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var corner in corners)
        {
            var rotated = RotatePoint(corner, center, element.Rotation);
            if (rotated.X < minX) minX = rotated.X;
            if (rotated.Y < minY) minY = rotated.Y;
            if (rotated.X > maxX) maxX = rotated.X;
            if (rotated.Y > maxY) maxY = rotated.Y;
        }

        return new SKRect(minX, minY, maxX, maxY);
    }

    private static SKRect GetTextBounds(TextElement text)
    {
        if (string.IsNullOrEmpty(text.Text))
            return new SKRect(text.Position.X, text.Position.Y, text.Position.X, text.Position.Y);

        using var font = new SKFont
        {
            Size = text.FontSize
        };

        if (!string.IsNullOrEmpty(text.FontFamily))
        {
            using var typeface = SKTypeface.FromFamilyName(text.FontFamily);
            if (typeface is not null)
                font.Typeface = typeface;
        }

        var metrics = font.Metrics;
        var lineHeight = metrics.Descent - metrics.Ascent + metrics.Leading;
        if (lineHeight <= float.Epsilon)
            lineHeight = text.FontSize * 1.2f;

        var lines = text.Text.Split('\n');
        var maxWidth = 0f;
        foreach (var line in lines)
        {
            var width = font.MeasureText(line);
            if (width > maxWidth)
                maxWidth = width;
        }

        var height = Math.Max(lineHeight, lineHeight * lines.Length);
        return new SKRect(text.Position.X, text.Position.Y, text.Position.X + maxWidth, text.Position.Y + height);
    }

    public static SKRect CreateNormalizedRect(Vector2 position, Vector2 size)
    {
        float left = MathF.Min(position.X, position.X + size.X);
        float top = MathF.Min(position.Y, position.Y + size.Y);
        float right = MathF.Max(position.X, position.X + size.X);
        float bottom = MathF.Max(position.Y, position.Y + size.Y);
        return new SKRect(left, top, right, bottom);
    }

    public static Vector2 GetCenter(BoardElement element)
        => new(element.Position.X + element.Size.X / 2f, element.Position.Y + element.Size.Y / 2f);

    public static bool SupportsRotation(BoardElement element)
        => element is ImageElement
            || element is ShapeElement { Type: ShapeType.Rectangle or ShapeType.Ellipse };

    public static bool SupportsResize(BoardElement element)
        => element is ImageElement
            || element is ShapeElement { Type: ShapeType.Rectangle or ShapeType.Ellipse or ShapeType.Line or ShapeType.Arrow };

    public static Vector2 RotatePoint(Vector2 point, Vector2 center, float rotationDegrees)
    {
        if (MathF.Abs(rotationDegrees) <= float.Epsilon)
            return point;

        float radians = rotationDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        var translated = point - center;
        return new Vector2(
            center.X + translated.X * cos - translated.Y * sin,
            center.Y + translated.X * sin + translated.Y * cos);
    }

    public static Vector2 InverseRotatePoint(Vector2 point, Vector2 center, float rotationDegrees)
        => RotatePoint(point, center, -rotationDegrees);

    private static SKRect GetStrokeBounds(StrokeElement stroke)
    {
        if (stroke.Points.Count == 0)
            return new SKRect(stroke.Position.X, stroke.Position.Y, stroke.Position.X, stroke.Position.Y);

        if (stroke.Points.Count == 1)
        {
            var dotCenter = stroke.Position + stroke.Points[0];
            float dotPad = Math.Max(stroke.Thickness / 2f, 1f);
            return new SKRect(dotCenter.X - dotPad, dotCenter.Y - dotPad, dotCenter.X + dotPad, dotCenter.Y + dotPad);
        }

        var smoothed = StrokeSmoothingHelper.SmoothStroke(stroke.Points);

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var pt in smoothed)
        {
            float x = stroke.Position.X + pt.X;
            float y = stroke.Position.Y + pt.Y;
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
        }

        // Pad by thickness
        float pad = stroke.Thickness / 2f;
        return new SKRect(minX - pad, minY - pad, maxX + pad, maxY + pad);
    }

    /// <summary>
    /// Computes the shortest distance from a point to a line segment.
    /// </summary>
    private static float PointToSegmentDistance(Vector2 point, Vector2 segA, Vector2 segB)
    {
        var ab = segB - segA;
        var ap = point - segA;

        float abLenSq = ab.LengthSquared();
        if (abLenSq < float.Epsilon)
            return (point - segA).Length();

        float t = Vector2.Dot(ap, ab) / abLenSq;
        t = Math.Clamp(t, 0f, 1f);

        var closest = segA + ab * t;
        return (point - closest).Length();
    }
}
