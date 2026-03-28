using System.Numerics;
using SkiaSharp;

namespace BFGA.Canvas.Rendering;

public sealed record RemoteCursorState(Guid ClientId, string DisplayName, SKColor AssignedColor, Vector2 Position);

public sealed record RemoteStrokePreviewState(
    Guid ClientId,
    Guid StrokeId,
    string DisplayName,
    SKColor AssignedColor,
    IReadOnlyList<Vector2> Points);
