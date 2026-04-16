using System.Numerics;
using BFGA.Core;
using BFGA.Core.Models;
using MessagePack;
using SkiaSharp;

namespace BFGA.Network.Protocol;

/// <summary>
/// Operation types for the networking protocol
/// </summary>
public enum OperationType
{
    AddElement = 0,
    UpdateElement = 1,
    DeleteElement = 2,
    MoveElement = 3,
    CursorUpdate = 4,
    DrawStrokePoint = 5,
    CancelStroke = 6,
    RequestFullSync = 7,
    FullSyncResponse = 8,
    PeerJoined = 9,
    PeerLeft = 10,
    Undo = 11,
    Redo = 12,
    LaserPointer = 13,
    UpdatePresenceColor = 14
}

/// <summary>
/// Base class for all board operations transmitted over the network.
/// Uses MessagePack union attributes for polymorphic serialization.
/// </summary>
[MessagePackObject]
[MessagePack.Union(0, typeof(AddElementOperation))]
[MessagePack.Union(1, typeof(UpdateElementOperation))]
[MessagePack.Union(2, typeof(DeleteElementOperation))]
[MessagePack.Union(3, typeof(MoveElementOperation))]
[MessagePack.Union(4, typeof(CursorUpdateOperation))]
[MessagePack.Union(5, typeof(DrawStrokePointOperation))]
[MessagePack.Union(6, typeof(CancelStrokeOperation))]
[MessagePack.Union(7, typeof(RequestFullSyncOperation))]
[MessagePack.Union(8, typeof(FullSyncResponseOperation))]
[MessagePack.Union(9, typeof(PeerJoinedOperation))]
[MessagePack.Union(10, typeof(PeerLeftOperation))]
[MessagePack.Union(11, typeof(UndoOperation))]
[MessagePack.Union(12, typeof(RedoOperation))]
[MessagePack.Union(13, typeof(LaserPointerOperation))]
[MessagePack.Union(14, typeof(UpdatePresenceColorOperation))]
public abstract class BoardOperation
{
    /// <summary>
    /// The type of this operation for quick identification without deserializing the full payload.
    /// </summary>
    [Key(0)]
    public abstract OperationType Type { get; }

    /// <summary>
    /// Client ID of the sender. Set by the sending client and preserved through host validation.
    /// </summary>
    [Key(1)]
    public Guid SenderId { get; set; }

    /// <summary>
    /// Timestamp when this operation was created, for ordering and debugging.
    /// </summary>
    [Key(2)]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

/// <summary>
/// Creates a new element on the board.
/// Direction: Client -> Host -> All clients
/// </summary>
[MessagePackObject]
public class AddElementOperation : BoardOperation
{
    public override OperationType Type => OperationType.AddElement;

    /// <summary>
    /// The element to add. Must have a unique Id set by the client.
    /// </summary>
    [Key(3)]
    public BoardElement Element { get; set; } = null!;

    public AddElementOperation() { }

    public AddElementOperation(BoardElement element)
    {
        Element = element;
    }
}

/// <summary>
/// Updates properties of an existing element.
/// Direction: Client -> Host -> All clients
/// </summary>
[MessagePackObject]
public class UpdateElementOperation : BoardOperation
{
    public override OperationType Type => OperationType.UpdateElement;

    /// <summary>
    /// The element to update.
    /// </summary>
    [Key(3)]
    public Guid ElementId { get; set; }

    /// <summary>
    /// Dictionary of property names to new values. 
    /// Supported keys: "Position", "Size", "Rotation", "ZIndex", "IsLocked", 
    /// and element-specific properties like "Text", "Color", "Thickness", etc.
    /// </summary>
    [Key(4)]
    public Dictionary<string, object> ModifiedProperties { get; set; } = new();

    public UpdateElementOperation() { }

    public UpdateElementOperation(Guid elementId, Dictionary<string, object> modifiedProperties)
    {
        ElementId = elementId;
        ModifiedProperties = modifiedProperties;
    }
}

/// <summary>
/// Removes an element from the board.
/// Direction: Client -> Host -> All clients
/// </summary>
[MessagePackObject]
public class DeleteElementOperation : BoardOperation
{
    public override OperationType Type => OperationType.DeleteElement;

    /// <summary>
    /// The ID of the element to delete.
    /// </summary>
    [Key(3)]
    public Guid ElementId { get; set; }

    public DeleteElementOperation() { }

    public DeleteElementOperation(Guid elementId)
    {
        ElementId = elementId;
    }
}

/// <summary>
/// Changes an element's position, size, or rotation.
/// Direction: Client -> Host -> All clients
/// </summary>
[MessagePackObject]
public class MoveElementOperation : BoardOperation
{
    public override OperationType Type => OperationType.MoveElement;

    /// <summary>
    /// The ID of the element to move.
    /// </summary>
    [Key(3)]
    public Guid ElementId { get; set; }

    /// <summary>
    /// New position of the element.
    /// </summary>
    [Key(4)]
    public Vector2 Position { get; set; }

    /// <summary>
    /// New size of the element.
    /// </summary>
    [Key(5)]
    public Vector2 Size { get; set; }

    /// <summary>
    /// New rotation in degrees.
    /// </summary>
    [Key(6)]
    public float Rotation { get; set; }

    public MoveElementOperation() { }

    public MoveElementOperation(Guid elementId, Vector2 position, Vector2 size, float rotation)
    {
        ElementId = elementId;
        Position = position;
        Size = size;
        Rotation = rotation;
    }
}

/// <summary>
/// Reports a player's cursor position for collaborative awareness.
/// Direction: Client -> Host -> All clients
/// Note: Sent on Channel 1 (unreliable) at ~60 updates/sec
/// </summary>
[MessagePackObject]
public class CursorUpdateOperation : BoardOperation
{
    public override OperationType Type => OperationType.CursorUpdate;

    /// <summary>
    /// The cursor's position in board coordinates.
    /// </summary>
    [Key(3)]
    public Vector2 Position { get; set; }

    public CursorUpdateOperation() { }

    public CursorUpdateOperation(Guid clientId, Vector2 position)
    {
        SenderId = clientId;
        Position = position;
    }
}

/// <summary>
/// Streams an individual point during stroke drawing for real-time preview.
/// Direction: Client -> Host -> All clients
/// </summary>
[MessagePackObject]
public class DrawStrokePointOperation : BoardOperation
{
    public override OperationType Type => OperationType.DrawStrokePoint;

    /// <summary>
    /// The stroke ID this point belongs to.
    /// </summary>
    [Key(3)]
    public Guid StrokeId { get; set; }

    /// <summary>
    /// The new point to add to the stroke.
    /// </summary>
    [Key(4)]
    public Vector2 Point { get; set; }

    public DrawStrokePointOperation() { }

    public DrawStrokePointOperation(Guid strokeId, Vector2 point)
    {
        StrokeId = strokeId;
        Point = point;
    }
}

/// <summary>
/// Cancels an in-progress stroke that was not finished.
/// Direction: Client -> Host -> All clients
/// </summary>
[MessagePackObject]
public class CancelStrokeOperation : BoardOperation
{
    public override OperationType Type => OperationType.CancelStroke;

    /// <summary>
    /// The stroke ID to cancel.
    /// </summary>
    [Key(3)]
    public Guid StrokeId { get; set; }

    public CancelStrokeOperation() { }

    public CancelStrokeOperation(Guid strokeId)
    {
        StrokeId = strokeId;
    }
}

/// <summary>
/// Requests a complete board state sync from the host.
/// Direction: Client -> Host
/// </summary>
[MessagePackObject]
public class RequestFullSyncOperation : BoardOperation
{
    public override OperationType Type => OperationType.RequestFullSync;
}

/// <summary>
/// Contains the complete board state and player roster.
/// Direction: Host -> Client (in response to RequestFullSync)
/// </summary>
[MessagePackObject]
public class FullSyncResponseOperation : BoardOperation
{
    public override OperationType Type => OperationType.FullSyncResponse;

    /// <summary>
    /// The ClientId assigned to this client by the host.
    /// </summary>
    [Key(3)]
    public Guid ClientId { get; set; }

    /// <summary>
    /// The current board state with all elements.
    /// </summary>
    [Key(4)]
    public BoardState BoardState { get; set; } = new();

    /// <summary>
    /// The current player roster mapping ClientId to PlayerInfo.
    /// </summary>
    [Key(5)]
    public Dictionary<Guid, PlayerInfo> PlayerRoster { get; set; } = new();

    public FullSyncResponseOperation() { }

    public FullSyncResponseOperation(Guid clientId, BoardState boardState, Dictionary<Guid, PlayerInfo> playerRoster)
    {
        ClientId = clientId;
        BoardState = boardState;
        PlayerRoster = playerRoster;
    }
}

/// <summary>
/// Notifies that a new player has connected.
/// Direction: Host -> All clients
/// </summary>
[MessagePackObject]
public class PeerJoinedOperation : BoardOperation
{
    public override OperationType Type => OperationType.PeerJoined;

    /// <summary>
    /// The assigned ClientId for the new player.
    /// </summary>
    [Key(3)]
    public Guid ClientId { get; set; }

    /// <summary>
    /// The player's chosen display name.
    /// </summary>
    [Key(4)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The color assigned to this player for cursors and attribution.
    /// </summary>
    [Key(5)]
    public SKColor AssignedColor { get; set; }

    public PeerJoinedOperation() { }

    public PeerJoinedOperation(Guid clientId, string displayName, SKColor assignedColor)
    {
        ClientId = clientId;
        DisplayName = displayName;
        AssignedColor = assignedColor;
    }
}

/// <summary>
/// Notifies that a player has disconnected.
/// Direction: Host -> All clients
/// </summary>
[MessagePackObject]
public class PeerLeftOperation : BoardOperation
{
    public override OperationType Type => OperationType.PeerLeft;

    /// <summary>
    /// The ClientId of the player who left.
    /// </summary>
    [Key(3)]
    public Guid ClientId { get; set; }

    public PeerLeftOperation() { }

    public PeerLeftOperation(Guid clientId)
    {
        ClientId = clientId;
    }
}

/// <summary>
/// Requests the host to undo the sender's last operation.
/// Direction: Client -> Host
/// </summary>
[MessagePackObject]
public sealed class UndoOperation : BoardOperation
{
    public override OperationType Type => OperationType.Undo;
}

/// <summary>
/// Requests the host to redo the sender's last undone operation.
/// Direction: Client -> Host
/// </summary>
[MessagePackObject]
public sealed class RedoOperation : BoardOperation
{
    public override OperationType Type => OperationType.Redo;
}

/// <summary>
/// Transmits a laser pointer position for ephemeral collaborative pointing.
/// Direction: Client -> Host -> All clients (relay only, no board state mutation)
/// Note: Sent on Channel 2 (sequenced) — dedicated channel, not shared with cursor or reliable ops
/// </summary>
[MessagePackObject]
public class LaserPointerOperation : BoardOperation
{
    public override OperationType Type => OperationType.LaserPointer;

    /// <summary>
    /// The laser pointer position in board (scene-space) coordinates.
    /// </summary>
    [Key(3)]
    public Vector2 Position { get; set; }

    /// <summary>
    /// Whether the laser is currently active (pressed) or deactivating (released).
    /// When false, signals peers to begin fade-out of this user's laser trail.
    /// </summary>
    [Key(4)]
    public bool IsActive { get; set; }

    public LaserPointerOperation() { }

    public LaserPointerOperation(Guid clientId, Vector2 position, bool isActive)
    {
        SenderId = clientId;
        Position = position;
        IsActive = isActive;
    }
}

/// <summary>
/// Requests host-authoritative update of sender's shared presence color.
/// Direction: Client -> Host
/// </summary>
[MessagePackObject]
public sealed class UpdatePresenceColorOperation : BoardOperation
{
    public override OperationType Type => OperationType.UpdatePresenceColor;

    [Key(3)]
    public SKColor Color { get; set; }

    public UpdatePresenceColorOperation()
    {
    }

    public UpdatePresenceColorOperation(SKColor color)
    {
        Color = color;
    }
}
