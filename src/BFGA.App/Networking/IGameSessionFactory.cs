namespace BFGA.App.Networking;

public interface IGameSessionFactory
{
    IGameHostSession CreateHost();
    IGameClientSession CreateClient(string displayName);
}
