using System.Numerics;
using BFGA.Core;
using BFGA.Core.Models;
using BFGA.Network.Protocol;
using MessagePack;
using SkiaSharp;

namespace BFGA.Network.Tests;

public class ProtocolTests
{
    private readonly byte[] _serialized;

    public ProtocolTests()
    {
        MessagePackSerializer.DefaultOptions = MessagePackSetup.Options;
        _serialized = Array.Empty<byte>();
    }

    [Fact]
    public void AddElementOperation_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var element = new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 20),
            Size = new Vector2(100, 200),
            Rotation = 0f,
            ZIndex = 1,
            OwnerId = Guid.NewGuid(),
            IsLocked = false,
            Points = new List<Vector2> { new(0, 0), new(50, 50) },
            Color = SKColors.Red,
            Thickness = 2.5f
        };
        var operation = new AddElementOperation(element);

        // Act - wrap in NetworkMessage for proper polymorphic serialization
        var message = new NetworkMessage(operation);
        var bytes = MessagePackSerializer.Serialize(message, MessagePackSetup.Options);
        var restored = MessagePackSerializer.Deserialize<NetworkMessage>(bytes, MessagePackSetup.Options);

        // Assert
        Assert.NotNull(restored.Operation);
        Assert.IsType<AddElementOperation>(restored.Operation);
        var result = (AddElementOperation)restored.Operation;
        Assert.Equal(element.Id, result.Element.Id);
        Assert.Equal(element.Position, result.Element.Position);
        Assert.Equal(element.Size, result.Element.Size);
    }

    [Fact]
    public void UpdateElementOperation_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var elementId = Guid.NewGuid();
        var operation = new UpdateElementOperation(
            elementId,
            new Dictionary<string, object>
            {
                { "Position", new Vector2(100, 200) },
                { "IsLocked", true }
            });

        // Act - wrap in NetworkMessage for proper polymorphic serialization
        var message = new NetworkMessage(operation);
        var bytes = MessagePackSerializer.Serialize(message, MessagePackSetup.Options);
        var restored = MessagePackSerializer.Deserialize<NetworkMessage>(bytes, MessagePackSetup.Options);

        // Assert
        Assert.NotNull(restored.Operation);
        Assert.IsType<UpdateElementOperation>(restored.Operation);
        var result = (UpdateElementOperation)restored.Operation;
        Assert.Equal(elementId, result.ElementId);
        Assert.Equal(2, result.ModifiedProperties.Count);
        Assert.True(result.ModifiedProperties.TryGetValue("Position", out var position));
        // Vector2 in Dictionary<string, object> is serialized as an object array [x, y]
        var positionArray = Assert.IsType<object[]>(position);
        Assert.Equal(100f, (float)positionArray[0]);
        Assert.Equal(200f, (float)positionArray[1]);
        Assert.True(result.ModifiedProperties.TryGetValue("IsLocked", out var isLocked));
        Assert.Equal(true, isLocked);
    }

    [Fact]
    public void DeleteElementOperation_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var elementId = Guid.NewGuid();
        var operation = new DeleteElementOperation(elementId);

        // Act - wrap in NetworkMessage for proper polymorphic serialization
        var message = new NetworkMessage(operation);
        var bytes = MessagePackSerializer.Serialize(message, MessagePackSetup.Options);
        var restored = MessagePackSerializer.Deserialize<NetworkMessage>(bytes, MessagePackSetup.Options);

        // Assert
        Assert.NotNull(restored.Operation);
        Assert.IsType<DeleteElementOperation>(restored.Operation);
        var result = (DeleteElementOperation)restored.Operation;
        Assert.Equal(elementId, result.ElementId);
    }

    [Fact]
    public void MoveElementOperation_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var elementId = Guid.NewGuid();
        var operation = new MoveElementOperation(
            elementId,
            new Vector2(50, 100),
            new Vector2(200, 150),
            45f);

        // Act - wrap in NetworkMessage for proper polymorphic serialization
        var message = new NetworkMessage(operation);
        var bytes = MessagePackSerializer.Serialize(message, MessagePackSetup.Options);
        var restored = MessagePackSerializer.Deserialize<NetworkMessage>(bytes, MessagePackSetup.Options);

        // Assert
        Assert.NotNull(restored.Operation);
        Assert.IsType<MoveElementOperation>(restored.Operation);
        var result = (MoveElementOperation)restored.Operation;
        Assert.Equal(elementId, result.ElementId);
        Assert.Equal(new Vector2(50, 100), result.Position);
        Assert.Equal(new Vector2(200, 150), result.Size);
        Assert.Equal(45f, result.Rotation);
    }

    [Fact]
    public void CursorUpdateOperation_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var operation = new CursorUpdateOperation(clientId, new Vector2(320, 240));

        // Act - wrap in NetworkMessage for proper polymorphic serialization
        var message = new NetworkMessage(operation);
        var bytes = MessagePackSerializer.Serialize(message, MessagePackSetup.Options);
        var restored = MessagePackSerializer.Deserialize<NetworkMessage>(bytes, MessagePackSetup.Options);

        // Assert
        Assert.NotNull(restored.Operation);
        Assert.IsType<CursorUpdateOperation>(restored.Operation);
        var result = (CursorUpdateOperation)restored.Operation;
        Assert.Equal(clientId, result.SenderId);
        Assert.Equal(new Vector2(320, 240), result.Position);
    }

    [Fact]
    public void DrawStrokePointOperation_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var strokeId = Guid.NewGuid();
        var operation = new DrawStrokePointOperation(strokeId, new Vector2(75, 125));

        // Act - wrap in NetworkMessage for proper polymorphic serialization
        var message = new NetworkMessage(operation);
        var bytes = MessagePackSerializer.Serialize(message, MessagePackSetup.Options);
        var restored = MessagePackSerializer.Deserialize<NetworkMessage>(bytes, MessagePackSetup.Options);

        // Assert
        Assert.NotNull(restored.Operation);
        Assert.IsType<DrawStrokePointOperation>(restored.Operation);
        var result = (DrawStrokePointOperation)restored.Operation;
        Assert.Equal(strokeId, result.StrokeId);
        Assert.Equal(new Vector2(75, 125), result.Point);
    }

    [Fact]
    public void CancelStrokeOperation_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var strokeId = Guid.NewGuid();
        var operation = new CancelStrokeOperation(strokeId);

        // Act - wrap in NetworkMessage for proper polymorphic serialization
        var message = new NetworkMessage(operation);
        var bytes = MessagePackSerializer.Serialize(message, MessagePackSetup.Options);
        var restored = MessagePackSerializer.Deserialize<NetworkMessage>(bytes, MessagePackSetup.Options);

        // Assert
        Assert.NotNull(restored.Operation);
        Assert.IsType<CancelStrokeOperation>(restored.Operation);
        var result = (CancelStrokeOperation)restored.Operation;
        Assert.Equal(strokeId, result.StrokeId);
    }

    [Fact]
    public void RequestFullSyncOperation_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var operation = new RequestFullSyncOperation();

        // Act - wrap in NetworkMessage for proper polymorphic serialization
        var message = new NetworkMessage(operation);
        var bytes = MessagePackSerializer.Serialize(message, MessagePackSetup.Options);
        var restored = MessagePackSerializer.Deserialize<NetworkMessage>(bytes, MessagePackSetup.Options);

        // Assert
        Assert.NotNull(restored.Operation);
        Assert.IsType<RequestFullSyncOperation>(restored.Operation);
    }

    [Fact]
    public void FullSyncResponseOperation_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var boardState = new BoardState
        {
            BoardId = Guid.NewGuid(),
            BoardName = "Test Board",
            Elements = new List<BoardElement>
            {
                new StrokeElement
                {
                    Id = Guid.NewGuid(),
                    Position = new Vector2(0, 0),
                    Size = new Vector2(100, 100),
                    Points = new List<Vector2> { new(0, 0), new(50, 50) },
                    Color = SKColors.Blue,
                    Thickness = 3f
                }
            },
            LastModified = DateTime.UtcNow
        };

        var playerRoster = new Dictionary<Guid, PlayerInfo>
        {
            { Guid.NewGuid(), new PlayerInfo("Player1", SKColors.Red) },
            { Guid.NewGuid(), new PlayerInfo("Player2", SKColors.Green) }
        };

        var clientId = Guid.NewGuid();
        var operation = new FullSyncResponseOperation(clientId, boardState, playerRoster);

        // Act - wrap in NetworkMessage for proper polymorphic serialization
        var message = new NetworkMessage(operation);
        var bytes = MessagePackSerializer.Serialize(message, MessagePackSetup.Options);
        var restored = MessagePackSerializer.Deserialize<NetworkMessage>(bytes, MessagePackSetup.Options);

        // Assert
        Assert.NotNull(restored.Operation);
        Assert.IsType<FullSyncResponseOperation>(restored.Operation);
        var result = (FullSyncResponseOperation)restored.Operation;
        Assert.Equal(boardState.BoardId, result.BoardState.BoardId);
        Assert.Equal(boardState.BoardName, result.BoardState.BoardName);
        Assert.Single(result.BoardState.Elements);
        Assert.Equal(2, result.PlayerRoster.Count);
        // Verify specific element property round-trip
        var element = result.BoardState.Elements[0] as StrokeElement;
        Assert.NotNull(element);
        Assert.Equal(new Vector2(100, 100), element.Size);
        Assert.Equal(SKColors.Blue, element.Color);
    }

    [Fact]
    public void PeerJoinedOperation_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var operation = new PeerJoinedOperation(clientId, "TestPlayer", SKColors.Orange);

        // Act - wrap in NetworkMessage for proper polymorphic serialization
        var message = new NetworkMessage(operation);
        var bytes = MessagePackSerializer.Serialize(message, MessagePackSetup.Options);
        var restored = MessagePackSerializer.Deserialize<NetworkMessage>(bytes, MessagePackSetup.Options);

        // Assert
        Assert.NotNull(restored.Operation);
        Assert.IsType<PeerJoinedOperation>(restored.Operation);
        var result = (PeerJoinedOperation)restored.Operation;
        Assert.Equal(clientId, result.ClientId);
        Assert.Equal("TestPlayer", result.DisplayName);
        Assert.Equal(SKColors.Orange, result.AssignedColor);
    }

    [Fact]
    public void PeerLeftOperation_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var operation = new PeerLeftOperation(clientId);

        // Act - wrap in NetworkMessage for proper polymorphic serialization
        var message = new NetworkMessage(operation);
        var bytes = MessagePackSerializer.Serialize(message, MessagePackSetup.Options);
        var restored = MessagePackSerializer.Deserialize<NetworkMessage>(bytes, MessagePackSetup.Options);

        // Assert
        Assert.NotNull(restored.Operation);
        Assert.IsType<PeerLeftOperation>(restored.Operation);
        var result = (PeerLeftOperation)restored.Operation;
        Assert.Equal(clientId, result.ClientId);
    }

    [Fact]
    public void BoardOperation_PolymorphicRoundTrip_AllTypes()
    {
        // Arrange
        var operations = new BoardOperation[]
        {
            new AddElementOperation(new StrokeElement { Id = Guid.NewGuid() }),
            new UpdateElementOperation(Guid.NewGuid(), new Dictionary<string, object>()),
            new DeleteElementOperation(Guid.NewGuid()),
            new MoveElementOperation(Guid.NewGuid(), new Vector2(0, 0), new Vector2(100, 100), 0f),
            new CursorUpdateOperation(Guid.NewGuid(), new Vector2(0, 0)),
            new DrawStrokePointOperation(Guid.NewGuid(), new Vector2(0, 0)),
            new CancelStrokeOperation(Guid.NewGuid()),
            new RequestFullSyncOperation(),
            new FullSyncResponseOperation(Guid.NewGuid(), new BoardState(), new Dictionary<Guid, PlayerInfo>()),
            new PeerJoinedOperation(Guid.NewGuid(), "Player", SKColors.Red),
            new PeerLeftOperation(Guid.NewGuid()),
            new UndoOperation(),
            new RedoOperation()
        };

        // Act & Assert - wrap in NetworkMessage for proper polymorphic serialization
        foreach (var operation in operations)
        {
            var message = new NetworkMessage(operation);
            var bytes = MessagePackSerializer.Serialize(message, MessagePackSetup.Options);
            var restored = MessagePackSerializer.Deserialize<NetworkMessage>(bytes, MessagePackSetup.Options);
            Assert.NotNull(restored.Operation);
            Assert.Equal(operation.GetType(), restored.Operation.GetType());
        }
    }

    [Fact]
    public void OperationSerializer_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var element = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 20),
            Size = new Vector2(100, 50),
            Rotation = 0f,
            ZIndex = 1,
            OwnerId = Guid.NewGuid(),
            IsLocked = false,
            Type = ShapeType.Ellipse,
            FillColor = SKColors.Yellow,
            StrokeColor = SKColors.Black,
            StrokeWidth = 1f
        };
        var operation = new AddElementOperation(element);

        // Act
        var bytes = OperationSerializer.Serialize(operation);
        var deserialized = OperationSerializer.Deserialize(bytes);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<AddElementOperation>(deserialized);
        var result = (AddElementOperation)deserialized;
        Assert.Equal(element.Id, result.Element.Id);
    }
}
