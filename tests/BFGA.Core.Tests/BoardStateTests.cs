using System.Numerics;
using BFGA.Core.Models;
using SkiaSharp;

namespace BFGA.Core.Tests;

public class BoardStateTests
{
    [Fact]
    public void BoardState_CreatesWithDefaultValues()
    {
        var board = new BoardState();

        Assert.NotEqual(Guid.Empty, board.BoardId);
        Assert.Equal("Untitled", board.BoardName);
        Assert.NotNull(board.Elements);
        Assert.Empty(board.Elements);
    }

    [Fact]
    public void BoardState_CanSetProperties()
    {
        var boardId = Guid.NewGuid();
        var board = new BoardState
        {
            BoardId = boardId,
            BoardName = "Test Board",
            LastModified = DateTime.UtcNow
        };

        Assert.Equal(boardId, board.BoardId);
        Assert.Equal("Test Board", board.BoardName);
    }

    [Fact]
    public void StrokeElement_CanBeAddedToBoard()
    {
        var board = new BoardState();
        var stroke = new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 20),
            Size = new Vector2(100, 50),
            Points = new List<Vector2> { new(0, 0), new(50, 50), new(100, 0) },
            Color = SKColors.Red,
            Thickness = 2f
        };

        board.Elements.Add(stroke);

        Assert.Single(board.Elements);
        Assert.Equal(3, ((StrokeElement)board.Elements[0]).Points.Count);
    }

    [Fact]
    public void ShapeElement_CanBeAddedToBoard()
    {
        var board = new BoardState();
        var shape = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 100),
            Type = ShapeType.Rectangle,
            StrokeColor = SKColors.Blue,
            FillColor = SKColors.Green,
            StrokeWidth = 1.5f
        };

        board.Elements.Add(shape);

        Assert.Single(board.Elements);
        Assert.Equal(ShapeType.Rectangle, ((ShapeElement)board.Elements[0]).Type);
    }

    [Fact]
    public void ImageElement_CanBeAddedToBoard()
    {
        var board = new BoardState();
        var imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        var image = new ImageElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(200, 150),
            ImageData = imageData,
            OriginalFileName = "test.png"
        };

        board.Elements.Add(image);

        Assert.Single(board.Elements);
        Assert.Equal("test.png", ((ImageElement)board.Elements[0]).OriginalFileName);
        Assert.Equal(4, ((ImageElement)board.Elements[0]).ImageData.Length);
    }

    [Fact]
    public void TextElement_CanBeAddedToBoard()
    {
        var board = new BoardState();
        var text = new TextElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(50, 50),
            Size = new Vector2(100, 30),
            Text = "Hello World",
            FontSize = 14f,
            Color = SKColors.Black,
            FontFamily = "Arial"
        };

        board.Elements.Add(text);

        Assert.Single(board.Elements);
        Assert.Equal("Hello World", ((TextElement)board.Elements[0]).Text);
    }

    [Fact]
    public void BoardElement_CanSetBaseProperties()
    {
        var element = new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 20),
            Size = new Vector2(100, 50),
            Rotation = 45f,
            ZIndex = 5,
            OwnerId = Guid.NewGuid(),
            IsLocked = true
        };

        Assert.Equal(new Vector2(10, 20), element.Position);
        Assert.Equal(45f, element.Rotation);
        Assert.Equal(5, element.ZIndex);
        Assert.True(element.IsLocked);
    }
}
