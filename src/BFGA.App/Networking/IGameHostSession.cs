using BFGA.Core;
using BFGA.Network;

namespace BFGA.App.Networking;

public interface IGameHostSession : IDisposable
{
    event EventHandler<PeerJoinedEventArgs>? PeerJoined;
    event EventHandler<PeerLeftEventArgs>? PeerLeft;

    bool IsRunning { get; }
    int Port { get; }
    BoardState BoardState { get; }

    void Start(int port = 7777);
    void ReplaceBoardState(BoardState snapshot);
    bool TryApplyLocalOperation(BFGA.Network.Protocol.BoardOperation operation);
    void SyncAllClients();
    void BroadcastOperation(BFGA.Network.Protocol.BoardOperation operation, bool reliable = true);
    void BroadcastFullSync();
    void PollEvents();
}
