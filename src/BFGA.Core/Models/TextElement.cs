using System.Numerics;
using MessagePack;
using SkiaSharp;

namespace BFGA.Core.Models;

[MessagePackObject]
public class TextElement : BoardElement
{
    [Key(10)]
    public string Text { get; set; } = string.Empty;

    [Key(11)]
    public float FontSize { get; set; }

    [Key(12)]
    public SKColor Color { get; set; }

    [Key(13)]
    public string FontFamily { get; set; } = string.Empty;
}
