using BFGA.Network.Protocol;
using MessagePack;

namespace BFGA.Network;

/// <summary>
/// Serializes and deserializes board operations using MessagePack.
/// Uses the network-specific resolver that includes both core formatters and network union types.
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
        return MessagePackSerializer.Serialize(operation, NetworkMessagePackSetup.Options);
    }

    /// <summary>
    /// Deserializes a board operation from a byte array.
    /// </summary>
    /// <param name="data">The serialized operation bytes.</param>
    /// <returns>The deserialized board operation.</returns>
    public static BoardOperation Deserialize(byte[] data)
    {
        return MessagePackSerializer.Deserialize<BoardOperation>(data, NetworkMessagePackSetup.Options);
    }

    /// <summary>
    /// Deserializes a board operation from a byte array segment.
    /// </summary>
    /// <param name="data">The serialized operation bytes.</param>
    /// <returns>The deserialized board operation.</returns>
    public static BoardOperation Deserialize(ReadOnlyMemory<byte> data)
    {
        return MessagePackSerializer.Deserialize<BoardOperation>(data, NetworkMessagePackSetup.Options);
    }
}
