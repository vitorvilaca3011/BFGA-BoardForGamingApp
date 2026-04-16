using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.VisualTree;
using BFGA.Canvas.Rendering;
using BFGA.Core;
using BFGA.Core.Models;
using SkiaSharp;
using System.Numerics;
using System.Threading;
using Avalonia.Threading;

namespace BFGA.Canvas;

/// <summary>
/// A canvas control that renders board elements using SkiaSharp via Avalonia's
/// <see cref="ICustomDrawOperation"/> mechanism.
/// </summary>
public class BoardCanvas : Control
{
    private readonly ImageDecodeCache _imageCache = new();
    private long _renderGeneration;
    private float _dotGridOpacity = 0.1f;
    private float _zoom = 1.0f;
    private Vector2 _pan;
    private DispatcherTimer? _laserFadeTimer;

    /// <summary>
    /// Avalonia styled property for the board state to render.
    /// </summary>
    public static readonly StyledProperty<BoardState?> BoardProperty =
        AvaloniaProperty.Register<BoardCanvas, BoardState?>(nameof(Board));

    public static readonly StyledProperty<IReadOnlyDictionary<Guid, RemoteCursorState>?> RemoteCursorsProperty =
        AvaloniaProperty.Register<BoardCanvas, IReadOnlyDictionary<Guid, RemoteCursorState>?>(nameof(RemoteCursors));

    public static readonly StyledProperty<IReadOnlyDictionary<Guid, RemoteStrokePreviewState>?> RemoteStrokePreviewsProperty =
        AvaloniaProperty.Register<BoardCanvas, IReadOnlyDictionary<Guid, RemoteStrokePreviewState>?>(nameof(RemoteStrokePreviews));

    public static readonly StyledProperty<SelectionOverlayState?> SelectionOverlayProperty =
        AvaloniaProperty.Register<BoardCanvas, SelectionOverlayState?>(nameof(SelectionOverlay));

    public static readonly StyledProperty<EraserPreviewState?> EraserPreviewProperty =
        AvaloniaProperty.Register<BoardCanvas, EraserPreviewState?>(nameof(EraserPreview));

    public static readonly StyledProperty<IReadOnlyDictionary<Guid, RemoteLaserState>?> RemoteLasersProperty =
        AvaloniaProperty.Register<BoardCanvas, IReadOnlyDictionary<Guid, RemoteLaserState>?>(nameof(RemoteLasers));

    public static readonly StyledProperty<LocalLaserState?> LocalLaserProperty =
        AvaloniaProperty.Register<BoardCanvas, LocalLaserState?>(nameof(LocalLaser));

    public static readonly StyledProperty<PingMarkerState?> LocalPingProperty =
        AvaloniaProperty.Register<BoardCanvas, PingMarkerState?>(nameof(LocalPing));

    public float DotGridOpacity
    {
        get => _dotGridOpacity;
        set
        {
            _dotGridOpacity = Math.Clamp(value, 0f, 0.3f);
            InvalidateVisual();
        }
    }

    static BoardCanvas()
    {
        // Register handler once at the class level, not per instance,
        // so handlers are not duplicated for every BoardCanvas created.
        BoardProperty.Changed.AddClassHandler<BoardCanvas>((canvas, e) =>
        {
            // Clear the per-canvas image cache when the board instance changes,
            // since the new board may contain different ImageElements.
            if (!ReferenceEquals(e.OldValue, e.NewValue))
                canvas._imageCache.Clear();

            canvas.SyncImageCache(e.NewValue as BoardState);

            canvas.AdvanceRenderGeneration();
            canvas.InvalidateVisual();
        });

        RemoteCursorsProperty.Changed.AddClassHandler<BoardCanvas>((canvas, _) => canvas.OnOverlayChanged());
        RemoteStrokePreviewsProperty.Changed.AddClassHandler<BoardCanvas>((canvas, _) => canvas.OnOverlayChanged());
        SelectionOverlayProperty.Changed.AddClassHandler<BoardCanvas>((canvas, _) => canvas.OnOverlayChanged());
        EraserPreviewProperty.Changed.AddClassHandler<BoardCanvas>((canvas, _) => canvas.OnOverlayChanged());
        RemoteLasersProperty.Changed.AddClassHandler<BoardCanvas>((canvas, _) => canvas.OnLaserStateChanged());
        LocalLaserProperty.Changed.AddClassHandler<BoardCanvas>((canvas, _) => canvas.OnLaserStateChanged());
        LocalPingProperty.Changed.AddClassHandler<BoardCanvas>((canvas, _) => canvas.OnLaserStateChanged());
    }

    public BoardCanvas()
    {
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
    }

    public float Zoom
    {
        get => _zoom;
        set
        {
            if (_zoom != value)
            {
                _zoom = value;
                _renderGeneration++;
                InvalidateVisual();
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
                _renderGeneration++;
                InvalidateVisual();
            }
        }
    }

    /// <summary>
    /// Gets or sets the board state to render on this canvas.
    /// When changed, the control automatically invalidates and redraws.
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

    public SelectionOverlayState? SelectionOverlay
    {
        get => GetValue(SelectionOverlayProperty);
        set => SetValue(SelectionOverlayProperty, value);
    }

    public EraserPreviewState? EraserPreview
    {
        get => GetValue(EraserPreviewProperty);
        set => SetValue(EraserPreviewProperty, value);
    }

    public IReadOnlyDictionary<Guid, RemoteLaserState>? RemoteLasers
    {
        get => GetValue(RemoteLasersProperty);
        set => SetValue(RemoteLasersProperty, value);
    }

    public LocalLaserState? LocalLaser
    {
        get => GetValue(LocalLaserProperty);
        set => SetValue(LocalLaserProperty, value);
    }

    public PingMarkerState? LocalPing
    {
        get => GetValue(LocalPingProperty);
        set => SetValue(LocalPingProperty, value);
    }

    /// <summary>
    /// Explicitly invalidates the canvas so the current board instance is repainted.
    /// Call this after mutating the existing <see cref="BoardState"/> in place.
    /// </summary>
    public void InvalidateBoard()
    {
        SyncImageCache(Board);
        AdvanceRenderGeneration();
        InvalidateVisual();
    }

    internal void AdvanceRenderGeneration() => Interlocked.Increment(ref _renderGeneration);

    internal long RenderGeneration => Interlocked.Read(ref _renderGeneration);

    internal void ClearImageCache() => _imageCache.Clear();

    internal ImageDecodeCache ImageCache => _imageCache;

    private void SyncImageCache(BoardState? board)
    {
        if (board is null)
            return;

        _imageCache.SyncImages(board.Elements.OfType<ImageElement>());
    }

    private void OnOverlayChanged()
    {
        AdvanceRenderGeneration();
        InvalidateVisual();
    }

    private void OnLaserStateChanged()
    {
        AdvanceRenderGeneration();
        InvalidateVisual();
        UpdateLaserFadeTimer();
    }

    private void UpdateLaserFadeTimer()
    {
        var now = Environment.TickCount64;
        var hasVisible = LaserTrailRenderer.HasVisibleTrails(RemoteLasers, now)
            || LaserTrailRenderer.HasVisibleLocalLaser(LocalLaser, now)
            || LaserTrailRenderer.HasVisiblePing(LocalPing, now);
        if (hasVisible && _laserFadeTimer is null)
        {
            _laserFadeTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnLaserFadeTick);
            _laserFadeTimer.Start();
        }
        else if (!hasVisible && _laserFadeTimer is not null)
        {
            _laserFadeTimer.Stop();
            _laserFadeTimer = null;
        }
    }

    private void OnLaserFadeTick(object? sender, EventArgs e)
    {
        var now = Environment.TickCount64;
        var hasVisible = LaserTrailRenderer.HasVisibleTrails(RemoteLasers, now)
            || LaserTrailRenderer.HasVisibleLocalLaser(LocalLaser, now)
            || LaserTrailRenderer.HasVisiblePing(LocalPing, now);
        if (!hasVisible)
        {
            _laserFadeTimer?.Stop();
            _laserFadeTimer = null;
            return;
        }
        AdvanceRenderGeneration();
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _laserFadeTimer?.Stop();
        _laserFadeTimer = null;
        _imageCache.Clear();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var board = Board;
        if (board is null)
            return;

        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var drawOp = new BoardDrawOperation(this, bounds, board, _zoom, _pan);
        context.Custom(drawOp);
    }

    /// <summary>
    /// Custom draw operation that renders board elements via SkiaSharp.
    /// </summary>
    private sealed class BoardDrawOperation : ICustomDrawOperation
    {
        private readonly BoardCanvas _owner;
        private readonly BoardState _board;
        private readonly long _renderGeneration;
        private readonly float _zoom;
        private readonly Vector2 _pan;
        private readonly IReadOnlyDictionary<Guid, RemoteStrokePreviewState>? _remoteStrokePreviews;
        private readonly IReadOnlyDictionary<Guid, RemoteCursorState>? _remoteCursors;
        private readonly SelectionOverlayState? _selectionOverlay;
        private readonly EraserPreviewState? _eraserPreview;
        private readonly IReadOnlyDictionary<Guid, RemoteLaserState>? _remoteLasers;
        private readonly LocalLaserState? _localLaser;
        private readonly PingMarkerState? _localPing;

        public BoardDrawOperation(BoardCanvas owner, Rect bounds, BoardState board, float zoom, Vector2 pan)
        {
            _owner = owner;
            Bounds = bounds;
            _board = board;
            _renderGeneration = owner.RenderGeneration;
            _zoom = zoom;
            _pan = pan;
            // Snapshot styled properties here on the UI thread — they cannot be
            // read from the render thread (Avalonia throws "Call from invalid thread").
            _remoteStrokePreviews = owner.RemoteStrokePreviews;
            _remoteCursors = owner.RemoteCursors;
            _selectionOverlay = owner.SelectionOverlay;
            _eraserPreview = owner.EraserPreview;
            _remoteLasers = owner.RemoteLasers;
            _localLaser = owner.LocalLaser;
            _localPing = owner.LocalPing;
        }

        public Rect Bounds { get; }

        public void Dispose()
        {
            // No unmanaged resources to dispose
        }

        public bool Equals(ICustomDrawOperation? other)
        {
            return other is BoardDrawOperation operation
                && Bounds.Equals(operation.Bounds)
                && ReferenceEquals(_board, operation._board)
                && ReferenceEquals(_owner, operation._owner)
                && _renderGeneration == operation._renderGeneration
                && _zoom == operation._zoom
                && _pan == operation._pan
                && Equals(_selectionOverlay, operation._selectionOverlay);
        }

        public bool HitTest(Point p) => Bounds.Contains(p);

        public void Render(ImmediateDrawingContext drawingContext)
        {
            // Try to get SkiaSharp lease for direct SKCanvas access
            var leaseFeature = drawingContext.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as ISkiaSharpApiLeaseFeature;
            if (leaseFeature is null)
                return;

            using var lease = leaseFeature.Lease();
            var canvas = lease?.SkCanvas;
            if (canvas is null)
                return;

            canvas.Save();
            canvas.Translate(_pan.X, _pan.Y);
            canvas.Scale(_zoom, _zoom);

            try
            {
                var visibleBounds = canvas.LocalClipBounds;
                using (var backgroundPaint = new SKPaint { Color = ThemeColors.BgSurface })
                {
                    canvas.DrawRect(visibleBounds, backgroundPaint);
                }

                var dotColor = new SKColor(
                    ThemeColors.DotGrid.Red,
                    ThemeColors.DotGrid.Green,
                    ThemeColors.DotGrid.Blue,
                    (byte)(_owner._dotGridOpacity * 255));
                DotGridHelper.DrawDots(canvas, visibleBounds, Vector2.Zero, 24f, dotColor, 1.25f, _zoom);

                var elements = _board.Elements;
                var sortedElements = IsSortedByZIndex(elements)
                    ? elements
                    : elements.OrderBy(e => e.ZIndex).ToList();

                foreach (var element in sortedElements)
                {
                    ElementDrawingHelper.DrawElement(canvas, element, null);
                }

                SelectionOverlayRenderer.Draw(canvas, _board, _selectionOverlay, _zoom);
                DrawEraserPreview(canvas, _eraserPreview, _zoom);
                DrawRemoteStrokePreviews(canvas, _remoteStrokePreviews);
                DrawRemoteCursors(canvas, _remoteCursors);
                DrawLaserTrails(canvas, _remoteLasers);
                DrawLocalLaser(canvas, _localLaser, _zoom);
                DrawPingMarker(canvas, _localPing, _zoom);
            }
            finally
            {
                canvas.Restore();
            }
        }

        private static void DrawRemoteStrokePreviews(SKCanvas canvas, IReadOnlyDictionary<Guid, RemoteStrokePreviewState>? previews)
        {
            if (previews is null)
                return;

            foreach (var preview in previews.Values)
            {
                CollaboratorOverlayHelper.DrawRemoteStrokePreview(canvas, preview);
            }
        }

        private static void DrawEraserPreview(SKCanvas canvas, EraserPreviewState? preview, float zoom)
        {
            if (preview is null)
                return;

            EraserPreviewRenderer.Draw(canvas, preview.Value, zoom);
        }

        private static void DrawRemoteCursors(SKCanvas canvas, IReadOnlyDictionary<Guid, RemoteCursorState>? cursors)
        {
            if (cursors is null)
                return;

            foreach (var cursor in cursors.Values)
            {
                CollaboratorOverlayHelper.DrawRemoteCursor(canvas, cursor);
            }
        }

        private static void DrawLaserTrails(SKCanvas canvas, IReadOnlyDictionary<Guid, RemoteLaserState>? lasers)
        {
            LaserTrailRenderer.DrawLaserTrails(canvas, lasers, Environment.TickCount64);
        }

        private static void DrawLocalLaser(SKCanvas canvas, LocalLaserState? laser, float zoom)
        {
            LaserTrailRenderer.DrawLocalLaser(canvas, laser, Environment.TickCount64, zoom);
        }

        private static void DrawPingMarker(SKCanvas canvas, PingMarkerState? ping, float zoom)
        {
            LaserTrailRenderer.DrawPingMarker(canvas, ping, Environment.TickCount64, zoom);
        }

        private static bool IsSortedByZIndex(IReadOnlyList<BoardElement> elements)
        {
            for (int i = 1; i < elements.Count; i++)
            {
                if (elements[i].ZIndex < elements[i - 1].ZIndex)
                    return false;
            }
            return true;
        }
    }

}
