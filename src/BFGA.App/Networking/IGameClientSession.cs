using BFGA.Network;

namespace BFGA.App.Networking;

public interface IGameClientSession : IDisposable
{
    event EventHandler? Connected;
    event EventHandler? Disconnected;
    event EventHandler<ClientOperationReceivedEventArgs>? OperationReceived;

    bool IsConnected { get; }

    void ConnectAsync(string hostAddress, int port = 7777);
    void RequestFullSync();
    void SendOperation(BFGA.Network.Protocol.BoardOperation operation, bool reliable = true);
    void PollEvents();
}
