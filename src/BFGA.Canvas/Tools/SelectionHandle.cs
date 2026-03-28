using System.Numerics;

namespace BFGA.Canvas.Tools;

public sealed record SelectionHandle(SelectionHandleKind Kind, Vector2 Position, float HitRadius = 8f);
