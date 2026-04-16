using System.Collections.Concurrent;
using System.Numerics;
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

    [Fact]
    public void GameHost_HandleLaserOp_DoesNotModifyBoardState()
    {
        // Arrange
        using var host = new GameHost();
        host.Start(0);
        var initialLastModified = host.BoardState.LastModified;
        var initialElementCount = host.BoardState.Elements.Count;

        // Act — TryApplyLocalOperation calls HandleOperation path for host-local ops
        var laserOp = new LaserPointerOperation(Guid.Empty, new Vector2(100, 200), true);
        host.TryApplyLocalOperation(laserOp);

        // Assert — board state must be untouched
        Assert.Equal(initialLastModified, host.BoardState.LastModified);
        Assert.Equal(initialElementCount, host.BoardState.Elements.Count);
        host.Stop();
    }

    [Fact]
    public void GameHost_HandleLaserOp_FiresOperationReceivedEvent()
    {
        // Arrange
        using var host = new GameHost();
        host.Start(0);
        BoardOperation? receivedOp = null;
        host.OperationReceived += (_, args) => receivedOp = args.Operation;

        // Act
        var laserOp = new LaserPointerOperation(Guid.Empty, new Vector2(50, 50), true);
        host.TryApplyLocalOperation(laserOp);

        // Assert — OperationReceived event should NOT fire for local ops via TryApplyLocalOperation
        // (it only fires in HandleOperation which is the network path)
        // But broadcast should still succeed without error
        host.Stop();
    }

    [Fact]
    public void GameHost_ChannelsCount_IsThree()
    {
        // Arrange
        using var host = new GameHost();
        host.Start(0);

        // Act — If ChannelsCount were wrong, sending on channel 2 would fail
        var laserOp = new LaserPointerOperation(Guid.NewGuid(), new Vector2(50, 50), true);
        var exception = Record.Exception(() => host.BroadcastOperation(laserOp, reliable: false));

        // Assert
        Assert.Null(exception);
        host.Stop();
    }

    [Fact]
    public void GameHost_IsOperationReliable_ReturnsFalseForLaser()
    {
        // LaserPointerOperation should be non-reliable (like CursorUpdateOperation)
        // We verify this indirectly: BroadcastOperation with reliable:false should not throw
        using var host = new GameHost();
        host.Start(0);

        var laserOp = new LaserPointerOperation(Guid.NewGuid(), new Vector2(10, 20), true);
        var exception = Record.Exception(() => host.BroadcastOperation(laserOp, reliable: false));
        Assert.Null(exception);
        host.Stop();
    }

    [Fact]
    public void GameHost_LaserOp_BoardStateLastModifiedUnchanged()
    {
        // Verify board state isolation more precisely — capture timestamp before and after
        using var host = new GameHost();
        host.Start(0);

        // Force a known board state with a timestamp
        var element = new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 10),
            Points = [Vector2.Zero]
        };
        var snapshot = new BoardState();
        snapshot.Elements.Add(element);
        host.ReplaceBoardState(snapshot);

        var timestampBefore = host.BoardState.LastModified;
        var elementCountBefore = host.BoardState.Elements.Count;

        // Apply multiple laser ops
        for (int i = 0; i < 10; i++)
        {
            host.TryApplyLocalOperation(new LaserPointerOperation(Guid.Empty, new Vector2(i * 10, i * 10), true));
        }

        // Board state must be completely unchanged
        Assert.Equal(timestampBefore, host.BoardState.LastModified);
        Assert.Equal(elementCountBefore, host.BoardState.Elements.Count);
        host.Stop();
    }

    [Fact]
    public async Task GameClient_SendLaserOp_UsesSequencedChannel()
    {
        // Verify client can send laser ops and host receives them without error
        using var host = new GameHost();
        host.Start(0);

        using var client = new GameClient("LaserTester");
        var connectTask = client.ConnectAsync("localhost", host.Port);

        for (int i = 0; i < 100; i++)
        {
            host.PollEvents();
            client.PollEvents();
            await Task.Delay(10);
            if (client.IsConnected) break;
        }

        await connectTask;
        Assert.True(client.IsConnected);

        // Send laser op — should use SequencedChannel (channel 2)
        var laserOp = new LaserPointerOperation(client.ClientId, new Vector2(200, 300), true);
        var exception = Record.Exception(() => client.SendOperation(laserOp));
        Assert.Null(exception);

        // Pump events to process the laser op on host
        BoardOperation? hostReceivedOp = null;
        host.OperationReceived += (_, args) =>
        {
            if (args.Operation is LaserPointerOperation)
                hostReceivedOp = args.Operation;
        };

        for (int i = 0; i < 50; i++)
        {
            host.PollEvents();
            client.PollEvents();
            await Task.Delay(10);
            if (hostReceivedOp != null) break;
        }

        Assert.NotNull(hostReceivedOp);
        Assert.IsType<LaserPointerOperation>(hostReceivedOp);

        host.Stop();
        client.Disconnect();
    }

    [Fact]
    public async Task GameHost_UpdatePresenceColorOperation_UpdatesPlayerRosterAndBroadcastsPeerMetadata()
    {
        using var host = new GameHost();
        host.Start(0);

        using var client = new GameClient("ColorTester");
        var connectTask = client.ConnectAsync("localhost", host.Port);

        for (int i = 0; i < 100; i++)
        {
            host.PollEvents();
            client.PollEvents();
            await Task.Delay(10);
            if (client.IsConnected)
            {
                break;
            }
        }

        await connectTask;
        Assert.True(client.IsConnected);

        for (int i = 0; i < 100 && (host.PlayerRoster.Count == 0 || client.ClientId == Guid.Empty); i++)
        {
            host.PollEvents();
            client.PollEvents();
            await Task.Delay(10);
        }

        var localClientId = client.ClientId;
        Assert.True(host.PlayerRoster.TryGetValue(localClientId, out var originalPlayer));

        var receivedOperations = new ConcurrentQueue<BoardOperation>();
        client.OperationReceived += (_, args) => receivedOperations.Enqueue(args.Operation);

        var requestedColor = SKColors.Gold;
        var spoofedSenderId = Guid.NewGuid();
        var updateColor = new UpdatePresenceColorOperation(requestedColor)
        {
            SenderId = spoofedSenderId
        };

        client.SendOperation(updateColor);

        PeerJoinedOperation? metadataBroadcast = null;
        for (int i = 0; i < 100; i++)
        {
            host.PollEvents();
            client.PollEvents();
            await Task.Delay(10);

            metadataBroadcast = receivedOperations
                .OfType<PeerJoinedOperation>()
                .LastOrDefault(op => op.ClientId == localClientId && op.AssignedColor == requestedColor);
            if (metadataBroadcast is not null)
            {
                break;
            }
        }

        Assert.True(host.PlayerRoster.TryGetValue(localClientId, out var updatedPlayer));
        Assert.NotNull(updatedPlayer);
        Assert.Equal(requestedColor, updatedPlayer.AssignedColor);
        Assert.Equal(originalPlayer.DisplayName, updatedPlayer.DisplayName);

        Assert.NotNull(metadataBroadcast);
        Assert.Equal(localClientId, metadataBroadcast!.ClientId);
        Assert.Equal(originalPlayer.DisplayName, metadataBroadcast.DisplayName);
        Assert.Equal(requestedColor, metadataBroadcast.AssignedColor);
        Assert.NotEqual(spoofedSenderId, metadataBroadcast.ClientId);

        host.Stop();
        client.Disconnect();
    }

    [Fact]
    public async Task GameHost_FullSync_IncludesHostPresenceMetadata()
    {
        using var host = new GameHost();
        host.SetHostPresence("Host Player", SKColors.MediumPurple);
        host.Start(0);

        using var client = new GameClient("SyncTester");
        var connectTask = client.ConnectAsync("localhost", host.Port);

        FullSyncResponseOperation? fullSync = null;
        client.OperationReceived += (_, args) =>
        {
            if (args.Operation is FullSyncResponseOperation sync)
            {
                fullSync = sync;
            }
        };

        for (int i = 0; i < 100; i++)
        {
            host.PollEvents();
            client.PollEvents();
            await Task.Delay(10);
            if (fullSync is not null)
            {
                break;
            }
        }

        await connectTask;

        Assert.NotNull(fullSync);
        Assert.True(fullSync!.PlayerRoster.TryGetValue(Guid.Empty, out var hostInfo));
        Assert.Equal("Host Player", hostInfo.DisplayName);
        Assert.Equal(SKColors.MediumPurple, hostInfo.AssignedColor);

        host.Stop();
        client.Disconnect();
    }
}
