using System.Numerics;
using BFGA.Core;
using BFGA.Core.Models;

namespace BFGA.Canvas.Rendering;

/// <summary>
/// Provides hit-testing logic for board elements.
/// </summary>
public static class HitTestHelper
{
    /// <summary>
    /// Tests whether a point hits a stroke element by checking distance to each segment.
    /// </summary>
    /// <param name="stroke">The stroke to test against.</param>
    /// <param name="point">The point in board coordinates.</param>
    /// <returns>True if the point is within the stroke's thickness of any segment.</returns>
    public static bool HitTestStroke(StrokeElement stroke, Vector2 point)
    {
        if (stroke.Points.Count == 0)
            return false;

        if (stroke.Points.Count == 1)
        {
            var dotCenter = stroke.Position + stroke.Points[0];
            return Vector2.Distance(point, dotCenter) <= Math.Max(stroke.Thickness / 2f, 1f) + 2f;
        }

        float hitRadius = stroke.Thickness / 2f + 2f; // small tolerance
        var smoothed = StrokeSmoothingHelper.SmoothStroke(stroke.Points);

        for (int i = 0; i < smoothed.Count - 1; i++)
        {
            var a = stroke.Position + smoothed[i];
            var b = stroke.Position + smoothed[i + 1];

            float dist = PointToSegmentDistance(point, a, b);
            if (dist <= hitRadius)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Finds the topmost element that contains the given point, mirroring draw order.
    /// Elements are sorted by ZIndex ascending (same as rendering), then checked
    /// in reverse so the last-drawn element wins on ZIndex ties.
    /// </summary>
    /// <param name="board">The board state to search.</param>
    /// <param name="point">The point in board coordinates.</param>
    /// <returns>The topmost hit element, or null if no element contains the point.</returns>
    public static BoardElement? GetTopmostHit(BoardState board, Vector2 point)
    {
        // Sort by ZIndex ascending to match draw order (OrderBy is stable).
        // Skip allocation when already sorted.
        var elements = board.Elements;
        var sorted = IsSortedByZIndex(elements)
            ? elements
            : elements.OrderBy(e => e.ZIndex).ToList();

        // Iterate in reverse: last-drawn (topmost) is checked first
        for (int i = sorted.Count - 1; i >= 0; i--)
        {
            if (ElementBoundsHelper.ContainsPoint(sorted[i], point))
                return sorted[i];
        }

        return null;
    }

    private static bool IsSortedByZIndex(IReadOnlyList<BoardElement> elements)
    {
        for (int i = 1; i < elements.Count; i++)
        {
            if (elements[i].ZIndex < elements[i - 1].ZIndex)
                return false;
        }
        return true;
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
