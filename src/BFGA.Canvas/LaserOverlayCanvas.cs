using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BFGA.Canvas.Rendering;
using SkiaSharp;
using System.Numerics;

namespace BFGA.Canvas;

public class LaserOverlayCanvas : Control
{
    private const long RemoteLaserStaleTimeoutMs = 3000;
    private float _zoom = 1f;
    private Vector2 _pan;
    private DispatcherTimer? _laserFadeTimer;
    private long _renderGeneration;

    public static readonly StyledProperty<IReadOnlyDictionary<Guid, RemoteLaserState>?> RemoteLasersProperty =
        AvaloniaProperty.Register<LaserOverlayCanvas, IReadOnlyDictionary<Guid, RemoteLaserState>?>(nameof(RemoteLasers));

    public static readonly StyledProperty<LocalLaserState?> LocalLaserProperty =
        AvaloniaProperty.Register<LaserOverlayCanvas, LocalLaserState?>(nameof(LocalLaser));

    public static readonly StyledProperty<PingMarkerState?> LocalPingProperty =
        AvaloniaProperty.Register<LaserOverlayCanvas, PingMarkerState?>(nameof(LocalPing));

    public static readonly StyledProperty<float> ZoomProperty =
        AvaloniaProperty.Register<LaserOverlayCanvas, float>(nameof(Zoom), 1f);

    public static readonly StyledProperty<Vector2> PanProperty =
        AvaloniaProperty.Register<LaserOverlayCanvas, Vector2>(nameof(Pan));

    static LaserOverlayCanvas()
    {
        RemoteLasersProperty.Changed.AddClassHandler<LaserOverlayCanvas>((overlay, _) => overlay.OnLaserStateChanged());
        LocalLaserProperty.Changed.AddClassHandler<LaserOverlayCanvas>((overlay, _) => overlay.OnLaserStateChanged());
        LocalPingProperty.Changed.AddClassHandler<LaserOverlayCanvas>((overlay, _) => overlay.OnLaserStateChanged());
        ZoomProperty.Changed.AddClassHandler<LaserOverlayCanvas>((overlay, e) => overlay.OnTransformChanged((float)e.NewValue!));
        PanProperty.Changed.AddClassHandler<LaserOverlayCanvas>((overlay, e) => overlay.OnTransformChanged((Vector2)e.NewValue!));
    }

    public LaserOverlayCanvas()
    {
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        IsHitTestVisible = false;
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

    public float Zoom
    {
        get => _zoom;
        set => SetValue(ZoomProperty, value);
    }

    public Vector2 Pan
    {
        get => _pan;
        set => SetValue(PanProperty, value);
    }

    private void OnTransformChanged(float zoom)
    {
        if (_zoom == zoom)
            return;

        _zoom = zoom;
        _renderGeneration++;
        InvalidateVisual();
    }

    private void OnTransformChanged(Vector2 pan)
    {
        if (_pan == pan)
            return;

        _pan = pan;
        _renderGeneration++;
        InvalidateVisual();
    }

    private void OnLaserStateChanged()
    {
        _renderGeneration++;
        InvalidateVisual();
        UpdateLaserFadeTimer();
    }

    private void UpdateLaserFadeTimer()
    {
        UpdateLaserFadeTimer(Environment.TickCount64);
    }

    private void UpdateLaserFadeTimer(long now)
    {
        var hasVisible = LaserTrailRenderer.HasVisibleTrails(RemoteLasers, now)
            || LaserTrailRenderer.HasVisibleActiveRemoteLaser(RemoteLasers, now, RemoteLaserStaleTimeoutMs)
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
        var releasedStaleRemoteLaser = ReleaseStaleRemoteLasers(now);
        var hasVisible = LaserTrailRenderer.HasVisibleTrails(RemoteLasers, now)
            || LaserTrailRenderer.HasVisibleActiveRemoteLaser(RemoteLasers, now, RemoteLaserStaleTimeoutMs)
            || LaserTrailRenderer.HasVisibleLocalLaser(LocalLaser, now)
            || LaserTrailRenderer.HasVisiblePing(LocalPing, now);

        if (!hasVisible)
        {
            _laserFadeTimer?.Stop();
            _laserFadeTimer = null;
            return;
        }

        if (releasedStaleRemoteLaser || hasVisible)
        {
            _renderGeneration++;
            InvalidateVisual();
        }
    }

    private bool ReleaseStaleRemoteLasers(long now)
    {
        if (RemoteLasers is null || RemoteLasers.Count == 0)
            return false;

        var changed = false;
        foreach (var state in RemoteLasers.Values)
        {
            if (!state.IsActive)
                continue;

            if (now - state.LastUpdateMs < RemoteLaserStaleTimeoutMs)
                continue;

            state.IsActive = false;

            var points = state.Trail.GetPoints();
            if (points.Length > 0)
            {
                var last = points[^1];
                state.Trail.UpdateLast(last.Position, now);
            }

            changed = true;
        }

        return changed;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _laserFadeTimer?.Stop();
        _laserFadeTimer = null;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        context.Custom(new LaserOverlayDrawOperation(this, bounds, _zoom, _pan));
    }

    private void RenderOverlay(SKCanvas canvas, long now)
    {
        canvas.Save();
        canvas.Translate(_pan.X, _pan.Y);
        canvas.Scale(_zoom, _zoom);

        try
        {
            LaserTrailRenderer.DrawLaserTrails(canvas, RemoteLasers, now, _zoom);
            LaserTrailRenderer.DrawLocalLaser(canvas, LocalLaser, now, _zoom);
            LaserTrailRenderer.DrawPingMarker(canvas, LocalPing, now, _zoom);
        }
        finally
        {
            canvas.Restore();
        }
    }

    private sealed class LaserOverlayDrawOperation : ICustomDrawOperation
    {
        private readonly LaserOverlayCanvas _owner;
        private readonly float _zoom;
        private readonly Vector2 _pan;
        private readonly long _renderGeneration;
        private readonly IReadOnlyDictionary<Guid, RemoteLaserState>? _remoteLasers;
        private readonly LocalLaserState? _localLaser;
        private readonly PingMarkerState? _localPing;

        public LaserOverlayDrawOperation(LaserOverlayCanvas owner, Rect bounds, float zoom, Vector2 pan)
        {
            _owner = owner;
            Bounds = bounds;
            _zoom = zoom;
            _pan = pan;
            _renderGeneration = owner._renderGeneration;
            _remoteLasers = owner.RemoteLasers;
            _localLaser = owner.LocalLaser;
            _localPing = owner.LocalPing;
        }

        public Rect Bounds { get; }

        public void Dispose()
        {
        }

        public bool Equals(ICustomDrawOperation? other)
        {
            return other is LaserOverlayDrawOperation operation
                && Bounds.Equals(operation.Bounds)
                && ReferenceEquals(_owner, operation._owner)
                && _zoom == operation._zoom
                && _pan == operation._pan
                && _renderGeneration == operation._renderGeneration
                && ReferenceEquals(_remoteLasers, operation._remoteLasers)
                && ReferenceEquals(_localLaser, operation._localLaser)
                && Equals(_localPing, operation._localPing);
        }

        public bool HitTest(Point p) => false;

        public void Render(ImmediateDrawingContext drawingContext)
        {
            var leaseFeature = drawingContext.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as ISkiaSharpApiLeaseFeature;
            if (leaseFeature is null)
                return;

            using var lease = leaseFeature.Lease();
            var canvas = lease?.SkCanvas;
            if (canvas is null)
                return;

            RenderOverlay(canvas, Environment.TickCount64);
        }

        private void RenderOverlay(SKCanvas canvas, long now)
        {
            canvas.Save();
            canvas.Translate(_pan.X, _pan.Y);
            canvas.Scale(_zoom, _zoom);

            try
            {
                LaserTrailRenderer.DrawLaserTrails(canvas, _remoteLasers, now, _zoom);
                LaserTrailRenderer.DrawLocalLaser(canvas, _localLaser, now, _zoom);
                LaserTrailRenderer.DrawPingMarker(canvas, _localPing, now, _zoom);
            }
            finally
            {
                canvas.Restore();
            }
        }
    }
}
