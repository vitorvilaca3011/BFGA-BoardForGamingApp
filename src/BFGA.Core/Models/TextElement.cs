using System.Numerics;
using MessagePack;
using SkiaSharp;

namespace BFGA.Core.Models;

[MessagePackObject]
public class TextElement : BoardElement
{
    [Key(7)]
    public string Text { get; set; } = string.Empty;

    [Key(8)]
    public float FontSize { get; set; }

    [Key(9)]
    public SKColor Color { get; set; }

    [Key(10)]
    public string FontFamily { get; set; } = string.Empty;
}
