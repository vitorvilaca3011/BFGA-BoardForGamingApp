using System.Numerics;
using SkiaSharp;

namespace BFGA.Canvas.Rendering;

public sealed class LocalLaserState
{
    public LaserTrailBuffer Trail { get; }

    public Vector2 HeadPosition { get; set; }

    public bool IsActive { get; set; }

    public long LastUpdateMs { get; set; }

    public SKColor Color { get; set; }

    public LocalLaserState(SKColor color)
    {
        Trail = new LaserTrailBuffer(128);
        HeadPosition = Vector2.Zero;
        Color = color;
        IsActive = false;
        LastUpdateMs = 0;
    }

    public void Reset()
    {
        Trail.Clear();
        HeadPosition = Vector2.Zero;
        IsActive = false;
    }
}
