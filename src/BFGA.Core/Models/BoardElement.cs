using System.Numerics;
using MessagePack;

namespace BFGA.Core.Models;

[MessagePackObject]
[MessagePack.Union(0, typeof(StrokeElement))]
[MessagePack.Union(1, typeof(ShapeElement))]
[MessagePack.Union(2, typeof(ImageElement))]
[MessagePack.Union(3, typeof(TextElement))]
public abstract partial class BoardElement { }
