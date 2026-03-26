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

        // Act
        var bytes = MessagePackSerializer.Serialize(operation);
        var deserialized = MessagePackSerializer.Deserialize<BoardOperation>(bytes);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<AddElementOperation>(deserialized);
        var result = (AddElementOperation)deserialized;
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

        // Act
        var bytes = MessagePackSerializer.Serialize(operation);
        var deserialized = MessagePackSerializer.Deserialize<BoardOperation>(bytes);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<UpdateElementOperation>(deserialized);
        var result = (UpdateElementOperation)deserialized;
        Assert.Equal(elementId, result.ElementId);
        Assert.Equal(2, result.ModifiedProperties.Count);
    }

    [Fact]
    public void DeleteElementOperation_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var elementId = Guid.NewGuid();
        var operation = new DeleteElementOperation(elementId);

        // Act
        var bytes = MessagePackSerializer.Serialize(operation);
        var deserialized = MessagePackSerializer.Deserialize<BoardOperation>(bytes);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<DeleteElementOperation>(deserialized);
        var result = (DeleteElementOperation)deserialized;
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

        // Act
        var bytes = MessagePackSerializer.Serialize(operation);
        var deserialized = MessagePackSerializer.Deserialize<BoardOperation>(bytes);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<MoveElementOperation>(deserialized);
        var result = (MoveElementOperation)deserialized;
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

        // Act
        var bytes = MessagePackSerializer.Serialize(operation);
        var deserialized = MessagePackSerializer.Deserialize<BoardOperation>(bytes);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<CursorUpdateOperation>(deserialized);
        var result = (CursorUpdateOperation)deserialized;
        Assert.Equal(clientId, result.SenderId);
        Assert.Equal(new Vector2(320, 240), result.Position);
    }

    [Fact]
    public void DrawStrokePointOperation_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var strokeId = Guid.NewGuid();
        var operation = new DrawStrokePointOperation(strokeId, new Vector2(75, 125));

        // Act
        var bytes = MessagePackSerializer.Serialize(operation);
        var deserialized = MessagePackSerializer.Deserialize<BoardOperation>(bytes);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<DrawStrokePointOperation>(deserialized);
        var result = (DrawStrokePointOperation)deserialized;
        Assert.Equal(strokeId, result.StrokeId);
        Assert.Equal(new Vector2(75, 125), result.Point);
    }

    [Fact]
    public void CancelStrokeOperation_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var strokeId = Guid.NewGuid();
        var operation = new CancelStrokeOperation(strokeId);

        // Act
        var bytes = MessagePackSerializer.Serialize(operation);
        var deserialized = MessagePackSerializer.Deserialize<BoardOperation>(bytes);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<CancelStrokeOperation>(deserialized);
        var result = (CancelStrokeOperation)deserialized;
        Assert.Equal(strokeId, result.StrokeId);
    }

    [Fact]
    public void RequestFullSyncOperation_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var operation = new RequestFullSyncOperation();

        // Act
        var bytes = MessagePackSerializer.Serialize(operation);
        var deserialized = MessagePackSerializer.Deserialize<BoardOperation>(bytes);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<RequestFullSyncOperation>(deserialized);
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

        // Act
        var bytes = MessagePackSerializer.Serialize(operation);
        var deserialized = MessagePackSerializer.Deserialize<BoardOperation>(bytes);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<FullSyncResponseOperation>(deserialized);
        var result = (FullSyncResponseOperation)deserialized;
        Assert.Equal(boardState.BoardId, result.BoardState.BoardId);
        Assert.Equal(boardState.BoardName, result.BoardState.BoardName);
        Assert.Single(result.BoardState.Elements);
        Assert.Equal(2, result.PlayerRoster.Count);
    }

    [Fact]
    public void PeerJoinedOperation_SerializeDeserialize_RoundTrip()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var operation = new PeerJoinedOperation(clientId, "TestPlayer", SKColors.Orange);

        // Act
        var bytes = MessagePackSerializer.Serialize(operation);
        var deserialized = MessagePackSerializer.Deserialize<BoardOperation>(bytes);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<PeerJoinedOperation>(deserialized);
        var result = (PeerJoinedOperation)deserialized;
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

        // Act
        var bytes = MessagePackSerializer.Serialize(operation);
        var deserialized = MessagePackSerializer.Deserialize<BoardOperation>(bytes);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<PeerLeftOperation>(deserialized);
        var result = (PeerLeftOperation)deserialized;
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
            new PeerLeftOperation(Guid.NewGuid())
        };

        // Act & Assert
        foreach (var operation in operations)
        {
            var bytes = MessagePackSerializer.Serialize(operation);
            var deserialized = MessagePackSerializer.Deserialize<BoardOperation>(bytes);
            Assert.NotNull(deserialized);
            Assert.Equal(operation.GetType(), deserialized.GetType());
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
            ShapeType = ShapeType.Ellipse,
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
