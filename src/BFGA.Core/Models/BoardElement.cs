using System.Numerics;
using MessagePack;

namespace BFGA.Core.Models;

[MessagePackObject]
[MessagePack.Union(0, typeof(StrokeElement))]
[MessagePack.Union(1, typeof(ShapeElement))]
[MessagePack.Union(2, typeof(ImageElement))]
[MessagePack.Union(3, typeof(TextElement))]
public abstract partial class BoardElement
{
    [Key(0)]
    public Guid Id { get; set; }

    [Key(1)]
    public Vector2 Position { get; set; }

    [Key(2)]
    public Vector2 Size { get; set; }

    [Key(3)]
    public float Rotation { get; set; }

    [Key(4)]
    public int ZIndex { get; set; }

    [Key(5)]
    public Guid OwnerId { get; set; }

    [Key(6)]
    public bool IsLocked { get; set; }
}
