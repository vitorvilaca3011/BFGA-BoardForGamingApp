using BFGA.Core;

namespace BFGA.Network;

/// <summary>
/// MessagePack setup for network operations.
/// Uses the shared resolver from BFGA.Core which handles both
/// BoardElement and BoardOperation union types via DynamicUnionResolver.
/// </summary>
public static class NetworkMessagePackSetup
{
    /// <summary>
    /// Gets the shared MessagePack options from BFGA.Core.
    /// These options include DynamicUnionResolver for union types
    /// (BoardElement, BoardOperation) and custom formatters for
    /// Vector2 and SKColor.
    /// </summary>
    public static readonly MessagePack.MessagePackSerializerOptions Options = MessagePackSetup.Options;
}