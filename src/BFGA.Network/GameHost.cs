using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using BFGA.Core;
using BFGA.Core.Models;
using BFGA.Network.Protocol;
using LiteNetLib;
using LiteNetLib.Utils;
using MessagePack;
using SkiaSharp;

namespace BFGA.Network;

/// <summary>
/// Arguments for peer-joined events.
/// </summary>
public class PeerJoinedEventArgs : EventArgs
{
    public Guid ClientId { get; }
    public string DisplayName { get; }
    public SKColor AssignedColor { get; }

    public PeerJoinedEventArgs(Guid clientId, string displayName, SKColor assignedColor)
    {
        ClientId = clientId;
        DisplayName = displayName;
        AssignedColor = assignedColor;
    }
}

/// <summary>
/// Arguments for peer-left events.
/// </summary>
public class PeerLeftEventArgs : EventArgs
{
    public Guid ClientId { get; }

    public PeerLeftEventArgs(Guid clientId)
    {
        ClientId = clientId;
    }
}

/// <summary>
/// Arguments for operation-received events.
/// </summary>
public class OperationReceivedEventArgs : EventArgs
{
    public BoardOperation Operation { get; }
    public Guid ClientId { get; }

    public OperationReceivedEventArgs(BoardOperation operation, Guid clientId)
    {
        Operation = operation;
        ClientId = clientId;
    }
}

/// <summary>
/// Host-side LiteNetLib wrapper for hosting a game session.
/// Manages player connections, validates operations, and broadcasts to all clients.
/// </summary>
public class GameHost : IDisposable
{
    private readonly NetManager _netManager;
    // NOTE: NetDataWriter is not thread-safe. This is acceptable for v0.1 where the API
    // is designed for single-threaded UI use. If multi-threaded access is needed in the future,
    // a lock or thread-local NetDataWriter instances would be required.
    private readonly NetDataWriter _dataWriter;
    private readonly ConcurrentDictionary<Guid, (NetPeer Peer, PlayerInfo Info)> _players;
    private readonly Dictionary<Guid, BoardElement> _boardElements;
    private readonly Dictionary<IPEndPoint, string> _pendingDisplayNames;
    private readonly object _lockObject = new();
    
    private BoardState _boardState;
    private bool _isRunning;
    private int _port;

    /// <summary>
    /// Channel 0: Reliable ordered for all operations
    /// </summary>
    public const int ReliableChannel = 0;

    /// <summary>
    /// Channel 1: Unreliable for cursor updates
    /// </summary>
    public const int UnreliableChannel = 1;

    /// <summary>
    /// Raised when a player joins the session.
    /// </summary>
    public event EventHandler<PeerJoinedEventArgs>? PeerJoined;

    /// <summary>
    /// Raised when a player leaves the session.
    /// </summary>
    public event EventHandler<PeerLeftEventArgs>? PeerLeft;

    /// <summary>
    /// Raised when an operation is received from a client.
    /// </summary>
    public event EventHandler<OperationReceivedEventArgs>? OperationReceived;

    /// <summary>
    /// Gets whether the host is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets the current player roster.
    /// </summary>
    public IReadOnlyDictionary<Guid, PlayerInfo> PlayerRoster => _players.ToDictionary(
        kvp => kvp.Key, 
        kvp => kvp.Value.Info);

    /// <summary>
    /// Gets the port being listened on.
    /// </summary>
    public int Port => _port;

    /// <summary>
    /// Gets the current board state.
    /// NOTE: The returned BoardState is a direct reference to internal state, not a clone.
    /// Callers can mutate internal state. For v0.1 this is acceptable given the single-threaded
    /// UI model. Full isolation would require cloning which has performance implications.
    /// </summary>
    public BoardState BoardState => _boardState;

    public void ReplaceBoardState(BoardState snapshot)
    {
        lock (_lockObject)
        {
            _boardState = CloneBoardState(snapshot);
            _boardElements.Clear();

            foreach (var element in _boardState.Elements)
            {
                _boardElements[element.Id] = element;
            }

            _boardState.Elements = _boardElements.Values.ToList();
            _boardState.LastModified = DateTime.UtcNow;
        }
    }

    public bool TryApplyLocalOperation(BoardOperation operation)
    {
        if (!_isRunning || !ValidateOperation(operation))
        {
            return false;
        }

        ApplyOperation(operation);
        return true;
    }

    public void SyncAllClients()
    {
        if (!_isRunning)
            return;

        foreach (var player in _players.Values)
        {
            SendFullSync(player.Peer, GetClientId(player.Peer));
        }
    }

    public void BroadcastFullSync()
    {
        SyncAllClients();
    }

    private static readonly SKColor[] PlayerColors = new[]
    {
        SKColors.Red,
        SKColors.Blue,
        SKColors.Green,
        SKColors.Orange,
        SKColors.Purple,
        SKColors.Cyan,
        SKColors.Yellow,
        SKColors.Magenta
    };

    public GameHost()
    {
        _players = new ConcurrentDictionary<Guid, (NetPeer, PlayerInfo)>();
        _boardElements = new Dictionary<Guid, BoardElement>();
        _pendingDisplayNames = new Dictionary<IPEndPoint, string>();
        _boardState = new BoardState();
        _dataWriter = new NetDataWriter();
        _netManager = new NetManager(new HostEventListener(this));
        _netManager.ChannelsCount = 2;
    }

    /// <summary>
    /// Starts the host listening on the specified port.
    /// </summary>
    /// <param name="port">The port to listen on (default 7777). Use 0 to let the OS assign a port.</param>
    public void Start(int port = 7777)
    {
        if (_isRunning) return;

        _port = port;
        if (!_netManager.Start(port))
        {
            throw new InvalidOperationException($"Failed to start network host on port {port}");
        }
        // Query the actual bound port (important when port=0 for dynamic allocation)
        _port = _netManager.LocalPort;
        _isRunning = true;
    }

    /// <summary>
    /// Stops the host and disconnects all players.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;

        _netManager.DisconnectAll();
        _netManager.Stop();
        _players.Clear();
        _isRunning = false;
    }

    /// <summary>
    /// Polls for network events. Call this regularly in your game loop.
    /// </summary>
    public void PollEvents()
    {
        _netManager.PollEvents();
    }

    /// <summary>
    /// Broadcasts an operation to all connected clients.
    /// </summary>
    /// <param name="operation">The operation to broadcast.</param>
    /// <param name="reliable">Whether to use reliable delivery (default true).</param>
    public void BroadcastOperation(BoardOperation operation, bool reliable = true)
    {
        if (!_isRunning) return;

        var channel = reliable ? ReliableChannel : UnreliableChannel;
        _dataWriter.Reset();
        _dataWriter.PutBytesWithLength(OperationSerializer.Serialize(operation));
        var deliveryMethod = reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable;

        foreach (var player in _players.Values)
        {
            player.Peer.Send(_dataWriter, (byte)channel, deliveryMethod);
        }
    }

    /// <summary>
    /// Gets the current player roster.
    /// </summary>
    /// <returns>Dictionary mapping ClientId to PlayerInfo.</returns>
    public Dictionary<Guid, PlayerInfo> GetPlayerRoster()
    {
        return _players.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Info);
    }

    /// <summary>
    /// Updates the board state with the latest element.
    /// </summary>
    /// <param name="element">The element to add or update.</param>
    public void UpdateBoardElement(BoardElement element)
    {
        lock (_lockObject)
        {
            _boardElements[element.Id] = element;
            _boardState.Elements = _boardElements.Values.ToList();
            _boardState.LastModified = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Removes an element from the board.
    /// </summary>
    /// <param name="elementId">The ID of the element to remove.</param>
    public bool RemoveBoardElement(Guid elementId)
    {
        lock (_lockObject)
        {
            if (_boardElements.Remove(elementId))
            {
                _boardState.Elements = _boardElements.Values.ToList();
                _boardState.LastModified = DateTime.UtcNow;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Gets an element by ID.
    /// </summary>
    /// <param name="elementId">The element ID.</param>
    /// <returns>The element if found, null otherwise.</returns>
    public BoardElement? GetElement(Guid elementId)
    {
        lock (_lockObject)
        {
            return _boardElements.TryGetValue(elementId, out var element) ? element : null;
        }
    }

    private void HandleOperation(NetPeer peer, BoardOperation operation)
    {
        operation.SenderId = GetClientId(peer);
        OperationReceived?.Invoke(this, new OperationReceivedEventArgs(operation, operation.SenderId));

        // Validate and apply operation
        bool isValid = ValidateOperation(operation);
        if (!isValid) return;

        ApplyOperation(operation);
        
        // Broadcast to all connected clients (including sender for consistency)
        BroadcastOperation(operation, IsOperationReliable(operation));
    }

    private bool ValidateOperation(BoardOperation operation)
    {
        // Reject host-only operations that should never come from clients
        switch (operation)
        {
            case PeerJoinedOperation:
            case PeerLeftOperation:
            case FullSyncResponseOperation:
                return false;
        }

        switch (operation)
        {
            case UpdateElementOperation update:
                lock (_lockObject)
                {
                    if (!_boardElements.ContainsKey(update.ElementId))
                    {
                        return false;
                    }
                }
                break;
            case MoveElementOperation move:
                lock (_lockObject)
                {
                    if (!_boardElements.ContainsKey(move.ElementId))
                    {
                        return false;
                    }
                }
                break;
            case DeleteElementOperation delete:
                lock (_lockObject)
                {
                    if (!_boardElements.ContainsKey(delete.ElementId))
                    {
                        return false;
                    }
                }
                break;
        }
        return true;
    }

    private void ApplyOperation(BoardOperation operation)
    {
        lock (_lockObject)
        {
            switch (operation)
            {
                case AddElementOperation add:
                    _boardElements[add.Element.Id] = add.Element;
                    break;
                case UpdateElementOperation update:
                    if (_boardElements.TryGetValue(update.ElementId, out var existingElement))
                    {
                        ApplyModifiedProperties(existingElement, update.ModifiedProperties);
                    }
                    break;
                case DeleteElementOperation delete:
                    _boardElements.Remove(delete.ElementId);
                    break;
                case MoveElementOperation move:
                    if (_boardElements.TryGetValue(move.ElementId, out var movableElement))
                    {
                        movableElement.Position = move.Position;
                        movableElement.Size = move.Size;
                        movableElement.Rotation = move.Rotation;
                    }
                    break;
            }

            _boardState.Elements = _boardElements.Values.ToList();
            _boardState.LastModified = DateTime.UtcNow;
        }
    }

    private static BoardState CloneBoardState(BoardState source)
        => MessagePackSerializer.Deserialize<BoardState>(
            MessagePackSerializer.Serialize(source, BFGA.Core.MessagePackSetup.Options),
            BFGA.Core.MessagePackSetup.Options);

    private static void ApplyModifiedProperties(BoardElement element, Dictionary<string, object> properties)
    {
        foreach (var kvp in properties)
        {
            switch (kvp.Key)
            {
                case "Position" when kvp.Value is Vector2 pos:
                    element.Position = pos;
                    break;
                case "Size" when kvp.Value is Vector2 size:
                    element.Size = size;
                    break;
                case "Rotation" when kvp.Value is float f:
                    element.Rotation = f;
                    break;
                case "ZIndex" when kvp.Value is int z:
                    element.ZIndex = z;
                    break;
                case "IsLocked" when kvp.Value is bool b:
                    element.IsLocked = b;
                    break;
                // StrokeElement properties
                case "Color" when element is StrokeElement stroke && kvp.Value is SkiaSharp.SKColor strokeColor:
                    stroke.Color = strokeColor;
                    break;
                case "Thickness" when element is StrokeElement stroke:
                    stroke.Thickness = Convert.ToSingle(kvp.Value);
                    break;
                // ShapeElement properties
                case "StrokeColor" when element is ShapeElement shape && kvp.Value is SkiaSharp.SKColor strokeColor:
                    shape.StrokeColor = strokeColor;
                    break;
                case "FillColor" when element is ShapeElement shape && kvp.Value is SkiaSharp.SKColor fillColor:
                    shape.FillColor = fillColor;
                    break;
                case "StrokeWidth" when element is ShapeElement shape:
                    shape.StrokeWidth = Convert.ToSingle(kvp.Value);
                    break;
                case "Type" when element is ShapeElement shape && kvp.Value is ShapeType shapeType:
                    shape.Type = shapeType;
                    break;
                // TextElement properties
                case "Text" when element is TextElement text:
                    text.Text = kvp.Value?.ToString() ?? string.Empty;
                    break;
                case "FontSize" when element is TextElement text:
                    text.FontSize = Convert.ToSingle(kvp.Value);
                    break;
                case "Color" when element is TextElement text && kvp.Value is SkiaSharp.SKColor textColor:
                    text.Color = textColor;
                    break;
                case "FontFamily" when element is TextElement text:
                    text.FontFamily = kvp.Value?.ToString() ?? string.Empty;
                    break;
                // ImageElement properties
                case "ImageData" when element is ImageElement image && kvp.Value is byte[] imageData:
                    image.ImageData = imageData;
                    break;
                case "OriginalFileName" when element is ImageElement image:
                    image.OriginalFileName = kvp.Value?.ToString() ?? string.Empty;
                    break;
            }
        }
    }

    private static bool IsOperationReliable(BoardOperation operation)
    {
        // Cursor updates are unreliable, everything else is reliable
        return operation is not CursorUpdateOperation;
    }

    private Guid GetClientId(NetPeer peer)
    {
        foreach (var kvp in _players)
        {
            if (kvp.Value.Peer == peer)
                return kvp.Key;
        }
        return Guid.Empty;
    }

    private NetPeer? GetPeer(Guid clientId)
    {
        return _players.TryGetValue(clientId, out var player) ? player.Peer : null;
    }

    private Guid AssignClientId(NetPeer peer, string displayName)
    {
        var clientId = Guid.NewGuid();
        var colorIndex = _players.Count % PlayerColors.Length;
        var playerInfo = new PlayerInfo(displayName, PlayerColors[colorIndex]);
        
        _players[clientId] = (peer, playerInfo);
        peer.Tag = displayName;
        return clientId;
    }

    private void UpdatePlayerDisplayName(Guid clientId, string displayName)
    {
        if (_players.TryGetValue(clientId, out var player))
        {
            var updatedInfo = new PlayerInfo(displayName, player.Info.AssignedColor);
            _players[clientId] = (player.Peer, updatedInfo);
            player.Peer.Tag = displayName;
        }
    }

    private void RemovePlayer(Guid clientId)
    {
        _players.TryRemove(clientId, out _);
    }

    private void SendToClient(NetPeer peer, BoardOperation operation, bool reliable = true)
    {
        var channel = reliable ? ReliableChannel : UnreliableChannel;
        _dataWriter.Reset();
        _dataWriter.PutBytesWithLength(OperationSerializer.Serialize(operation));
        var deliveryMethod = reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable;
        peer.Send(_dataWriter, (byte)channel, deliveryMethod);
    }

    private void SendFullSync(NetPeer peer, Guid clientId)
    {
        var roster = GetPlayerRoster();
        var response = new FullSyncResponseOperation(clientId, _boardState, roster);
        SendToClient(peer, response, reliable: true);
    }

    public void Dispose()
    {
        Stop();
    }

    private class HostEventListener : INetEventListener
    {
        private readonly GameHost _host;

        public HostEventListener(GameHost host)
        {
            _host = host;
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            // Read display name from connection data (client sends it during connection)
            string displayName = $"Player{_host._players.Count + 1}";
            try
            {
                if (request.Data.AvailableBytes > 0)
                {
                    var nameFromClient = request.Data.GetString();
                    if (!string.IsNullOrEmpty(nameFromClient))
                    {
                        displayName = nameFromClient;
                    }
                }
            }
            catch
            {
                // Use default name if reading fails
            }
            
            // Store display name by endpoint for retrieval in OnPeerConnected
            _host._pendingDisplayNames[request.RemoteEndPoint] = displayName;
            request.Accept();
        }

        public void OnPeerConnected(NetPeer peer)
        {
            // Get display name that was stored in OnConnectionRequest
            string displayName = $"Player{_host._players.Count + 1}";
            
            if (_host._pendingDisplayNames.TryGetValue(new IPEndPoint(peer.Address, peer.Port), out var pendingName))
            {
                displayName = pendingName;
                _host._pendingDisplayNames.Remove(new IPEndPoint(peer.Address, peer.Port));
            }
            
            var clientId = _host.AssignClientId(peer, displayName);

            _host.PeerJoined?.Invoke(_host, new PeerJoinedEventArgs(
                clientId,
                displayName,
                _host._players[clientId].Info.AssignedColor));

            // Send full sync to the new client (includes the assigned clientId)
            _host.SendFullSync(peer, clientId);

            // Notify all clients of the new player
            var joinOp = new PeerJoinedOperation(
                clientId,
                displayName,
                _host._players[clientId].Info.AssignedColor);
            _host.BroadcastOperation(joinOp, reliable: true);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            var clientId = _host.GetClientId(peer);
            if (clientId != Guid.Empty)
            {
                _host.RemovePlayer(clientId);
                _host.PeerLeft?.Invoke(_host, new PeerLeftEventArgs(clientId));

                // Notify all clients
                var leaveOp = new PeerLeftOperation(clientId);
                _host.BroadcastOperation(leaveOp, reliable: true);
            }
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
        {
            Debug.WriteLine($"[Host] Network error from {endPoint}: socket error {socketErrorCode}");
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            try
            {
                var data = reader.GetBytesWithLength();
                Debug.WriteLine($"[Host] Received {data.Length} bytes from {peer.Address} on channel {channel}");
                var operation = OperationSerializer.Deserialize(data);
                Debug.WriteLine($"[Host] Deserialized operation {operation.Type}");
                
                // Handle special operations
                if (operation is RequestFullSyncOperation)
                {
                    var clientId = _host.GetClientId(peer);
                    _host.SendFullSync(peer, clientId);
                    return;
                }

                _host.HandleOperation(peer, operation);
            }
            catch (Exception ex)
            {
                // Malformed messages - drop silently (logged in debug)
                Debug.WriteLine($"[Host] Dropped malformed message from {peer.Address}: {ex.Message}");
            }
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }
    }
}
