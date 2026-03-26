using System.Numerics;
using MessagePack;
using SkiaSharp;

namespace BFGA.Core.Models;

[MessagePackObject]
public class StrokeElement : BoardElement
{
    [Key(10)]
    public List<Vector2> Points { get; set; } = new();

    [Key(11)]
    public SKColor Color { get; set; }

    [Key(12)]
    public float Thickness { get; set; }
}
