using BFGA.Core;
using BFGA.Network.Protocol;
using MessagePack;

namespace BFGA.Network;

/// <summary>
/// Serializes and deserializes board operations using MessagePack.
/// Uses the resolver from BFGA.Core for consistent type handling.
/// </summary>
public static class OperationSerializer
{
    /// <summary>
    /// Serializes a board operation to a byte array.
    /// </summary>
    /// <param name="operation">The operation to serialize.</param>
    /// <returns>The serialized bytes.</returns>
    public static byte[] Serialize(BoardOperation operation)
    {
        return MessagePackSerializer.Serialize(operation, MessagePackSetup.Options);
    }

    /// <summary>
    /// Deserializes a board operation from a byte array.
    /// </summary>
    /// <param name="data">The serialized operation bytes.</param>
    /// <returns>The deserialized board operation.</returns>
    public static BoardOperation Deserialize(byte[] data)
    {
        return MessagePackSerializer.Deserialize<BoardOperation>(data, MessagePackSetup.Options);
    }

    /// <summary>
    /// Serializes an operation to a stream.
    /// </summary>
    /// <param name="operation">The operation to serialize.</param>
    /// <param name="stream">The stream to write to.</param>
    public static void SerializeToStream(BoardOperation operation, Stream stream)
    {
        MessagePackSerializer.Serialize(operation, stream, MessagePackSetup.Options);
    }

    /// <summary>
    /// Deserializes an operation from a stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>The deserialized board operation.</returns>
    public static BoardOperation DeserializeFromStream(Stream stream)
    {
        return MessagePackSerializer.Deserialize<BoardOperation>(stream, MessagePackSetup.Options);
    }
}
