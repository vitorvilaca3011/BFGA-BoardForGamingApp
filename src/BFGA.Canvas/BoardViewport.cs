using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using BFGA.Core;
using BFGA.Canvas.Rendering;
using System.Numerics;

namespace BFGA.Canvas;

/// <summary>
/// A pan-and-zoom viewport that hosts a <see cref="BoardCanvas"/>.
/// This is the primary control to place on a view; <see cref="BoardCanvas"/>
/// is an internal rendering surface and should not be used directly.
/// </summary>
public class BoardViewport : Border
{
    private readonly ZoomBorder _zoomBorder;
    private readonly BoardCanvas _canvas;
    private bool _initialCenterApplied;

    static BoardViewport()
    {
        BoardProperty.Changed.AddClassHandler<BoardViewport>((viewport, e) =>
        {
            var newBoard = e.NewValue as BoardState;
            viewport._canvas.Board = newBoard;
        });

        DotGridOpacityProperty.Changed.AddClassHandler<BoardViewport>((vp, e) =>
        {
            vp._canvas.DotGridOpacity = (float)e.NewValue!;
        });

        RemoteCursorsProperty.Changed.AddClassHandler<BoardViewport>((viewport, e) =>
        {
            viewport._canvas.RemoteCursors = e.NewValue as IReadOnlyDictionary<Guid, RemoteCursorState>;
        });

        RemoteStrokePreviewsProperty.Changed.AddClassHandler<BoardViewport>((viewport, e) =>
        {
            viewport._canvas.RemoteStrokePreviews = e.NewValue as IReadOnlyDictionary<Guid, RemoteStrokePreviewState>;
        });
    }

    /// <summary>
    /// Avalonia styled property for the board state to render.
    /// Forwards to the inner <see cref="BoardCanvas"/>.
    /// </summary>
    public static readonly StyledProperty<BoardState?> BoardProperty =
        BoardCanvas.BoardProperty.AddOwner<BoardViewport>();

    public static readonly StyledProperty<IReadOnlyDictionary<Guid, RemoteCursorState>?> RemoteCursorsProperty =
        BoardCanvas.RemoteCursorsProperty.AddOwner<BoardViewport>();

    public static readonly StyledProperty<IReadOnlyDictionary<Guid, RemoteStrokePreviewState>?> RemoteStrokePreviewsProperty =
        BoardCanvas.RemoteStrokePreviewsProperty.AddOwner<BoardViewport>();

    public static readonly StyledProperty<float> DotGridOpacityProperty =
        AvaloniaProperty.Register<BoardViewport, float>(nameof(DotGridOpacity), 0.1f);

    /// <summary>
    /// Gets or sets the board state rendered inside this viewport.
    /// </summary>
    public BoardState? Board
    {
        get => GetValue(BoardProperty);
        set => SetValue(BoardProperty, value);
    }

    public IReadOnlyDictionary<Guid, RemoteCursorState>? RemoteCursors
    {
        get => GetValue(RemoteCursorsProperty);
        set => SetValue(RemoteCursorsProperty, value);
    }

    public IReadOnlyDictionary<Guid, RemoteStrokePreviewState>? RemoteStrokePreviews
    {
        get => GetValue(RemoteStrokePreviewsProperty);
        set => SetValue(RemoteStrokePreviewsProperty, value);
    }

    public float DotGridOpacity
    {
        get => GetValue(DotGridOpacityProperty);
        set => SetValue(DotGridOpacityProperty, value);
    }

    public BoardViewport()
    {
        _canvas = new BoardCanvas();

        _zoomBorder = new ZoomBorder
        {
            EnablePan = true,
            EnableZoom = true,
            MinZoomX = 0.2,
            MaxZoomX = 3.0,
            MinZoomY = 0.2,
            MaxZoomY = 3.0,
            ZoomSpeed = 1.2,
            Child = _canvas
        };

        Child = _zoomBorder;
        ClipToBounds = true;
        Focusable = true;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += HandleLayoutUpdated;
        TryCenterWorkspaceOrigin();
    }

    /// <summary>
    /// Gets the inner <see cref="ZoomBorder"/> for advanced configuration.
    /// </summary>
    public ZoomBorder ZoomBorder => _zoomBorder;

    public Vector2 CanvasPointToBoard(Point canvasPoint)
    {
        var canvasVector = new Vector2((float)canvasPoint.X, (float)canvasPoint.Y);
        return canvasVector - BoardSurfaceHelper.StableOriginOffset;
    }

    public Point BoardPointToCanvas(Vector2 boardPoint)
    {
        var canvasPoint = BoardSurfaceHelper.BoardToCanvas(boardPoint, BoardSurfaceHelper.StableOriginOffset);
        return new Point(canvasPoint.X, canvasPoint.Y);
    }

    /// <summary>
    /// Explicitly invalidates the inner canvas so the current board instance is repainted.
    /// Call this after mutating the existing <see cref="BoardState"/> in place.
    /// </summary>
    public void InvalidateBoard() => _canvas.InvalidateBoard();

    /// <summary>
    /// Gets the inner <see cref="BoardCanvas"/>.
    /// This remains public so tests and advanced integration scenarios can
    /// inspect the hosted rendering surface directly.
    /// </summary>
    public BoardCanvas Canvas => _canvas;

    private void HandleLayoutUpdated(object? sender, EventArgs e) => TryCenterWorkspaceOrigin();

    private void TryCenterWorkspaceOrigin()
    {
        if (_initialCenterApplied || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        _zoomBorder.CenterOn(new Point(BoardSurfaceHelper.StableOriginOffset.X, BoardSurfaceHelper.StableOriginOffset.Y), false);
        _initialCenterApplied = true;
        LayoutUpdated -= HandleLayoutUpdated;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= HandleLayoutUpdated;
        base.OnDetachedFromVisualTree(e);
        _canvas.ClearImageCache();
    }
}
