using System.Numerics;

namespace BFGA.Canvas.Rendering;

/// <summary>
/// Converts between screen space and board space.
/// Screen space: pixels relative to the viewport control (origin at top-left).
/// Board space: logical coordinates where elements live (origin at 0,0, infinite).
/// </summary>
public static class CoordinateTransformHelper
{
    /// <summary>
    /// Converts a screen-space point to board-space.
    /// Formula: (screenPoint - pan) / zoom
    /// </summary>
    public static Vector2 ScreenToBoard(Vector2 screenPoint, Vector2 pan, float zoom)
    {
        return (screenPoint - pan) / zoom;
    }

    /// <summary>
    /// Converts a board-space point to screen-space.
    /// Formula: boardPoint * zoom + pan
    /// </summary>
    public static Vector2 BoardToScreen(Vector2 boardPoint, Vector2 pan, float zoom)
    {
        return boardPoint * zoom + pan;
    }
}
