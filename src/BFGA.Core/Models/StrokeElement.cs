using System.Numerics;
using MessagePack;
using SkiaSharp;

namespace BFGA.Core.Models;

[MessagePackObject]
public class StrokeElement : BoardElement
{
    [Key(7)]
    public List<Vector2> Points { get; set; } = new();

    [Key(8)]
    public SKColor Color { get; set; }

    [Key(9)]
    public float Thickness { get; set; }
}
