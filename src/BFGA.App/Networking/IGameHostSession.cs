using BFGA.Core;
using BFGA.Network;
using SkiaSharp;

namespace BFGA.App.Networking;

public interface IGameHostSession : IDisposable
{
    event EventHandler<OperationReceivedEventArgs>? OperationReceived;
    event EventHandler<PeerJoinedEventArgs>? PeerJoined;
    event EventHandler<PeerLeftEventArgs>? PeerLeft;

    bool IsRunning { get; }
    int Port { get; }
    BoardState BoardState { get; }

    void Start(int port = 7777);
    void SetHostPresence(string displayName, SKColor assignedColor);
    void ReplaceBoardState(BoardState snapshot);
    bool TryApplyLocalOperation(BFGA.Network.Protocol.BoardOperation operation);
    void SyncAllClients();
    void BroadcastOperation(BFGA.Network.Protocol.BoardOperation operation, bool reliable = true);
    void BroadcastFullSync();
    void PollEvents();

    bool CanUndo { get; }
    bool CanRedo { get; }
    bool TryUndo();
    bool TryRedo();
}
