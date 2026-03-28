using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using BFGA.Core;
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

        // Act & Assert - Should not throw (port 0 lets OS assign a free port)
        host.Start(0);
        Assert.True(host.IsRunning);
        Assert.True(host.Port > 0);
        host.Stop();
        Assert.False(host.IsRunning);
    }

    [Fact]
    public void GameHost_PlayerRoster_InitiallyEmpty()
    {
        // Arrange
        using var host = new GameHost();
        host.Start(0);

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
        host.Start(0);
        int hostPort = host.Port;

        using var client = new GameClient("TestPlayer");
        var joinedEvent = new ManualResetEventSlim(false);
        Guid? receivedClientId = null;

        host.PeerJoined += (sender, args) =>
        {
            receivedClientId = args.ClientId;
            joinedEvent.Set();
        };

        // Act - Start connection
        var connectTask = client.ConnectAsync("localhost", hostPort);
        Assert.False(connectTask.IsCompleted);
        
        // Pump events to establish connection and trigger PeerJoined
        for (int i = 0; i < 100; i++)
        {
            host.PollEvents();
            client.PollEvents();
            await Task.Delay(10);
            if (joinedEvent.IsSet) break;
        }

        await connectTask;

        // Assert
        Assert.True(client.IsConnected);
        Assert.NotNull(receivedClientId);
        Assert.NotEqual(Guid.Empty, receivedClientId);
        Assert.True(host.PlayerRoster.Count > 0, "Host should have at least one player in roster");

        host.Stop();
        client.Disconnect();
    }

    [Fact]
    public async Task GameClient_SendReceiveOperation_RoundTrip()
    {
        // Arrange
        using var host = new GameHost();
        host.Start(0);
        int hostPort = host.Port;
        
        // Track host receiving operations
        BoardOperation? hostReceivedOp = null;
        host.OperationReceived += (sender, args) =>
        {
            hostReceivedOp = args.Operation;
        };

        using var client = new GameClient("HostPlayer");
        
        // Start connection (ConnectAsync polls client events internally)
        var connectTask = client.ConnectAsync("localhost", hostPort);
        
        // Pump events on both host and client until connected
        // Note: ConnectAsync polls client events, but we also need to poll host events
        for (int i = 0; i < 100; i++)
        {
            host.PollEvents();
            // client.PollEvents() is called internally by ConnectAsync, but we call it here too for safety
            client.PollEvents();
            await Task.Delay(10);
            if (client.IsConnected) break;
        }

        await connectTask;

        Assert.True(client.IsConnected, "Client should be connected");

        for (int i = 0; i < 100 && host.PlayerRoster.Count == 0; i++)
        {
            host.PollEvents();
            client.PollEvents();
            await Task.Delay(10);
        }

        Assert.True(host.PlayerRoster.Count > 0, "Host should have at least one player in roster");

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

        Assert.True(hostReceivedOp != null, "Host should have received the operation");
        Assert.True(received, "Client should have received the broadcast");
        Assert.NotNull(receivedOp);
        // Verify it's the same AddElementOperation with the same element
        Assert.IsType<AddElementOperation>(receivedOp);
        var receivedAddOp = (AddElementOperation)receivedOp!;
        Assert.Equal(addOp.Element.Id, receivedAddOp.Element.Id);
        Assert.Equal(addOp.Element.Position, receivedAddOp.Element.Position);
    }

    [Fact]
    public async Task GameClient_ConnectAsync_ReusesInFlightAttempt()
    {
        using var host = new GameHost();
        host.Start(0);

        using var client = new GameClient("TestPlayer");

        var firstConnectTask = client.ConnectAsync("localhost", host.Port);
        var secondConnectTask = client.ConnectAsync("localhost", host.Port);

        Assert.Same(firstConnectTask, secondConnectTask);

        for (int i = 0; i < 100; i++)
        {
            host.PollEvents();
            client.PollEvents();
            await Task.Delay(10);
            if (client.IsConnected) break;
        }

        await firstConnectTask;
        Assert.True(client.IsConnected);

        host.Stop();
        client.Disconnect();
    }

    [Fact]
    public async Task GameClient_ConnectAsync_FailedAttemptCanBeRetried()
    {
        using var host = new GameHost();
        host.Start(0);

        using var client = new GameClient("TestPlayer");

        var firstConnectTask = client.ConnectAsync("localhost", host.Port);
        client.Disconnect();

        await Assert.ThrowsAsync<InvalidOperationException>(() => firstConnectTask);

        var secondConnectTask = client.ConnectAsync("localhost", host.Port);

        for (int i = 0; i < 100; i++)
        {
            host.PollEvents();
            client.PollEvents();
            await Task.Delay(10);
            if (client.IsConnected) break;
        }

        await secondConnectTask;
        Assert.True(client.IsConnected);

        host.Stop();
        client.Disconnect();
    }

    [Fact]
    public void GameHost_BroadcastOperation_DoesNotThrowWhenNoClients()
    {
        // This test validates broadcast doesn't throw when no clients are connected
        
        // Arrange
        using var host = new GameHost();
        host.Start(0);

        // Act & Assert - Broadcast should not throw even with no clients
        var operation = new CursorUpdateOperation(
            Guid.NewGuid(),
            new System.Numerics.Vector2(100, 200));
        
        var exception = Record.Exception(() => host.BroadcastOperation(operation, reliable: false));
        
        host.Stop();

        Assert.Null(exception);
    }

    [Fact]
    public void GameHost_ReplaceBoardState_RebuildsAuthoritativeIndex()
    {
        using var host = new GameHost();
        host.Start(0);

        var element = new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = new System.Numerics.Vector2(25, 40),
            Points = [System.Numerics.Vector2.Zero]
        };
        var snapshot = new BoardState();
        snapshot.Elements.Add(element);

        host.ReplaceBoardState(snapshot);

        var applied = host.TryApplyLocalOperation(new DeleteElementOperation(element.Id));

        Assert.True(applied);
        Assert.Empty(host.BoardState.Elements);
    }
}
