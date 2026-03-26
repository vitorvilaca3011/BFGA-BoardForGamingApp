using System.Numerics;
using MessagePack;
using SkiaSharp;

namespace BFGA.Core.Models;

[MessagePackObject]
public class ShapeElement : BoardElement
{
    [Key(7)]
    public ShapeType Type { get; set; }

    [Key(8)]
    public SKColor StrokeColor { get; set; }

    [Key(9)]
    public SKColor FillColor { get; set; }

    [Key(10)]
    public float StrokeWidth { get; set; }
}
