using System.Numerics;
using System.Reflection;
using BFGA.Canvas.Tools;
using BFGA.Core;
using BFGA.Core.Models;
using BFGA.Network.Protocol;
using SkiaSharp;

namespace BFGA.Core.Tests;

public class BoardToolControllerTests
{
    [Fact]
    public void SelectTool_ClickingElement_SelectsIt()
    {
        var (board, controller, target) = CreateControllerWithRectangle();

        controller.SetTool(BoardToolType.Select);
        controller.HandlePointerDown(new Vector2(20, 20));
        controller.HandlePointerUp(new Vector2(20, 20));

        Assert.Single(controller.Selection.SelectedElementIds);
        Assert.Contains(target.Id, controller.Selection.SelectedElementIds);
        Assert.Same(board, controller.Board);
    }

    [Fact]
    public void SetBoard_PreservesSelectionWhenElementStillExists()
    {
        var (board, controller, target) = CreateControllerWithRectangle();
        controller.Selection.Select(target.Id);

        var replacement = new BoardState();
        replacement.Elements.Add(new ShapeElement
        {
            Id = target.Id,
            Position = target.Position,
            Size = target.Size,
            Rotation = target.Rotation,
            ZIndex = target.ZIndex,
            OwnerId = target.OwnerId,
            IsLocked = target.IsLocked,
            Type = ((ShapeElement)target).Type,
            StrokeColor = ((ShapeElement)target).StrokeColor,
            FillColor = ((ShapeElement)target).FillColor,
            StrokeWidth = ((ShapeElement)target).StrokeWidth
        });

        controller.SetBoard(replacement);

        Assert.Same(replacement, controller.Board);
        Assert.Single(controller.Selection.SelectedElementIds);
        Assert.Contains(target.Id, controller.Selection.SelectedElementIds);
        Assert.Equal(target.Id, controller.Selection.ActiveElementId);
    }

    [Fact]
    public void SetBoard_ClearsSelectionWhenElementNoLongerExists()
    {
        var (_, controller, target) = CreateControllerWithRectangle();
        controller.Selection.Select(target.Id);

        controller.SetBoard(new BoardState());

        Assert.Empty(controller.Selection.SelectedElementIds);
        Assert.Null(controller.Selection.ActiveElementId);
    }

    [Fact]
    public void SelectTool_DragBox_SelectsIntersectingElements()
    {
        var board = new BoardState();
        var first = CreateRectangle(10, 10, 20, 20);
        var second = CreateRectangle(35, 35, 20, 20);
        board.Elements.Add(first);
        board.Elements.Add(second);

        var controller = new BoardToolController(board);
        controller.SetTool(BoardToolType.Select);

        controller.HandlePointerDown(new Vector2(0, 0));
        controller.HandlePointerMove(new Vector2(60, 60));
        controller.HandlePointerUp(new Vector2(60, 60));

        Assert.Equal(2, controller.Selection.SelectedElementIds.Count);
        Assert.Contains(first.Id, controller.Selection.SelectedElementIds);
        Assert.Contains(second.Id, controller.Selection.SelectedElementIds);
        Assert.Equal(SelectionHandleKind.None, controller.Selection.ActiveHandle);
    }

    [Fact]
    public void SelectTool_ClickingResizeHandle_BeginsResizeInteraction()
    {
        var (_, controller, target) = CreateControllerWithRectangle();
        controller.Selection.Select(target.Id);

        controller.SetTool(BoardToolType.Select);
        controller.HandlePointerDown(new Vector2(10, 10));

        Assert.Equal(SelectionHandleKind.ResizeTopLeft, controller.Selection.ActiveHandle);
        Assert.Equal(target.Id, controller.Selection.ActiveElementId);
    }

    [Fact]
    public void SelectTool_ClickingRotateHandle_BeginsRotationInteraction()
    {
        var (_, controller, target) = CreateControllerWithRectangle();
        controller.Selection.Select(target.Id);

        controller.SetTool(BoardToolType.Select);
        controller.HandlePointerDown(new Vector2(30, -12));

        Assert.Equal(SelectionHandleKind.Rotate, controller.Selection.ActiveHandle);
        Assert.Equal(target.Id, controller.Selection.ActiveElementId);
    }

    [Fact]
    public void SelectionHandles_StrokeAndTextExposeMoveOnlyByNoHandles()
    {
        var board = new BoardState();
        var stroke = new StrokeElement { Id = Guid.NewGuid(), Position = Vector2.Zero };
        var text = new TextElement { Id = Guid.NewGuid(), Position = Vector2.Zero };
        board.Elements.Add(stroke);
        board.Elements.Add(text);

        var controller = new BoardToolController(board);

        controller.Selection.Select(stroke.Id);
        Assert.Empty(controller.GetSelectionHandles());

        controller.Selection.Select(text.Id);
        Assert.Empty(controller.GetSelectionHandles());
    }

    [Fact]
    public void SelectionHandles_LineAndArrowExposeResizeOnly()
    {
        var board = new BoardState();
        var line = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 10),
            Size = new Vector2(40, 0),
            Type = ShapeType.Line
        };
        var arrow = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 10),
            Size = new Vector2(40, 0),
            Type = ShapeType.Arrow
        };
        board.Elements.Add(line);
        board.Elements.Add(arrow);

        var controller = new BoardToolController(board);

        controller.Selection.Select(line.Id);
        var lineHandles = controller.GetSelectionHandles();
        Assert.Equal(4, lineHandles.Count);
        Assert.DoesNotContain(lineHandles, h => h.Kind == SelectionHandleKind.Rotate);

        controller.Selection.Select(arrow.Id);
        var arrowHandles = controller.GetSelectionHandles();
        Assert.Equal(4, arrowHandles.Count);
        Assert.DoesNotContain(arrowHandles, h => h.Kind == SelectionHandleKind.Rotate);
    }

    [Fact]
    public void SelectionHandles_RotatableSubsetExposeRotateHandle()
    {
        var board = new BoardState();
        var image = new ImageElement { Id = Guid.NewGuid(), Position = new Vector2(10, 10), Size = new Vector2(40, 20) };
        var ellipse = new ShapeElement { Id = Guid.NewGuid(), Position = new Vector2(10, 10), Size = new Vector2(40, 20), Type = ShapeType.Ellipse };
        board.Elements.Add(image);
        board.Elements.Add(ellipse);

        var controller = new BoardToolController(board);

        controller.Selection.Select(image.Id);
        Assert.Contains(controller.GetSelectionHandles(), h => h.Kind == SelectionHandleKind.Rotate);

        controller.Selection.Select(ellipse.Id);
        Assert.Contains(controller.GetSelectionHandles(), h => h.Kind == SelectionHandleKind.Rotate);
    }

    [Theory]
    [InlineData(ShapeType.Rectangle)]
    [InlineData(ShapeType.Ellipse)]
    public void SelectionHandles_RotatedShapeExposeRotateButNoResizeHandles(ShapeType shapeType)
    {
        var board = new BoardState();
        var element = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 10),
            Size = new Vector2(40, 20),
            Type = shapeType,
            Rotation = 15f
        };
        board.Elements.Add(element);

        var controller = new BoardToolController(board);
        controller.Selection.Select(element.Id);

        var handles = controller.GetSelectionHandles();

        Assert.Contains(handles, h => h.Kind == SelectionHandleKind.Rotate);
        Assert.DoesNotContain(handles, h => h.Kind is SelectionHandleKind.ResizeTopLeft or SelectionHandleKind.ResizeTopRight or SelectionHandleKind.ResizeBottomLeft or SelectionHandleKind.ResizeBottomRight);
    }

    [Fact]
    public void SelectionHandles_RotatedImageExposeRotateButNoResizeHandles()
    {
        var board = new BoardState();
        var image = new ImageElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 10),
            Size = new Vector2(40, 20),
            Rotation = 15f
        };
        board.Elements.Add(image);

        var controller = new BoardToolController(board);
        controller.Selection.Select(image.Id);

        var handles = controller.GetSelectionHandles();

        Assert.Contains(handles, h => h.Kind == SelectionHandleKind.Rotate);
        Assert.DoesNotContain(handles, h => h.Kind is SelectionHandleKind.ResizeTopLeft or SelectionHandleKind.ResizeTopRight or SelectionHandleKind.ResizeBottomLeft or SelectionHandleKind.ResizeBottomRight);
    }

    [Fact]
    public void SelectTool_DraggingSelectedElement_MovesIt()
    {
        var (_, controller, target) = CreateControllerWithRectangle();
        controller.Selection.Select(target.Id);

        controller.SetTool(BoardToolType.Select);
        controller.HandlePointerDown(new Vector2(20, 20));
        controller.HandlePointerMove(new Vector2(35, 45));
        controller.HandlePointerUp(new Vector2(35, 45));

        Assert.Equal(new Vector2(25, 35), target.Position);
        Assert.Equal(SelectionHandleKind.None, controller.Selection.ActiveHandle);
    }

    [Fact]
    public void SelectTool_DraggingResizeHandle_ResizesElement()
    {
        var (_, controller, target) = CreateControllerWithRectangle();
        controller.Selection.Select(target.Id);

        controller.SetTool(BoardToolType.Select);
        controller.HandlePointerDown(new Vector2(10, 10));
        controller.HandlePointerMove(new Vector2(0, 0));
        controller.HandlePointerUp(new Vector2(0, 0));

        Assert.Equal(new Vector2(0, 0), target.Position);
        Assert.Equal(new Vector2(50, 50), target.Size);
        Assert.Equal(SelectionHandleKind.None, controller.Selection.ActiveHandle);
    }

    [Fact]
    public void RotatedElement_ResizeAttempt_DoesNotMutateGeometry()
    {
        var board = new BoardState();
        var target = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 10),
            Size = new Vector2(40, 20),
            Type = ShapeType.Rectangle,
            Rotation = 15f
        };
        board.Elements.Add(target);

        var controller = new BoardToolController(board);
        controller.Selection.Select(target.Id);

        var beforePosition = target.Position;
        var beforeSize = target.Size;
        var beginManipulation = typeof(BoardToolController).GetMethod("BeginManipulation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        beginManipulation.Invoke(controller, [new Vector2(0, 0), SelectionHandleKind.ResizeTopLeft]);

        var result = controller.HandlePointerMove(new Vector2(-20, -20));

        Assert.True(result.Handled);
        Assert.False(result.BoardChanged);
        Assert.Equal(beforePosition, target.Position);
        Assert.Equal(beforeSize, target.Size);
    }

    [Fact]
    public void SelectTool_DraggingRotateHandle_RotatesElement()
    {
        var (_, controller, target) = CreateControllerWithRectangle();
        controller.Selection.Select(target.Id);

        controller.SetTool(BoardToolType.Select);
        controller.HandlePointerDown(new Vector2(30, -12));
        controller.HandlePointerMove(new Vector2(62, 30));
        controller.HandlePointerUp(new Vector2(62, 30));

        Assert.NotEqual(0f, target.Rotation);
        Assert.Equal(SelectionHandleKind.None, controller.Selection.ActiveHandle);
    }

    [Fact]
    public void RotateTool_SeamCrossing_UsesSmallDelta()
    {
        var board = new BoardState();
        var target = CreateRectangle(10, 10, 40, 40);
        board.Elements.Add(target);

        var controller = new BoardToolController(board);
        controller.Selection.Select(target.Id);

        var center = new Vector2(30f, 30f);
        var startAngle = MathF.PI - 0.01f;
        var endAngle = -MathF.PI + 0.01f;
        var start = new Vector2(center.X + MathF.Cos(startAngle) * 20f, center.Y + MathF.Sin(startAngle) * 20f);
        var end = new Vector2(center.X + MathF.Cos(endAngle) * 20f, center.Y + MathF.Sin(endAngle) * 20f);

        var beginManipulation = typeof(BoardToolController).GetMethod("BeginManipulation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        beginManipulation.Invoke(controller, [start, SelectionHandleKind.Rotate]);

        var result = controller.HandlePointerMove(end);

        Assert.True(result.BoardChanged);
        Assert.InRange(MathF.Abs(target.Rotation), 0.5f, 10f);
    }

    [Fact]
    public void SelectTool_NoOpManipulation_ReturnsHandledOnly()
    {
        var (_, controller, target) = CreateControllerWithRectangle();
        controller.Selection.Select(target.Id);

        controller.SetTool(BoardToolType.Select);
        controller.HandlePointerDown(new Vector2(20, 20));

        var moveResult = controller.HandlePointerMove(new Vector2(20, 20));
        var upResult = controller.HandlePointerUp(new Vector2(20, 20));

        Assert.True(moveResult.Handled);
        Assert.False(moveResult.BoardChanged);
        Assert.True(upResult.Handled);
        Assert.False(upResult.BoardChanged);
        Assert.Equal(new Vector2(10, 10), target.Position);
        Assert.Equal(0f, target.Rotation);
    }

    [Fact]
    public void SelectTool_DraggingSelectedElementAfterMultiSelect_MovesAllSelected()
    {
        var board = new BoardState();
        var first = CreateRectangle(10, 10, 20, 20);
        var second = CreateRectangle(40, 40, 20, 20);
        board.Elements.Add(first);
        board.Elements.Add(second);

        var controller = new BoardToolController(board);
        controller.SetTool(BoardToolType.Select);

        controller.HandlePointerDown(new Vector2(0, 0));
        controller.HandlePointerMove(new Vector2(70, 70));
        controller.HandlePointerUp(new Vector2(70, 70));

        Assert.Equal(2, controller.Selection.SelectedElementIds.Count);

        controller.HandlePointerDown(new Vector2(15, 15));
        controller.HandlePointerMove(new Vector2(25, 35));
        controller.HandlePointerUp(new Vector2(25, 35));

        Assert.Equal(new Vector2(20, 30), first.Position);
        Assert.Equal(new Vector2(50, 60), second.Position);
    }

    [Fact]
    public void PenTool_CreatesStrokeFromDraggedPoints()
    {
        var board = new BoardState();
        var controller = new BoardToolController(board);

        controller.SetTool(BoardToolType.Pen);
        controller.HandlePointerDown(new Vector2(5, 5));
        controller.HandlePointerMove(new Vector2(10, 10));
        controller.HandlePointerMove(new Vector2(20, 20));
        controller.HandlePointerUp(new Vector2(20, 20));

        var stroke = Assert.Single(board.Elements.OfType<StrokeElement>());
        Assert.Equal(new Vector2(5, 5), stroke.Position);
        Assert.Equal(3, stroke.Points.Count);
        Assert.Equal(new Vector2(0, 0), stroke.Points[0]);
        Assert.Equal(new Vector2(15, 15), stroke.Points[^1]);
    }

    [Fact]
    public void ShapeTool_CreatesNormalizedRectangleFromDrag()
    {
        var board = new BoardState();
        var controller = new BoardToolController(board)
        {
            ShapeType = ShapeType.Rectangle
        };

        controller.SetTool(BoardToolType.Rectangle);
        controller.HandlePointerDown(new Vector2(40, 40));
        controller.HandlePointerMove(new Vector2(10, 20));
        controller.HandlePointerUp(new Vector2(10, 20));

        var shape = Assert.Single(board.Elements.OfType<ShapeElement>());
        Assert.Equal(new Vector2(10, 20), shape.Position);
        Assert.Equal(new Vector2(30, 20), shape.Size);
        Assert.Equal(ShapeType.Rectangle, shape.Type);
    }

    [Fact]
    public void ShapeTool_ClickOnly_DoesNotInsertZeroSizeShape()
    {
        var board = new BoardState();
        var controller = new BoardToolController(board)
        {
            ShapeType = ShapeType.Rectangle
        };

        controller.SetTool(BoardToolType.Rectangle);
        controller.HandlePointerDown(new Vector2(40, 40));
        var result = controller.HandlePointerUp(new Vector2(40, 40));

        Assert.True(result.Handled);
        Assert.False(result.BoardChanged);
        Assert.Empty(board.Elements);
    }

    [Fact]
    public void ShapeTool_InsertsShapeAboveExistingElements()
    {
        var board = new BoardState();
        var existing = CreateRectangle(0, 0, 10, 10);
        existing.ZIndex = 4;
        board.Elements.Add(existing);
        var controller = new BoardToolController(board)
        {
            ShapeType = ShapeType.Rectangle
        };

        controller.SetTool(BoardToolType.Rectangle);
        controller.HandlePointerDown(new Vector2(20, 20));
        controller.HandlePointerMove(new Vector2(30, 30));
        controller.HandlePointerUp(new Vector2(30, 30));

        var shape = Assert.Single(board.Elements.OfType<ShapeElement>(), e => e.Position == new Vector2(20, 20));
        Assert.Equal(5, shape.ZIndex);
    }

    [Fact]
    public void PenTool_InsertsStrokeAboveExistingElements()
    {
        var board = new BoardState();
        var existing = CreateRectangle(0, 0, 10, 10);
        existing.ZIndex = 9;
        board.Elements.Add(existing);
        var controller = new BoardToolController(board);

        controller.SetTool(BoardToolType.Pen);
        controller.HandlePointerDown(new Vector2(5, 5));
        controller.HandlePointerMove(new Vector2(10, 10));
        controller.HandlePointerUp(new Vector2(10, 10));

        var stroke = Assert.Single(board.Elements.OfType<StrokeElement>());
        Assert.Equal(10, stroke.ZIndex);
    }

    [Fact]
    public void ImageTool_InsertsImageElement()
    {
        var board = new BoardState();
        var controller = new BoardToolController(board);

        controller.PlaceImage(CreatePngBytes(), "sample.png", new Vector2(25, 30), new Vector2(40, 50));

        var image = Assert.Single(board.Elements.OfType<ImageElement>());
        Assert.Equal("sample.png", image.OriginalFileName);
        Assert.Equal(new Vector2(25, 30), image.Position);
        Assert.Equal(new Vector2(40, 50), image.Size);
    }

    [Fact]
    public void EraserTool_ClickingElementDeletesIt()
    {
        var board = new BoardState();
        var shape = CreateRectangle(0, 0, 100, 100);
        board.Elements.Add(shape);

        var controller = new BoardToolController(board);
        controller.SetTool(BoardToolType.Eraser);
        controller.HandlePointerDown(new Vector2(50, 50));
        controller.HandlePointerUp(new Vector2(50, 50));

        Assert.Empty(board.Elements);
    }

    [Fact]
    public void EraserBrush_Down_RemovesAllIntersectingElements()
    {
        var board = new BoardState();
        var first = new ShapeElement { Id = Guid.NewGuid(), Type = ShapeType.Rectangle, Position = new Vector2(20, 20), Size = new Vector2(12, 12) };
        var second = new ShapeElement { Id = Guid.NewGuid(), Type = ShapeType.Rectangle, Position = new Vector2(28, 20), Size = new Vector2(12, 12) };
        board.Elements.Add(first);
        board.Elements.Add(second);

        var controller = new BoardToolController(board);
        controller.SetTool(BoardToolType.Eraser);

var result = controller.HandlePointerDown(new Vector2(26, 26));

        Assert.True(result.Handled);
        Assert.True(result.BoardChanged);
        Assert.Empty(board.Elements);
        Assert.Equal(2, result.Operations.Count);
    }

    [Fact]
    public void EraserBrush_Drag_DeletesNewHitsOnlyOnce()
    {
        var board = new BoardState();
        var first = new ShapeElement { Id = Guid.NewGuid(), Type = ShapeType.Rectangle, Position = new Vector2(10, 10), Size = new Vector2(12, 12) };
        var second = new ShapeElement { Id = Guid.NewGuid(), Type = ShapeType.Rectangle, Position = new Vector2(28, 10), Size = new Vector2(12, 12) };
        var third = new ShapeElement { Id = Guid.NewGuid(), Type = ShapeType.Rectangle, Position = new Vector2(46, 10), Size = new Vector2(12, 12) };
        board.Elements.Add(first);
        board.Elements.Add(second);
        board.Elements.Add(third);

        var controller = new BoardToolController(board);
        controller.SetTool(BoardToolType.Eraser);

        controller.HandlePointerDown(new Vector2(16, 16));
        var moveResult = controller.HandlePointerMove(new Vector2(34, 16));
        var repeatResult = controller.HandlePointerMove(new Vector2(16, 16));

Assert.DoesNotContain(board.Elements, element => element.Id == first.Id);
        Assert.DoesNotContain(board.Elements, element => element.Id == second.Id);
        Assert.Contains(board.Elements, element => element.Id == third.Id);
        Assert.True(moveResult.Handled);
Assert.True(moveResult.BoardChanged);
        Assert.Equal(second.Id, Assert.IsType<DeleteElementOperation>(Assert.Single(moveResult.Operations)).ElementId);
        Assert.True(repeatResult.Handled);
        Assert.False(repeatResult.BoardChanged);
        Assert.Empty(repeatResult.Operations);
    }

    [Fact]
    public void EraserBrush_MoveWithoutDown_DoesNotErase()
    {
        var board = new BoardState();
        var shape = new ShapeElement { Id = Guid.NewGuid(), Type = ShapeType.Rectangle, Position = new Vector2(10, 10), Size = new Vector2(12, 12) };
        board.Elements.Add(shape);

        var controller = new BoardToolController(board);
        controller.SetTool(BoardToolType.Eraser);

        // Move without prior HandlePointerDown - should not erase
        var moveResult = controller.HandlePointerMove(new Vector2(16, 16));
        var upResult = controller.HandlePointerUp(new Vector2(16, 16));

        Assert.Single(board.Elements); // Element still exists
        Assert.False(moveResult.Handled);
        Assert.False(moveResult.BoardChanged);
        Assert.False(upResult.Handled);
        Assert.False(upResult.BoardChanged);
    }

    [Fact]
    public void DeleteSelectedElements_RemovesEverySelectedElement()
    {
        var board = new BoardState();
        var first = new ShapeElement { Id = Guid.NewGuid(), Type = ShapeType.Rectangle, Size = new Vector2(20, 20) };
        var second = new ShapeElement { Id = Guid.NewGuid(), Type = ShapeType.Ellipse, Position = new Vector2(30, 30), Size = new Vector2(10, 10) };
        board.Elements.Add(first);
        board.Elements.Add(second);

        var controller = new BoardToolController(board);
        controller.Selection.SelectMany([first.Id, second.Id]);

        var result = controller.DeleteSelectedElements();

        Assert.Empty(board.Elements);
        Assert.Empty(controller.Selection.SelectedElementIds);
        Assert.Collection(result.Operations,
            operation => Assert.Equal(first.Id, Assert.IsType<DeleteElementOperation>(operation).ElementId),
            operation => Assert.Equal(second.Id, Assert.IsType<DeleteElementOperation>(operation).ElementId));
    }

    [Fact]
    public void PlaceText_AddsTextElementWithProvidedContent()
    {
        var board = new BoardState();
        var controller = new BoardToolController(board);

        var text = controller.PlaceText("hello", new Vector2(50, 60), SKColors.White, 24f, "Inter");

        var element = Assert.Single(board.Elements.OfType<TextElement>());
        Assert.Same(text, element);
        Assert.Equal("hello", element.Text);
        Assert.Equal(new Vector2(50, 60), element.Position);
        Assert.Equal(SKColors.White, element.Color);
        Assert.Equal(24f, element.FontSize);
        Assert.Equal("Inter", element.FontFamily);
    }

    private static (BoardState board, BoardToolController controller, ShapeElement target) CreateControllerWithRectangle()
    {
        var board = new BoardState();
        var target = CreateRectangle(10, 10, 40, 40);
        board.Elements.Add(target);

        return (board, new BoardToolController(board), target);
    }

    private static ShapeElement CreateRectangle(float x, float y, float width, float height)
    {
        return new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(x, y),
            Size = new Vector2(width, height),
            Type = ShapeType.Rectangle,
            StrokeColor = SKColors.Black,
            FillColor = SKColors.Transparent,
            StrokeWidth = 1f
        };
    }

    private static byte[] CreatePngBytes()
    {
        using var bitmap = new SKBitmap(1, 1);
        bitmap.SetPixel(0, 0, SKColors.Red);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
