using System;
using System.Collections.Generic;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BFGA.Core;
using BFGA.Canvas.Rendering;

namespace BFGA.Canvas;

/// <summary>
/// Hosts a BoardCanvas and manages zoom/pan state for the infinite canvas.
/// Handles mouse-wheel zoom. Pan gestures are routed by the parent BoardView.
/// </summary>
public class BoardViewport : Border
{
    private readonly BoardCanvas _canvas;
    private bool _initialCenterApplied;

    private double _zoom = 1.0;
    private Vector2 _pan;

    public const double MinZoom = 0.2;
    public const double MaxZoom = 3.0;
    private const double ZoomFactor = 1.15;

    public BoardViewport()
    {
        _canvas = new BoardCanvas();
        Child = _canvas;
        ClipToBounds = true;
        Focusable = true;
    }

    public double Zoom
    {
        get => _zoom;
        set
        {
            var clamped = Math.Clamp(value, MinZoom, MaxZoom);
            if (_zoom != clamped)
            {
                _zoom = clamped;
                SyncCanvasState();
            }
        }
    }

    public Vector2 Pan
    {
        get => _pan;
        set
        {
            if (_pan != value)
            {
                _pan = value;
                SyncCanvasState();
            }
        }
    }

    public BoardCanvas Canvas => _canvas;

    public static readonly StyledProperty<BoardState?> BoardProperty =
        BoardCanvas.BoardProperty.AddOwner<BoardViewport>();

    public static readonly StyledProperty<IReadOnlyDictionary<Guid, RemoteCursorState>?> RemoteCursorsProperty =
        BoardCanvas.RemoteCursorsProperty.AddOwner<BoardViewport>();

    public static readonly StyledProperty<IReadOnlyDictionary<Guid, RemoteStrokePreviewState>?> RemoteStrokePreviewsProperty =
        BoardCanvas.RemoteStrokePreviewsProperty.AddOwner<BoardViewport>();

    public static readonly StyledProperty<float> DotGridOpacityProperty =
        AvaloniaProperty.Register<BoardViewport, float>(nameof(DotGridOpacity), 0.1f);

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

    public event EventHandler? ZoomChanged;

    static BoardViewport()
    {
        BoardProperty.Changed.AddClassHandler<BoardViewport>((vp, _) =>
        {
            vp._canvas.Board = vp.Board;
        });

        DotGridOpacityProperty.Changed.AddClassHandler<BoardViewport>((vp, e) =>
        {
            vp._canvas.DotGridOpacity = (float)e.NewValue!;
        });

        RemoteCursorsProperty.Changed.AddClassHandler<BoardViewport>((vp, e) =>
        {
            vp._canvas.RemoteCursors = e.NewValue as IReadOnlyDictionary<Guid, RemoteCursorState>;
        });

        RemoteStrokePreviewsProperty.Changed.AddClassHandler<BoardViewport>((vp, e) =>
        {
            vp._canvas.RemoteStrokePreviews = e.NewValue as IReadOnlyDictionary<Guid, RemoteStrokePreviewState>;
        });
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += HandleLayoutUpdated;
        TryCenterOrigin();
    }

    private void HandleLayoutUpdated(object? sender, EventArgs e)
    {
        TryCenterOrigin();
    }

    /// <summary>Centers board (0,0) in the viewport on first layout.</summary>
    private void TryCenterOrigin()
    {
        if (_initialCenterApplied) return;
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

        _pan = new Vector2((float)(Bounds.Width / 2), (float)(Bounds.Height / 2));
        _zoom = 1.0;
        _initialCenterApplied = true;
        LayoutUpdated -= HandleLayoutUpdated;
        SyncCanvasState();
    }

    public Vector2 ScreenToBoard(Point screenPoint)
    {
        return new Vector2(
            (float)((screenPoint.X - _pan.X) / _zoom),
            (float)((screenPoint.Y - _pan.Y) / _zoom));
    }

    public Point BoardToScreen(Vector2 boardPoint)
    {
        return new Point(
            boardPoint.X * _zoom + _pan.X,
            boardPoint.Y * _zoom + _pan.Y);
    }

    public void SetZoom(double newZoom, double centerX, double centerY)
    {
        newZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);
        if (Math.Abs(_zoom - newZoom) < 0.0001) return;

        var ratio = newZoom / _zoom;
        _pan = new Vector2(
            (float)(centerX - (centerX - _pan.X) * ratio),
            (float)(centerY - (centerY - _pan.Y) * ratio));
        _zoom = newZoom;
        SyncCanvasState();
    }

    public void PanBy(Vector2 delta)
    {
        Pan = _pan + delta;
    }

    public void InvalidateBoard() => _canvas.InvalidateBoard();

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var pos = e.GetPosition(this);
        var direction = e.Delta.Y > 0 ? 1 : -1;
        var factor = direction > 0 ? ZoomFactor : 1.0 / ZoomFactor;
        var newZoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);

        SetZoom(newZoom, pos.X, pos.Y);
        e.Handled = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= HandleLayoutUpdated;
        base.OnDetachedFromVisualTree(e);
        _canvas.ClearImageCache();
    }

    private void SyncCanvasState()
    {
        _canvas.Zoom = (float)_zoom;
        _canvas.Pan = _pan;
        _canvas.InvalidateVisual();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }
}
