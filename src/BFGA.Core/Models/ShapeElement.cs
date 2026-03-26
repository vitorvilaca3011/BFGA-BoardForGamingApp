using System.Numerics;
using MessagePack;
using SkiaSharp;

namespace BFGA.Core.Models;

[MessagePackObject]
public class ShapeElement : BoardElement
{
    [Key(10)]
    public ShapeType Type { get; set; }

    [Key(11)]
    public SKColor StrokeColor { get; set; }

    [Key(12)]
    public SKColor FillColor { get; set; }

    [Key(13)]
    public float StrokeWidth { get; set; }
}
