using MessagePack;

namespace BFGA.Network.Protocol;

/// <summary>
/// Wrapper class for network messages containing board operations.
/// This wrapper is needed to ensure proper polymorphic serialization of BoardOperation.
/// </summary>
[MessagePackObject]
public class NetworkMessage
{
    /// <summary>
    /// The operation contained in this message.
    /// </summary>
    [Key(0)]
    public BoardOperation Operation { get; set; } = null!;

    public NetworkMessage() { }

    public NetworkMessage(BoardOperation operation)
    {
        Operation = operation;
    }
}