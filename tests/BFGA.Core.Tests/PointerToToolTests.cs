using System.Numerics;
using BFGA.Canvas;
using BFGA.Canvas.Rendering;
using BFGA.Canvas.Tools;
using BFGA.Core;
using BFGA.Core.Models;
using BFGA.Network.Protocol;

namespace BFGA.Core.Tests;

public class PointerToToolTests
{
    [Fact]
    public void BoardViewport_ConvertsScreenPointsUsingPan()
    {
        var viewport = new BoardViewport();

        viewport.Pan = new Vector2(100f, 50f);
        var boardPoint = viewport.ScreenToBoard(new Avalonia.Point(125f, 40f));

        Assert.Equal(new Vector2(25f, -10f), boardPoint);
    }

    [Theory]
    [InlineData(BoardToolType.Rectangle, ShapeType.Rectangle)]
    [InlineData(BoardToolType.Ellipse, ShapeType.Ellipse)]
    public void ShapeTools_PublishMatchingShapeType(BoardToolType tool, ShapeType expectedType)
    {
        var board = new BoardState();
        var controller = new BoardToolController(board);

        controller.SetTool(tool);
        controller.ShapeType = expectedType;

        controller.HandlePointerDown(new Vector2(10f, 10f));
        controller.HandlePointerMove(new Vector2(25f, 30f));
        var result = controller.HandlePointerUp(new Vector2(25f, 30f));

        var operation = Assert.Single(result.Operations);
        var add = Assert.IsType<AddElementOperation>(operation);
        var shape = Assert.IsType<ShapeElement>(add.Element);

        Assert.Equal(expectedType, shape.Type);
        Assert.Equal(new Vector2(10f, 10f), shape.Position);
        Assert.Equal(new Vector2(15f, 20f), shape.Size);
    }

    [Fact]
    public void MoveSelection_PublishesOneOperationPerMovedElement()
    {
        var board = new BoardState();
        var first = CreateRectangle(10f, 10f, 20f, 20f);
        var second = CreateRectangle(40f, 40f, 20f, 20f);
        board.Elements.Add(first);
        board.Elements.Add(second);

        var controller = new BoardToolController(board);
        controller.SetTool(BoardToolType.Select);

        controller.HandlePointerDown(new Vector2(0f, 0f));
        controller.HandlePointerMove(new Vector2(70f, 70f));
        controller.HandlePointerUp(new Vector2(70f, 70f));

        controller.HandlePointerDown(new Vector2(15f, 15f));
        controller.HandlePointerMove(new Vector2(25f, 35f));
        var result = controller.HandlePointerUp(new Vector2(25f, 35f));

        var operations = result.Operations.Cast<MoveElementOperation>().ToArray();

        Assert.Equal(2, operations.Length);
        Assert.Contains(operations, op => op.ElementId == first.Id);
        Assert.Contains(operations, op => op.ElementId == second.Id);
    }

    private static ShapeElement CreateRectangle(float x, float y, float width, float height)
        => new()
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(x, y),
            Size = new Vector2(width, height),
            Type = ShapeType.Rectangle
        };
}
