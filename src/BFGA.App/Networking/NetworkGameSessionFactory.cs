using BFGA.Core;
using BFGA.Network;
using SkiaSharp;

namespace BFGA.App.Networking;

public sealed class NetworkGameSessionFactory : IGameSessionFactory
{
    public IGameHostSession CreateHost()
    {
        return new HostSessionAdapter(new GameHost());
    }

    public IGameClientSession CreateClient(string displayName)
    {
        return new ClientSessionAdapter(new GameClient(displayName));
    }

    private sealed class HostSessionAdapter : IGameHostSession
    {
        private readonly GameHost _inner;

        public HostSessionAdapter(GameHost inner)
        {
            _inner = inner;
        }

        public event EventHandler<PeerJoinedEventArgs>? PeerJoined
        {
            add => _inner.PeerJoined += value;
            remove => _inner.PeerJoined -= value;
        }

        public event EventHandler<PeerLeftEventArgs>? PeerLeft
        {
            add => _inner.PeerLeft += value;
            remove => _inner.PeerLeft -= value;
        }

        public bool IsRunning => _inner.IsRunning;
        public int Port => _inner.Port;
        public BoardState BoardState => _inner.BoardState;

        public void Start(int port = 7777)
        {
            _inner.Start(port);
        }

        public void SetHostPresence(string displayName, SKColor assignedColor)
        {
            _inner.SetHostPresence(displayName, assignedColor);
        }

        public void ReplaceBoardState(BoardState snapshot)
        {
            _inner.ReplaceBoardState(snapshot);
        }

        public bool TryApplyLocalOperation(BFGA.Network.Protocol.BoardOperation operation)
        {
            return _inner.TryApplyLocalOperation(operation);
        }

        public void SyncAllClients()
        {
            _inner.SyncAllClients();
        }

        public void BroadcastFullSync()
        {
            _inner.BroadcastFullSync();
        }

        public void BroadcastOperation(BFGA.Network.Protocol.BoardOperation operation, bool reliable = true)
        {
            _inner.BroadcastOperation(operation, reliable);
        }

        public void PollEvents()
        {
            _inner.PollEvents();
        }

        public bool CanUndo => _inner.CanUndoLocal;
        public bool CanRedo => _inner.CanRedoLocal;
        public bool TryUndo() => _inner.TryUndoLocal();
        public bool TryRedo() => _inner.TryRedoLocal();

        public void Dispose()
        {
            _inner.Dispose();
        }
    }

    private sealed class ClientSessionAdapter : IGameClientSession
    {
        private readonly GameClient _inner;

        public ClientSessionAdapter(GameClient inner)
        {
            _inner = inner;
        }

        public event EventHandler? Connected
        {
            add => _inner.Connected += value;
            remove => _inner.Connected -= value;
        }

        public event EventHandler? Disconnected
        {
            add => _inner.Disconnected += value;
            remove => _inner.Disconnected -= value;
        }

        public event EventHandler<ClientOperationReceivedEventArgs>? OperationReceived
        {
            add => _inner.OperationReceived += value;
            remove => _inner.OperationReceived -= value;
        }

        public bool IsConnected => _inner.IsConnected;

        public void ConnectAsync(string hostAddress, int port = 7777)
        {
            var connectTask = _inner.ConnectAsync(hostAddress, port);
            _ = connectTask.ContinueWith(
                task => _ = task.Exception,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted);
        }

        public void RequestFullSync()
        {
            _inner.RequestFullSync();
        }

        public void SendOperation(BFGA.Network.Protocol.BoardOperation operation, bool reliable = true)
        {
            _inner.SendOperation(operation, reliable);
        }

        public void PollEvents()
        {
            _inner.PollEvents();
        }

        public void Dispose()
        {
            _inner.Dispose();
        }
    }
}
