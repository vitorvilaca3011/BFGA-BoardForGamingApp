using System.Numerics;
using BFGA.Core;
using BFGA.Core.Models;
using MessagePack;
using SkiaSharp;

namespace BFGA.Core.Tests;

public class SerializationTests
{
    private static readonly MessagePackSerializerOptions Options = MessagePackSetup.Options;

    [Fact]
    public void StrokeElement_RoundTrip_PreservesData()
    {
        var original = new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 20),
            Size = new Vector2(100, 50),
            Rotation = 0f,
            ZIndex = 1,
            OwnerId = Guid.NewGuid(),
            IsLocked = false,
            Points = new List<Vector2> { new(0, 0), new(50, 50), new(100, 100) },
            Color = SKColors.Red,
            Thickness = 2.5f
        };

        var bytes = MessagePackSerializer.Serialize(original, Options);
        var restored = MessagePackSerializer.Deserialize<StrokeElement>(bytes, Options);

        Assert.NotNull(restored);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Position, restored.Position);
        Assert.Equal(original.Size, restored.Size);
        Assert.Equal(original.Points.Count, restored.Points.Count);
        Assert.Equal(original.Color, restored.Color);
        Assert.Equal(original.Thickness, restored.Thickness);
    }

    [Fact]
    public void ShapeElement_RoundTrip_PreservesData()
    {
        var original = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 100),
            Rotation = 0f,
            ZIndex = 2,
            OwnerId = Guid.NewGuid(),
            IsLocked = false,
            Type = ShapeType.Ellipse,
            StrokeColor = SKColors.Blue,
            FillColor = SKColors.Transparent,
            StrokeWidth = 1.5f
        };

        var bytes = MessagePackSerializer.Serialize(original, Options);
        var restored = MessagePackSerializer.Deserialize<ShapeElement>(bytes, Options);

        Assert.NotNull(restored);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Type, restored.Type);
        Assert.Equal(original.StrokeColor, restored.StrokeColor);
        Assert.Equal(original.FillColor, restored.FillColor);
        Assert.Equal(original.StrokeWidth, restored.StrokeWidth);
    }

    [Fact]
    public void ImageElement_RoundTrip_PreservesEmbeddedBytes()
    {
        var original = new ImageElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(200, 150),
            Rotation = 0f,
            ZIndex = 3,
            OwnerId = Guid.NewGuid(),
            IsLocked = false,
            ImageData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            OriginalFileName = "test.png"
        };

        var bytes = MessagePackSerializer.Serialize(original, Options);
        var restored = MessagePackSerializer.Deserialize<ImageElement>(bytes, Options);

        Assert.NotNull(restored);
        Assert.Equal(original.ImageData.Length, restored.ImageData.Length);
        Assert.Equal(original.ImageData, restored.ImageData);
        Assert.Equal(original.OriginalFileName, restored.OriginalFileName);
    }

    [Fact]
    public void TextElement_RoundTrip_PreservesData()
    {
        var original = new TextElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(50, 50),
            Size = new Vector2(100, 30),
            Rotation = 0f,
            ZIndex = 4,
            OwnerId = Guid.NewGuid(),
            IsLocked = false,
            Text = "Hello World",
            FontSize = 14f,
            Color = SKColors.Black,
            FontFamily = "Arial"
        };

        var bytes = MessagePackSerializer.Serialize(original, Options);
        var restored = MessagePackSerializer.Deserialize<TextElement>(bytes, Options);

        Assert.NotNull(restored);
        Assert.Equal(original.Text, restored.Text);
        Assert.Equal(original.FontSize, restored.FontSize);
        Assert.Equal(original.Color, restored.Color);
        Assert.Equal(original.FontFamily, restored.FontFamily);
    }

    [Fact]
    public void BoardState_RoundTrip_PreservesAllElements()
    {
        var boardId = Guid.NewGuid();
        var lastModified = DateTime.UtcNow;
        var strokeId = Guid.NewGuid();
        var shapeId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        var testImageData = new byte[] { 0x89, 0x50 };

        var board = new BoardState
        {
            BoardId = boardId,
            BoardName = "Test Board",
            LastModified = lastModified
        };

        board.Elements.Add(new StrokeElement
        {
            Id = strokeId,
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 50),
            Points = new List<Vector2> { new(0, 0), new(50, 50) },
            Color = SKColors.Red,
            Thickness = 2f
        });

        board.Elements.Add(new ShapeElement
        {
            Id = shapeId,
            Position = new Vector2(100, 0),
            Size = new Vector2(100, 100),
            Type = ShapeType.Rectangle,
            StrokeColor = SKColors.Blue,
            FillColor = SKColors.Transparent,
            StrokeWidth = 1f
        });

        board.Elements.Add(new ImageElement
        {
            Id = imageId,
            Position = new Vector2(0, 100),
            Size = new Vector2(200, 150),
            ImageData = testImageData,
            OriginalFileName = "image.png"
        });

        var bytes = MessagePackSerializer.Serialize(board, Options);
        var restored = MessagePackSerializer.Deserialize<BoardState>(bytes, Options);

        Assert.NotNull(restored);
        Assert.Equal(boardId, restored.BoardId);
        Assert.Equal("Test Board", restored.BoardName);
        Assert.Equal(3, restored.Elements.Count);

        // Verify StrokeElement polymorphic type and properties
        var restoredStroke = Assert.IsType<StrokeElement>(restored.Elements[0]);
        Assert.Equal(strokeId, restoredStroke.Id);
        Assert.Equal(2, restoredStroke.Points.Count);
        Assert.Equal(SKColors.Red, restoredStroke.Color);
        Assert.Equal(2f, restoredStroke.Thickness);

        // Verify ShapeElement polymorphic type and properties
        var restoredShape = Assert.IsType<ShapeElement>(restored.Elements[1]);
        Assert.Equal(shapeId, restoredShape.Id);
        Assert.Equal(ShapeType.Rectangle, restoredShape.Type);
        Assert.Equal(SKColors.Blue, restoredShape.StrokeColor);
        Assert.Equal(SKColors.Transparent, restoredShape.FillColor);
        Assert.Equal(1f, restoredShape.StrokeWidth);

        // Verify ImageElement polymorphic type and properties
        var restoredImage = Assert.IsType<ImageElement>(restored.Elements[2]);
        Assert.Equal(imageId, restoredImage.Id);
        Assert.Equal(testImageData.Length, restoredImage.ImageData.Length);
        Assert.Equal(testImageData, restoredImage.ImageData);
        Assert.Equal("image.png", restoredImage.OriginalFileName);
    }

    [Fact]
    public void BoardState_RoundTrip_PreservesTextElement_Polymorphic()
    {
        var boardId = Guid.NewGuid();
        var textElementId = Guid.NewGuid();

        var board = new BoardState
        {
            BoardId = boardId,
            BoardName = "Text Test Board"
        };

        board.Elements.Add(new TextElement
        {
            Id = textElementId,
            Position = new Vector2(50, 50),
            Size = new Vector2(100, 30),
            Rotation = 0f,
            ZIndex = 4,
            OwnerId = Guid.NewGuid(),
            IsLocked = false,
            Text = "Hello World",
            FontSize = 14f,
            Color = SKColors.Black,
            FontFamily = "Arial"
        });

        var bytes = MessagePackSerializer.Serialize(board, Options);
        var restored = MessagePackSerializer.Deserialize<BoardState>(bytes, Options);

        Assert.NotNull(restored);
        Assert.Single(restored.Elements);

        var restoredText = Assert.IsType<TextElement>(restored.Elements[0]);
        Assert.Equal(textElementId, restoredText.Id);
        Assert.Equal("Hello World", restoredText.Text);
        Assert.Equal(14f, restoredText.FontSize);
        Assert.Equal(SKColors.Black, restoredText.Color);
        Assert.Equal("Arial", restoredText.FontFamily);
    }

    [Fact]
    public void BoardState_RoundTrip_PreservesLargeImageBytes()
    {
        var board = new BoardState
        {
            BoardId = Guid.NewGuid(),
            BoardName = "Image Test"
        };

        // Create a realistic 1MB image
        var largeImageData = new byte[1024 * 1024];
        new Random(42).NextBytes(largeImageData);
        largeImageData[0] = 0x89;
        largeImageData[1] = 0x50; // PNG header
        largeImageData[2] = 0x4E;
        largeImageData[3] = 0x47;

        board.Elements.Add(new ImageElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(1920, 1080),
            ImageData = largeImageData,
            OriginalFileName = "large_image.png"
        });

        var bytes = MessagePackSerializer.Serialize(board, Options);
        var restored = MessagePackSerializer.Deserialize<BoardState>(bytes, Options);

        Assert.NotNull(restored);
        var restoredImage = (ImageElement)restored.Elements[0];
        Assert.Equal(largeImageData.Length, restoredImage.ImageData.Length);
        Assert.Equal(largeImageData, restoredImage.ImageData);
    }
}
