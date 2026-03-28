using System.Numerics;

namespace BFGA.Canvas.Rendering;

/// <summary>
/// Provides coordinate transformation between screen space and board space.
/// Screen space is the pixel coordinate system of the control.
/// Board space is the logical coordinate system of the board model.
/// </summary>
public static class CoordinateTransformHelper
{
    /// <summary>
    /// Converts a screen-space point to board-space coordinates.
    /// </summary>
    /// <param name="screenPoint">The point in screen (pixel) coordinates.</param>
    /// <param name="panOffset">The current pan offset in screen space.</param>
    /// <param name="zoom">The current zoom level (1.0 = 100%).</param>
    /// <returns>The point in board coordinates.</returns>
    public static Vector2 ScreenToBoard(Vector2 screenPoint, Vector2 panOffset, float zoom, Vector2 originOffset)
    {
        if (zoom <= 0f)
            throw new ArgumentOutOfRangeException(nameof(zoom), "Zoom must be positive.");

        return (screenPoint - panOffset) / zoom - originOffset;
    }

    /// <summary>
    /// Converts a board-space point to screen-space coordinates.
    /// </summary>
    /// <param name="boardPoint">The point in board coordinates.</param>
    /// <param name="panOffset">The current pan offset in screen space.</param>
    /// <param name="zoom">The current zoom level (1.0 = 100%).</param>
    /// <returns>The point in screen (pixel) coordinates.</returns>
    public static Vector2 BoardToScreen(Vector2 boardPoint, Vector2 panOffset, float zoom, Vector2 originOffset)
    {
        if (zoom <= 0f)
            throw new ArgumentOutOfRangeException(nameof(zoom), "Zoom must be positive.");

        return (boardPoint + originOffset) * zoom + panOffset;
    }
}
