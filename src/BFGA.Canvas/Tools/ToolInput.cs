using System.Numerics;

namespace BFGA.Canvas.Tools;

public sealed record ToolInput(Vector2 Position, bool IsShiftPressed = false, bool IsCtrlPressed = false);
