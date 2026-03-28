using BFGA.Network.Protocol;

namespace BFGA.Canvas.Tools;

public sealed record ToolResult(bool Handled, bool BoardChanged, IReadOnlyList<BoardOperation> Operations)
{
    public static ToolResult None => new(false, false, Array.Empty<BoardOperation>());
    public static ToolResult HandledOnly => new(true, false, Array.Empty<BoardOperation>());
    public static ToolResult Changed => new(true, true, Array.Empty<BoardOperation>());

    public ToolResult(bool handled, bool boardChanged)
        : this(handled, boardChanged, Array.Empty<BoardOperation>())
    {
    }

    public bool HasOperations => Operations.Count > 0;
}
