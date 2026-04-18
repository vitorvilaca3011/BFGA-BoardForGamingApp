using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using BFGA.Core;
using BFGA.Network.Protocol;
using LiteNetLib;
using LiteNetLib.Utils;

namespace BFGA.Network;

/// <summary>
/// Arguments for operation-received events on the client.
/// </summary>
public class ClientOperationReceivedEventArgs : EventArgs
{
    public BoardOperation Operation { get; }

    public ClientOperationReceivedEventArgs(BoardOperation operation)
    {
        Operation = operation;
    }
}

/// <summary>
/// Client-side LiteNetLib wrapper for connecting to a game session.
/// </summary>
public class GameClient : IDisposable
{
    private NetManager? _netManager;
    private NetPeer? _connectedPeer;
    private NetDataWriter _dataWriter;
    private readonly string _displayName;
    private Guid _clientId;
    private bool _isConnected;
    private bool _isDisposed;
    private readonly ManualResetEventSlim _connectionEvent = new(false);
    private TaskCompletionSource<bool>? _connectCompletionSource;

    /// <summary>
    /// Channel 0: Reliable ordered for all operations
    /// </summary>
    public const int ReliableChannel = 0;

    /// <summary>
    /// Channel 1: Unreliable for cursor updates
    /// </summary>
    public const int UnreliableChannel = 1;

    /// <summary>
    /// Channel 2: Sequenced for laser pointer updates
    /// </summary>
    public const int SequencedChannel = 2;

    /// <summary>
    /// Raised when an operation is received from the host.
    /// </summary>
    public event EventHandler<ClientOperationReceivedEventArgs>? OperationReceived;

    /// <summary>
    /// Raised when connected to a host.
    /// </summary>
    public event EventHandler? Connected;

    /// <summary>
    /// Raised when disconnected from the host.
    /// </summary>
    public event EventHandler? Disconnected;

    /// <summary>
    /// Gets whether the client is currently connected.
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// Gets the client's display name.
    /// </summary>
    public string DisplayName => _displayName;

    /// <summary>
    /// Gets the assigned client ID.
    /// </summary>
    public Guid ClientId => _clientId;

    public GameClient(string displayName)
    {
        _displayName = displayName;
        _dataWriter = new NetDataWriter();
    }

    /// <summary>
    /// Connects to a game host asynchronously.
    /// After calling this method, you must poll events on both host and client
    /// until IsConnected becomes true.
    /// </summary>
    /// <param name="hostAddress">The host's IP address or hostname.</param>
    /// <param name="port">The port to connect to (default 7777).</param>
    public Task ConnectAsync(string hostAddress, int port = 7777)
    {
        if (_isDisposed) return Task.FromException(new ObjectDisposedException(nameof(GameClient)));
        if (_isConnected) return Task.CompletedTask;
        if (_connectCompletionSource is not null) return _connectCompletionSource.Task;

        _connectCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _netManager = new NetManager(new ClientEventListener(this));
        _netManager.ChannelsCount = 3; // Match host channel configuration (Reliable=0, Unreliable=1, Sequenced=2)
        if (!_netManager.Start())
        {
            _connectCompletionSource.TrySetException(new InvalidOperationException("Failed to start network client"));
            _connectCompletionSource = null;
            CleanupNetworkState();
            throw new InvalidOperationException($"Failed to start network client");
        }

        // Connect using display name as connection key (server retrieves it via ConnectionKey property)
        _netManager.Connect(hostAddress, port, _displayName);
        return _connectCompletionSource.Task;
    }

    /// <summary>
    /// Waits for a connection to be established.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds.</param>
    /// <returns>True if connected within timeout, false otherwise.</returns>
    public bool WaitForConnection(int timeoutMs)
    {
        if (_isConnected) return true;
        return _connectionEvent.Wait(timeoutMs);
    }

    /// <summary>
    /// Disconnects from the current host.
    /// </summary>
    public void Disconnect()
    {
        if (_isDisposed) return;

        if (_connectCompletionSource is not null && !_isConnected)
        {
            _connectCompletionSource.TrySetException(new InvalidOperationException("Disconnected before connection completed."));
            _connectCompletionSource = null;
            CleanupNetworkState();
        }

        _connectionEvent.Reset();
        CleanupNetworkState();
        _isConnected = false;
    }

    /// <summary>
    /// Sends an operation to the host.
    /// </summary>
    /// <param name="operation">The operation to send.</param>
    /// <param name="reliable">Whether to use reliable delivery (default true). Ignored for CursorUpdate.</param>
    public void SendOperation(BoardOperation operation, bool reliable = true)
    {
        if (!_isConnected || _connectedPeer == null || _isDisposed)
        {
            Debug.WriteLine($"[GameClient] SendOperation called but not connected: IsConnected={_isConnected}, Peer={_connectedPeer}, Disposed={_isDisposed}");
            return;
        }

        operation.SenderId = _clientId;

        // Determine channel and delivery method
        int channel;
        DeliveryMethod deliveryMethod;

        if (operation is LaserPointerOperation)
        {
            channel = SequencedChannel;
            deliveryMethod = DeliveryMethod.Sequenced;
        }
        else if (operation is CursorUpdateOperation)
        {
            channel = UnreliableChannel;
            deliveryMethod = DeliveryMethod.Unreliable;
        }
        else
        {
            channel = reliable ? ReliableChannel : UnreliableChannel;
            deliveryMethod = reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable;
        }

        _dataWriter.Reset();
        _dataWriter.PutBytesWithLength(OperationSerializer.Serialize(operation));
        _connectedPeer.Send(_dataWriter, (byte)channel, deliveryMethod);
        Debug.WriteLine($"[GameClient] Sent operation {operation.Type} on channel {channel}");
    }

    /// <summary>
    /// Polls for network events. Call this regularly in your game loop.
    /// </summary>
    public void PollEvents()
    {
        _netManager?.PollEvents();
    }

    /// <summary>
    /// Requests a full board state sync from the host.
    /// </summary>
    public void RequestFullSync()
    {
        SendOperation(new RequestFullSyncOperation(), reliable: true);
    }

    private void HandleOperation(BoardOperation operation)
    {
        // Update our ClientId if this is a FullSyncResponse with our info
        if (operation is FullSyncResponseOperation syncResponse)
        {
            // Store the client ID assigned by the host
            if (syncResponse.ClientId != Guid.Empty)
            {
                _clientId = syncResponse.ClientId;
            }
        }

        OperationReceived?.Invoke(this, new ClientOperationReceivedEventArgs(operation));
    }

    private void SetConnected(NetPeer peer)
    {
        _connectedPeer = peer;
        _isConnected = true;
        _connectCompletionSource?.TrySetResult(true);
        _connectCompletionSource = null;
        _connectionEvent.Set();
        Connected?.Invoke(this, EventArgs.Empty);
    }

    private void SetDisconnected()
    {
        if (_connectCompletionSource is not null && !_isConnected)
        {
            _connectCompletionSource.TrySetException(new InvalidOperationException("Connection was lost before it completed."));
            _connectCompletionSource = null;
            CleanupNetworkState();
        }

        _isConnected = false;
        _connectionEvent.Reset();
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    private void FailConnectAttempt(string message)
    {
        if (_connectCompletionSource is null)
        {
            return;
        }

        _connectCompletionSource.TrySetException(new InvalidOperationException(message));
        _connectCompletionSource = null;
        CleanupNetworkState();
    }

    private void CleanupNetworkState()
    {
        _netManager?.DisconnectAll();
        _netManager?.Stop();
        _netManager = null;
        _connectedPeer = null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        Disconnect();
        _isDisposed = true;
        _connectionEvent.Dispose();
    }

    private class ClientEventListener : INetEventListener
    {
        private readonly GameClient _client;

        public ClientEventListener(GameClient client)
        {
            _client = client;
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            request.Accept();
        }

        public void OnPeerConnected(NetPeer peer)
        {
            _client.SetConnected(peer);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            _client.SetDisconnected();
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
        {
            _client.FailConnectAttempt($"Network error connecting to {endPoint}: socket error {socketErrorCode}");
            Debug.WriteLine($"[GameClient] Network error to {endPoint}: socket error {socketErrorCode}");
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            try
            {
                var data = reader.GetBytesWithLength();
                var operation = OperationSerializer.Deserialize(data);
                _client.HandleOperation(operation);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GameClient] Dropped malformed message: {ex.Message}");
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
