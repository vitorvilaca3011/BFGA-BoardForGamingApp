using System.Diagnostics;
using System.Net;
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

    /// <summary>
    /// Channel 0: Reliable ordered for all operations
    /// </summary>
    public const int ReliableChannel = 0;

    /// <summary>
    /// Channel 1: Unreliable for cursor updates
    /// </summary>
    public const int UnreliableChannel = 1;

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
    /// </summary>
    /// <param name="hostAddress">The host's IP address or hostname.</param>
    /// <param name="port">The port to connect to (default 7777).</param>
    /// <returns>A task that completes when the connection is established.</returns>
    public async Task ConnectAsync(string hostAddress, int port = 7777)
    {
        if (_isConnected || _isDisposed) return;

        _netManager = new NetManager(new ClientEventListener(this));
        if (!_netManager.Start())
        {
            throw new InvalidOperationException($"Failed to start network client");
        }
        
        // Connect using display name as connection key (server retrieves it via ConnectionKey property)
        _netManager.Connect(hostAddress, port, _displayName);

        // Poll events while waiting for connection
        var startTime = DateTime.UtcNow;
        while (!_connectionEvent.Wait(10))
        {
            _netManager.PollEvents();
            if ((DateTime.UtcNow - startTime).TotalMilliseconds > 5000)
            {
                _netManager.DisconnectAll();
                _netManager.Stop();
                _netManager.Dispose();
                _netManager = null;
                return;
            }
        }
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
        if (!_isConnected || _isDisposed) return;

        _connectionEvent.Reset();
        _netManager?.DisconnectAll();
        _netManager?.Stop();
        _netManager?.Dispose();
        _netManager = null;
        _connectedPeer = null;
        _isConnected = false;
    }

    /// <summary>
    /// Sends an operation to the host.
    /// </summary>
    /// <param name="operation">The operation to send.</param>
    /// <param name="reliable">Whether to use reliable delivery (default true). Ignored for CursorUpdate.</param>
    public void SendOperation(BoardOperation operation, bool reliable = true)
    {
        if (!_isConnected || _connectedPeer == null || _isDisposed) return;

        operation.SenderId = _clientId;
        
        // CursorUpdate always uses unreliable channel regardless of reliable parameter
        int channel;
        if (operation is CursorUpdateOperation)
        {
            channel = UnreliableChannel;
        }
        else
        {
            channel = reliable ? ReliableChannel : UnreliableChannel;
        }
        
        _dataWriter.Reset();
        _dataWriter.Put(OperationSerializer.Serialize(operation));
        _connectedPeer.Send(_dataWriter, channel);
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
        _connectionEvent.Set();
        Connected?.Invoke(this, EventArgs.Empty);
    }

    private void SetDisconnected()
    {
        _isConnected = false;
        _connectionEvent.Reset();
        Disconnected?.Invoke(this, EventArgs.Empty);
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

        public void OnNetworkError(IPEndPoint endPoint, int socketErrorCode)
        {
            Debug.WriteLine($"[GameClient] Network error to {endPoint}: socket error {socketErrorCode}");
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            try
            {
                var data = reader.GetRemainingBytes();
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

        public void OnLatencyUpdate(NetPeer peer, int latency)
        {
        }
    }
}
