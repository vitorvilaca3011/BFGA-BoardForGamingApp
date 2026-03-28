using BFGA.Core;
using BFGA.Core.Models;

namespace BFGA.Canvas.Tools;

public sealed record ToolContext(BoardState Board, SelectionState Selection)
{
    public ShapeType ShapeType { get; init; } = ShapeType.Rectangle;
}
