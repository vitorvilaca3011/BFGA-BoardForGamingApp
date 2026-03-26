using System.Numerics;
using MessagePack;

namespace BFGA.Core.Models;

[MessagePackObject]
public class ImageElement : BoardElement
{
    [Key(10)]
    public byte[] ImageData { get; set; } = Array.Empty<byte>();

    [Key(11)]
    public string OriginalFileName { get; set; } = string.Empty;
}
