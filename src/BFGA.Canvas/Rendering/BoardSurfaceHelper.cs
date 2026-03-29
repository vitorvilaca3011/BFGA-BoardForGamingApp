using BFGA.Core;
using BFGA.Core.Models;
using SkiaSharp;

namespace BFGA.Canvas.Rendering;

/// <summary>
/// Provides board content bounds computation.
/// </summary>
public static class BoardSurfaceHelper
{
    /// <summary>
    /// Computes the axis-aligned bounding box of all elements in board coordinates.
    /// Returns SKRect.Empty if the board is null or has no elements.
    /// </summary>
    public static SKRect GetBoardBounds(BoardState? board)
    {
        if (board is null) return SKRect.Empty;

        var elements = board.Elements;
        if (elements.Count == 0) return SKRect.Empty;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var element in elements)
        {
            var bounds = ElementBoundsHelper.GetBounds(element);
            if (bounds.Left < minX) minX = bounds.Left;
            if (bounds.Top < minY) minY = bounds.Top;
            if (bounds.Right > maxX) maxX = bounds.Right;
            if (bounds.Bottom > maxY) maxY = bounds.Bottom;
        }

        if (minX > maxX || minY > maxY) return SKRect.Empty;
        return new SKRect(minX, minY, maxX, maxY);
    }
}
