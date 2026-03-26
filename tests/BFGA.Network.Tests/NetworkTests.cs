using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using BFGA.Core.Models;
using BFGA.Network;
using BFGA.Network.Protocol;
using SkiaSharp;

namespace BFGA.Network.Tests;

public class NetworkTests
{
    [Fact]
    public void PlayerInfo_Properties_SetCorrectly()
    {
        // Arrange & Act
        var info = new PlayerInfo("TestPlayer", SKColors.Blue);

        // Assert
        Assert.Equal("TestPlayer", info.DisplayName);
        Assert.Equal(SKColors.Blue, info.AssignedColor);
    }

    [Fact]
    public void GameHost_StartStop_NoException()
    {
        // Arrange
        using var host = new GameHost();

        // Act & Assert - Should not throw
        host.Start(7778);
        Assert.True(host.IsRunning);
        host.Stop();
        Assert.False(host.IsRunning);
    }

    [Fact]
    public void GameHost_PlayerRoster_InitiallyEmpty()
    {
        // Arrange
        using var host = new GameHost();
        host.Start(7779);

        // Act
        var roster = host.GetPlayerRoster();

        // Assert
        Assert.Empty(roster);

        host.Stop();
    }

    [Fact]
    public void GameClient_Create_NotConnected()
    {
        // Arrange & Act
        using var client = new GameClient("TestPlayer");

        // Assert
        Assert.False(client.IsConnected);
        Assert.Equal("TestPlayer", client.DisplayName);
    }

    [Fact]
    public async Task GameHost_ClientConnect_PlayerJoinedBroadcast()
    {
        // Arrange
        using var host = new GameHost();
        host.Start(7780);

        using var client = new GameClient("TestPlayer");
        var joinedEvent = new ManualResetEventSlim(false);
        Guid? receivedClientId = null;

        host.PeerJoined += (sender, args) =>
        {
            receivedClientId = args.ClientId;
            joinedEvent.Set();
        };

        // Act - Start connection
        _ = client.ConnectAsync("localhost", 7780);
        
        // Pump events to establish connection and trigger PeerJoined
        for (int i = 0; i < 100; i++)
        {
            host.PollEvents();
            client.PollEvents();
            await Task.Delay(10);
            if (joinedEvent.IsSet) break;
        }

        // Assert
        Assert.True(client.IsConnected);
        Assert.NotNull(receivedClientId);

        host.Stop();
        client.Disconnect();
    }

    [Fact]
    public async Task GameClient_SendReceiveOperation_RoundTrip()
    {
        // Arrange
        using var host = new GameHost();
        host.Start(7771);

        using var client = new GameClient("HostPlayer");
        var connectTask = client.ConnectAsync("localhost", 7771);
        
        // Pump events to establish connection
        for (int i = 0; i < 100; i++)
        {
            host.PollEvents();
            client.PollEvents();
            await Task.Delay(10);
            if (client.IsConnected) break;
        }

        Assert.True(client.IsConnected);

        var operationReceived = new ManualResetEventSlim(false);
        BoardOperation? receivedOp = null;
        client.OperationReceived += (sender, args) =>
        {
            receivedOp = args.Operation;
            operationReceived.Set();
        };

        // Act
        var addOp = new AddElementOperation(new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = new System.Numerics.Vector2(100, 200)
        });
        client.SendOperation(addOp);

        // Pump events to deliver the operation
        for (int i = 0; i < 50; i++)
        {
            host.PollEvents();
            client.PollEvents();
            await Task.Delay(10);
        }

        // Assert
        var received = operationReceived.Wait(1000);
        
        host.Stop();
        client.Disconnect();

        Assert.True(received);
        Assert.NotNull(receivedOp);
    }

    [Fact]
    public void GameHost_BroadcastOperation_DoesNotThrowWhenNoClients()
    {
        // This test validates broadcast doesn't throw when no clients are connected
        
        // Arrange
        using var host = new GameHost();
        host.Start(7772);

        // Act & Assert - Broadcast should not throw even with no clients
        var operation = new CursorUpdateOperation(
            Guid.NewGuid(),
            new System.Numerics.Vector2(100, 200));
        
        var exception = Record.Exception(() => host.BroadcastOperation(operation, reliable: false));
        
        host.Stop();

        Assert.Null(exception);
    }
}
