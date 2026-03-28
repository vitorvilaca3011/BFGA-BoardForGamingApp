using System.Numerics;
using BFGA.Canvas.Rendering;
using BFGA.Core;
using BFGA.Core.Models;
using BFGA.Network.Protocol;
using SkiaSharp;

namespace BFGA.Canvas.Tools;

public sealed class BoardToolController
{
    private const float RotationEpsilon = 0.0001f;

    private enum GestureMode
    {
        None,
        SelectBox,
        Manipulate,
        Pen,
        Shape
    }

    private readonly SelectionState _selection = new();
    private readonly Dictionary<Guid, Vector2> _interactionStartPositions = new();
    private readonly Dictionary<Guid, float> _interactionStartRotations = new();
    private GestureMode _gestureMode = GestureMode.None;
    private Vector2 _anchor;
    private Vector2 _current;
    private SKRect _interactionStartBounds;
    private Vector2 _interactionStartPointer;
    private StrokeElement? _activeStroke;
    private ShapeElement? _activeShape;

    public BoardToolController(BoardState board)
    {
        Board = board;
    }

    public BoardState Board { get; private set; }

    public SelectionState Selection => _selection;

    public BoardToolType CurrentTool { get; private set; } = BoardToolType.Select;

    public ShapeType ShapeType { get; set; } = ShapeType.Rectangle;

    public void SetTool(BoardToolType tool)
    {
        CurrentTool = tool;
        _gestureMode = GestureMode.None;
        _activeStroke = null;
        _activeShape = null;
    }

    public void SetBoard(BoardState board)
    {
        var selectedIds = _selection.SelectedElementIds.ToArray();

        if (ReferenceEquals(Board, board))
            return;

        Board = board;
        PreserveSelection(selectedIds);
        _gestureMode = GestureMode.None;
        _activeStroke = null;
        _activeShape = null;
        _interactionStartPositions.Clear();
        _interactionStartRotations.Clear();
    }

    private void PreserveSelection(IReadOnlyCollection<Guid> selectedIds)
    {
        var existing = selectedIds.Where(id => Board.Elements.Any(element => element.Id == id)).ToArray();

        if (existing.Length == 0)
        {
            _selection.Clear();
            return;
        }

        _selection.SelectMany(existing);
    }

    public ToolResult HandlePointerDown(Vector2 position, bool isShiftPressed = false, bool isCtrlPressed = false)
    {
        switch (CurrentTool)
        {
            case BoardToolType.Select:
                return HandleSelectDown(position);
            case BoardToolType.Hand:
                return ToolResult.None;
            case BoardToolType.Pen:
                return HandlePenDown(position);
            case BoardToolType.Rectangle:
            case BoardToolType.Ellipse:
            case BoardToolType.Shape:
                return HandleShapeDown(position);
            case BoardToolType.Eraser:
                return HandleEraserDown(position);
            case BoardToolType.Image:
                return ToolResult.HandledOnly;
            default:
                return ToolResult.None;
        }
    }

    public ToolResult HandlePointerMove(Vector2 position, bool isShiftPressed = false, bool isCtrlPressed = false)
    {
        _current = position;

        return _gestureMode switch
        {
            GestureMode.SelectBox => ToolResult.HandledOnly,
            GestureMode.Manipulate => HandleManipulationMove(position),
            GestureMode.Pen => HandlePenMove(position),
            GestureMode.Shape => HandleShapeMove(position),
            _ => ToolResult.None
        };
    }

    public ToolResult HandlePointerUp(Vector2 position, bool isShiftPressed = false, bool isCtrlPressed = false)
    {
        _current = position;

        return _gestureMode switch
        {
            GestureMode.SelectBox => FinishSelectBox(position),
            GestureMode.Manipulate => FinishManipulation(position),
            GestureMode.Pen => FinishPen(position),
            GestureMode.Shape => FinishShape(position),
            _ => ToolResult.None
        };
    }

    public ImageElement PlaceImage(byte[] imageData, string fileName, Vector2 position, Vector2 size)
    {
        var image = new ImageElement
        {
            Id = Guid.NewGuid(),
            Position = position,
            Size = size,
            ImageData = imageData,
            OriginalFileName = fileName,
            ZIndex = GetNextZIndex()
        };

        Board.Elements.Add(image);
        return image;
    }

    public IReadOnlyList<SelectionHandle> GetSelectionHandles()
    {
        var selected = GetSelectedElement();
        if (selected is null)
            return Array.Empty<SelectionHandle>();

        var bounds = ElementBoundsHelper.GetBounds(selected);
        var midX = (bounds.Left + bounds.Right) / 2f;
        var rotateY = bounds.Top - 22f;
        var handles = new List<SelectionHandle>();

        if (ElementBoundsHelper.SupportsResize(selected) && MathF.Abs(selected.Rotation) <= RotationEpsilon)
        {
            handles.Add(new SelectionHandle(SelectionHandleKind.ResizeTopLeft, new Vector2(bounds.Left, bounds.Top)));
            handles.Add(new SelectionHandle(SelectionHandleKind.ResizeTopRight, new Vector2(bounds.Right, bounds.Top)));
            handles.Add(new SelectionHandle(SelectionHandleKind.ResizeBottomLeft, new Vector2(bounds.Left, bounds.Bottom)));
            handles.Add(new SelectionHandle(SelectionHandleKind.ResizeBottomRight, new Vector2(bounds.Right, bounds.Bottom)));
        }

        if (ElementBoundsHelper.SupportsRotation(selected))
            handles.Add(new SelectionHandle(SelectionHandleKind.Rotate, new Vector2(midX, rotateY)));

        return selected switch
        {
            StrokeElement => Array.Empty<SelectionHandle>(),
            TextElement => Array.Empty<SelectionHandle>(),
            _ => handles.ToArray()
        };
    }

    private ToolResult HandleSelectDown(Vector2 position)
    {
        var selected = GetSelectedElement();
        if (selected is not null)
        {
            var handle = GetSelectionHandles().FirstOrDefault(h => Vector2.Distance(h.Position, position) <= h.HitRadius);
            if (handle is not null)
            {
                _selection.BeginHandleInteraction(selected.Id, handle.Kind);
                BeginManipulation(position, handle.Kind);
                return ToolResult.HandledOnly;
            }
        }

        var hit = HitTestHelper.GetTopmostHit(Board, position);
        if (hit is not null)
        {
            if (_selection.Contains(hit.Id))
            {
                _selection.BeginMoveInteraction();
                BeginManipulation(position, SelectionHandleKind.Move);
                return ToolResult.HandledOnly;
            }

            _selection.Select(hit.Id);
            _gestureMode = GestureMode.None;
            return ToolResult.HandledOnly;
        }

        _selection.Clear();
        _gestureMode = GestureMode.SelectBox;
        _anchor = position;
        _current = position;
        return ToolResult.HandledOnly;
    }

    private ToolResult FinishSelectBox(Vector2 position)
    {
        var selectionRect = CreateRect(_anchor, position);
        var selectedIds = Board.Elements
            .Where(e => Intersects(selectionRect, ElementBoundsHelper.GetBounds(e)))
            .Select(e => e.Id)
            .ToList();

        _selection.SelectMany(selectedIds);
        _gestureMode = GestureMode.None;
        return ToolResult.HandledOnly;
    }

    private ToolResult HandlePenDown(Vector2 position)
    {
        _selection.Clear();
        _gestureMode = GestureMode.Pen;
        _anchor = position;
        _current = position;
        _activeStroke = new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = position,
            Points = [Vector2.Zero],
            Color = SKColors.Black,
            Thickness = 2f,
            ZIndex = GetNextZIndex()
        };

        Board.Elements.Add(_activeStroke);
        return ToolResult.Changed;
    }

    private ToolResult HandlePenMove(Vector2 position)
    {
        if (_activeStroke is null)
            return ToolResult.None;

        var point = position - _activeStroke.Position;
        var changed = _activeStroke.Points.Count == 0 || _activeStroke.Points[^1] != point;
        if (changed)
            _activeStroke.Points.Add(point);

        return changed ? ToolResult.Changed : ToolResult.HandledOnly;
    }

    private ToolResult FinishPen(Vector2 position)
    {
        var result = HandlePenMove(position);
        var stroke = _activeStroke;
        _gestureMode = GestureMode.None;
        _activeStroke = null;

        return stroke is null
            ? result
            : new ToolResult(result.Handled, result.BoardChanged, [new AddElementOperation(stroke)]);
    }

    private ToolResult HandleShapeDown(Vector2 position)
    {
        _selection.Clear();
        _gestureMode = GestureMode.Shape;
        _anchor = position;
        _current = position;
        _activeShape = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = position,
            Size = Vector2.Zero,
            Type = ShapeType,
            StrokeColor = SKColors.Black,
            FillColor = SKColors.Transparent,
            StrokeWidth = 1f
        };

        Board.Elements.Add(_activeShape);

        return ToolResult.HandledOnly;
    }

    private ToolResult HandleShapeMove(Vector2 position)
    {
        if (_activeShape is null)
            return ToolResult.None;

        var newPosition = new Vector2(MathF.Min(_anchor.X, position.X), MathF.Min(_anchor.Y, position.Y));
        var newSize = new Vector2(MathF.Abs(position.X - _anchor.X), MathF.Abs(position.Y - _anchor.Y));
        if (_activeShape.Position == newPosition && _activeShape.Size == newSize)
            return ToolResult.HandledOnly;

        _activeShape.Position = newPosition;
        _activeShape.Size = newSize;
        return ToolResult.Changed;
    }

    private ToolResult FinishShape(Vector2 position)
    {
        var result = HandleShapeMove(position);
        var shape = _activeShape;

        if (shape is not null && !(shape.Position == _anchor && shape.Size == Vector2.Zero))
        {
            shape.ZIndex = GetNextZIndex();

            _gestureMode = GestureMode.None;
            _activeShape = null;
            return new ToolResult(result.Handled, true, [new AddElementOperation(shape)]);
        }

        if (shape is not null)
            Board.Elements.Remove(shape);

        _gestureMode = GestureMode.None;
        _activeShape = null;
        return ToolResult.HandledOnly;
    }

    private ToolResult HandleEraserDown(Vector2 position)
    {
        var hit = HitTestHelper.GetTopmostHit(Board, position);
        if (hit is null)
            return ToolResult.HandledOnly;

        Board.Elements.Remove(hit);
        _selection.Clear();
        return new ToolResult(true, true, [new DeleteElementOperation(hit.Id)]);
    }

    private BoardElement? GetSelectedElement()
    {
        var id = _selection.ActiveElementId;
        return id is null ? null : Board.Elements.FirstOrDefault(e => e.Id == id);
    }

    private ToolResult HandleManipulationMove(Vector2 position)
    {
        var activeHandle = _selection.ActiveHandle;
        if (activeHandle == SelectionHandleKind.Move)
            return ApplyMove(position);

        return activeHandle == SelectionHandleKind.Rotate
            ? ApplyRotate(position)
            : ApplyResize(position, activeHandle);
    }

    private void BeginManipulation(Vector2 position, SelectionHandleKind handleKind)
    {
        _interactionStartPointer = position;
        _interactionStartBounds = GetSelectedBounds();
        _interactionStartPositions.Clear();
        _interactionStartRotations.Clear();

        foreach (var element in Board.Elements.Where(e => _selection.Contains(e.Id)))
        {
            _interactionStartPositions[element.Id] = element.Position;
            _interactionStartRotations[element.Id] = element.Rotation;
        }

        if (handleKind != SelectionHandleKind.Move && _selection.ActiveElementId is not null)
            _selection.BeginHandleInteraction(_selection.ActiveElementId.Value, handleKind);
        _gestureMode = GestureMode.Manipulate;
    }

    private ToolResult ApplyMove(Vector2 position)
    {
        var delta = position - _interactionStartPointer;
        if (delta.LengthSquared() < float.Epsilon)
            return ToolResult.HandledOnly;

        foreach (var element in Board.Elements.Where(e => _interactionStartPositions.ContainsKey(e.Id)))
            element.Position = _interactionStartPositions[element.Id] + delta;

        return ToolResult.Changed;
    }

    private ToolResult ApplyResize(Vector2 position, SelectionHandleKind handleKind)
    {
        var selected = GetSelectedElement();
        if (selected is null)
            return ToolResult.None;

        if (!ElementBoundsHelper.SupportsResize(selected) || selected is StrokeElement || selected is TextElement)
            return ToolResult.HandledOnly;

        if (MathF.Abs(selected.Rotation) > RotationEpsilon)
            return ToolResult.HandledOnly;

        var bounds = _interactionStartBounds;
        var (fixedX, fixedY, movingX, movingY) = handleKind switch
        {
            SelectionHandleKind.ResizeTopLeft => (bounds.Right, bounds.Bottom, position.X, position.Y),
            SelectionHandleKind.ResizeTopRight => (bounds.Left, bounds.Bottom, position.X, position.Y),
            SelectionHandleKind.ResizeBottomLeft => (bounds.Right, bounds.Top, position.X, position.Y),
            SelectionHandleKind.ResizeBottomRight => (bounds.Left, bounds.Top, position.X, position.Y),
            _ => (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom)
        };

        if (MathF.Abs(movingX - fixedX) < float.Epsilon && MathF.Abs(movingY - fixedY) < float.Epsilon)
            return ToolResult.HandledOnly;

        var newPosition = new Vector2(MathF.Min(fixedX, movingX), MathF.Min(fixedY, movingY));
        var newSize = new Vector2(MathF.Abs(movingX - fixedX), MathF.Abs(movingY - fixedY));
        if (selected.Position == newPosition && selected.Size == newSize)
            return ToolResult.HandledOnly;

        selected.Position = newPosition;
        selected.Size = newSize;
        return ToolResult.Changed;
    }

    private ToolResult ApplyRotate(Vector2 position)
    {
        var selected = GetSelectedElement();
        if (selected is null)
            return ToolResult.None;

        if (!ElementBoundsHelper.SupportsRotation(selected))
            return ToolResult.HandledOnly;

        var center = new Vector2(
            _interactionStartBounds.Left + _interactionStartBounds.Width / 2f,
            _interactionStartBounds.Top + _interactionStartBounds.Height / 2f);

        var startVector = _interactionStartPointer - center;
        var currentVector = position - center;
        if (startVector.LengthSquared() < float.Epsilon || currentVector.LengthSquared() < float.Epsilon)
            return ToolResult.HandledOnly;

        var startAngle = MathF.Atan2(startVector.Y, startVector.X);
        var currentAngle = MathF.Atan2(currentVector.Y, currentVector.X);
        var deltaRadians = NormalizeRadians(currentAngle - startAngle);
        var deltaDegrees = deltaRadians * (180f / MathF.PI);

        if (MathF.Abs(deltaDegrees) < float.Epsilon)
            return ToolResult.HandledOnly;

        var newRotation = _interactionStartRotations[selected.Id] + deltaDegrees;
        if (MathF.Abs(selected.Rotation - newRotation) < float.Epsilon)
            return ToolResult.HandledOnly;

        selected.Rotation = newRotation;
        return ToolResult.Changed;
    }

    private ToolResult FinishManipulation(Vector2 position)
    {
        var result = HandleManipulationMove(position);
        var operations = result.BoardChanged ? CreateManipulationOperations() : Array.Empty<BoardOperation>();
        _gestureMode = GestureMode.None;
        _selection.EndInteraction();
        _interactionStartPositions.Clear();
        _interactionStartRotations.Clear();

        return operations.Count == 0
            ? result
            : new ToolResult(result.Handled, result.BoardChanged, operations);
    }

    private IReadOnlyList<BoardOperation> CreateManipulationOperations()
    {
        if (_selection.ActiveHandle == SelectionHandleKind.Move)
        {
            return _interactionStartPositions.Keys
                .Select(elementId => CreateMoveOperation(FindElement(elementId)!))
                .Where(operation => operation is not null)
                .Cast<BoardOperation>()
                .ToArray();
        }

        var selected = GetSelectedElement();
        return selected is null ? Array.Empty<BoardOperation>() : [CreateMoveOperation(selected)];
    }

    private static MoveElementOperation CreateMoveOperation(BoardElement element)
        => new(element.Id, element.Position, element.Size, element.Rotation);

    private BoardElement? FindElement(Guid id) => Board.Elements.FirstOrDefault(e => e.Id == id);

    private SKRect GetSelectedBounds()
    {
        var selected = GetSelectedElement();
        return selected is null ? SKRect.Empty : ElementBoundsHelper.GetBounds(selected);
    }

    private int GetNextZIndex()
        => Board.Elements.Count == 0 ? 0 : Board.Elements.Max(e => e.ZIndex) + 1;

    private static SKRect CreateRect(Vector2 a, Vector2 b)
        => new(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y), MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));

    private static float NormalizeRadians(float radians)
    {
        while (radians <= -MathF.PI)
            radians += MathF.Tau;

        while (radians > MathF.PI)
            radians -= MathF.Tau;

        return radians;
    }

    private static bool Intersects(SKRect a, SKRect b)
        => a.Left <= b.Right && a.Right >= b.Left && a.Top <= b.Bottom && a.Bottom >= b.Top;
}
