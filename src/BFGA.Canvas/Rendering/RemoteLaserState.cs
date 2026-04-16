using SkiaSharp;

namespace BFGA.Canvas.Rendering;

public sealed class RemoteLaserState
{
    public LaserTrailBuffer Trail { get; }

    public bool IsActive { get; set; }

    public long LastUpdateMs { get; set; }

    public SKColor Color { get; set; }

    public RemoteLaserState(SKColor color)
    {
        Trail = new LaserTrailBuffer(128);
        Color = color;
        IsActive = false;
        LastUpdateMs = 0;
    }

    public void Reset()
    {
        Trail.Clear();
        IsActive = false;
    }
}
