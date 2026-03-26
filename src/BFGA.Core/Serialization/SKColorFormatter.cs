using MessagePack;
using MessagePack.Formatters;
using SkiaSharp;

namespace BFGA.Core.Serialization;

public class SKColorFormatter : IMessagePackFormatter<SKColor>
{
    public void Serialize(ref MessagePackWriter writer, SKColor value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(4);
        writer.Write(value.Red);
        writer.Write(value.Green);
        writer.Write(value.Blue);
        writer.Write(value.Alpha);
    }

    public SKColor Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var count = reader.ReadArrayHeader();
        if (count != 4)
            throw new MessagePackSerializationException("Invalid SKColor format");

        var r = reader.ReadByte();
        var g = reader.ReadByte();
        var b = reader.ReadByte();
        var a = reader.ReadByte();
        return new SKColor(r, g, b, a);
    }
}
