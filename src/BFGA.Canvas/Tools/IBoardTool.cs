namespace BFGA.Canvas.Tools;

public interface IBoardTool
{
    BoardToolType ToolType { get; }
    ToolResult PointerDown(ToolContext context, ToolInput input);
    ToolResult PointerMove(ToolContext context, ToolInput input);
    ToolResult PointerUp(ToolContext context, ToolInput input);
}
