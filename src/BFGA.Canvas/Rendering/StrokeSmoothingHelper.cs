using System.Numerics;

namespace BFGA.Canvas.Rendering;

/// <summary>
/// Provides Catmull-Rom spline smoothing for stroke point lists.
/// Produces a smooth curve that passes through all input control points.
/// </summary>
public static class StrokeSmoothingHelper
{
    private const int DefaultSegmentsPerCurve = 8;

    /// <summary>
    /// Smooths a list of stroke points using Catmull-Rom interpolation.
    /// The returned path preserves the first and last input points exactly.
    /// </summary>
    /// <param name="points">The raw control points of the stroke.</param>
    /// <param name="segmentsPerCurve">Number of interpolated segments between each pair of control points.</param>
    /// <returns>A smoothed list of points forming a curve.</returns>
    public static IReadOnlyList<Vector2> SmoothStroke(IReadOnlyList<Vector2> points, int segmentsPerCurve = DefaultSegmentsPerCurve)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count <= 2)
            return points.ToList();

        if (segmentsPerCurve < 1)
            throw new ArgumentOutOfRangeException(nameof(segmentsPerCurve), "Must be at least 1.");

        var result = new List<Vector2>();

        for (int i = 0; i < points.Count - 1; i++)
        {
            // Catmull-Rom needs 4 control points; clamp at boundaries
            var p0 = points[Math.Max(i - 1, 0)];
            var p1 = points[i];
            var p2 = points[Math.Min(i + 1, points.Count - 1)];
            var p3 = points[Math.Min(i + 2, points.Count - 1)];

            for (int j = 0; j < segmentsPerCurve; j++)
            {
                float t = j / (float)segmentsPerCurve;
                var interpolated = CatmullRom(p0, p1, p2, p3, t);
                result.Add(interpolated);
            }
        }

        // Add the final point exactly
        result.Add(points[^1]);

        return result;
    }

    /// <summary>
    /// Evaluates a Catmull-Rom spline at parameter t.
    /// </summary>
    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        float x = 0.5f * (
            (2f * p1.X) +
            (-p0.X + p2.X) * t +
            (2f * p0.X - 5f * p1.X + 4f * p2.X - p3.X) * t2 +
            (-p0.X + 3f * p1.X - 3f * p2.X + p3.X) * t3
        );

        float y = 0.5f * (
            (2f * p1.Y) +
            (-p0.Y + p2.Y) * t +
            (2f * p0.Y - 5f * p1.Y + 4f * p2.Y - p3.Y) * t2 +
            (-p0.Y + 3f * p1.Y - 3f * p2.Y + p3.Y) * t3
        );

        return new Vector2(x, y);
    }
}
