using System.Numerics;
using BFGA.Core;
using BFGA.Core.Models;
using SkiaSharp;

namespace BFGA.Canvas.Rendering;

/// <summary>
/// Computes the hosted surface used by the MVP board viewport.
/// The surface is a fixed large workspace so board-to-canvas mapping
/// remains stable as content changes.
/// </summary>
public static class BoardSurfaceHelper
{
    /// <summary>
    /// Stable square workspace size used by the hosted canvas.
    /// </summary>
    public const float StableWorkspaceSize = 200_000f;

    /// <summary>
    /// Stable origin offset used to center board coordinates in workspace space.
    /// </summary>
    public static readonly Vector2 StableOriginOffset = new(StableWorkspaceSize / 2f, StableWorkspaceSize / 2f);

    public static BoardSurfaceMetrics GetSurfaceMetrics(BoardState? board)
    {
        return new BoardSurfaceMetrics(new Vector2(StableWorkspaceSize, StableWorkspaceSize), StableOriginOffset, GetBoardBounds(board));
    }

    public static SKRect GetBoardBounds(BoardState? board)
    {
        if (board?.Elements is not { Count: > 0 } elements)
            return new SKRect(0f, 0f, 0f, 0f);

        var firstBounds = ElementBoundsHelper.GetBounds(elements[0]);
        float minX = firstBounds.Left;
        float minY = firstBounds.Top;
        float maxX = firstBounds.Right;
        float maxY = firstBounds.Bottom;

        for (int i = 1; i < elements.Count; i++)
        {
            var elementBounds = ElementBoundsHelper.GetBounds(elements[i]);
            if (elementBounds.Left < minX) minX = elementBounds.Left;
            if (elementBounds.Top < minY) minY = elementBounds.Top;
            if (elementBounds.Right > maxX) maxX = elementBounds.Right;
            if (elementBounds.Bottom > maxY) maxY = elementBounds.Bottom;
        }

        return new SKRect(minX, minY, maxX, maxY);
    }

    public static Vector2 BoardToCanvas(Vector2 boardPoint, Vector2 originOffset) => boardPoint + originOffset;

    public static Vector2 CanvasToBoard(Vector2 canvasPoint, Vector2 originOffset) => canvasPoint - originOffset;
}

public readonly record struct BoardSurfaceMetrics(Vector2 SurfaceSize, Vector2 OriginOffset, SKRect BoardBounds);
