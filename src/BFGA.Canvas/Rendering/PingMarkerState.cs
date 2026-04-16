using System.Numerics;
using SkiaSharp;

namespace BFGA.Canvas.Rendering;

public sealed record PingMarkerState(Vector2 Position, long StartedAtMs, SKColor Color);
