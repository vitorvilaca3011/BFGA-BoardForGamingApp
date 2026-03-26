using System.Numerics;
using MessagePack;
using MessagePack.Formatters;

namespace BFGA.Core.Serialization;

public class Vector2Formatter : IMessagePackFormatter<Vector2>
{
    public void Serialize(ref MessagePackWriter writer, Vector2 value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(2);
        writer.Write(value.X);
        writer.Write(value.Y);
    }

    public Vector2 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var count = reader.ReadArrayHeader();
        if (count != 2)
            throw new MessagePackSerializationException("Invalid Vector2 format");

        var x = reader.ReadSingle();
        var y = reader.ReadSingle();
        return new Vector2(x, y);
    }
}
