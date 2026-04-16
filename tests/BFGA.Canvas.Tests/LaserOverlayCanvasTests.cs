using System.Numerics;
using System.Reflection;
using Avalonia;
using BFGA.Canvas.Rendering;
using SkiaSharp;

namespace BFGA.Canvas.Tests;

public sealed class LaserOverlayCanvasTests
{
    [Fact]
    public void LaserOverlayCanvas_VisibleLaser_StartsFadeTimer()
    {
        // Arrange
        var overlay = new LaserOverlayCanvas();
        var now = Environment.TickCount64;
        var remoteLaser = CreateRemoteLaserState(SKColors.DeepPink, new Vector2(12f, 18f), timestampMs: now, isActive: true);

        // Act
        overlay.RemoteLasers = new Dictionary<Guid, RemoteLaserState>
        {
            [Guid.NewGuid()] = remoteLaser
        };

        // Assert
        Assert.NotNull(GetLaserFadeTimer(overlay));
    }

    [Fact]
    public void LaserOverlayCanvas_ExpiredLaser_StopsFadeTimer()
    {
        // Arrange
        var overlay = new LaserOverlayCanvas();
        var expiredTimestamp = Environment.TickCount64 - 1600;
        overlay.RemoteLasers = new Dictionary<Guid, RemoteLaserState>
        {
            [Guid.NewGuid()] = CreateRemoteLaserState(SKColors.DeepPink, new Vector2(12f, 18f), timestampMs: expiredTimestamp, isActive: false)
        };

        // Act
        InvokeNonPublic(overlay, "UpdateLaserFadeTimer", new[] { typeof(long) }, Environment.TickCount64);

        // Assert
        Assert.Null(GetLaserFadeTimer(overlay));
    }

    [Fact]
    public void LaserOverlayCanvas_RenderPath_UsesLaserTrailRendererOnly()
    {
        // Arrange
        var overlay = new LaserOverlayCanvas
        {
            Zoom = 1f,
            Pan = new Vector2(0f, 0f),
            LocalLaser = CreateLocalLaserState(SKColors.LimeGreen, new Vector2(20f, 20f), timestampMs: Environment.TickCount64)
        };
        overlay.Measure(new Size(64, 64));
        overlay.Arrange(new Rect(0, 0, 64, 64));

        using var bitmap = new SKBitmap(64, 64, true);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        // Act
        InvokeNonPublic(overlay, "RenderOverlay", new[] { typeof(SKCanvas), typeof(long) }, canvas, Environment.TickCount64);
        canvas.Flush();

        // Assert
        Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha > 0);
        Assert.Equal((byte)0, bitmap.GetPixel(0, 0).Alpha);
    }

    [Fact]
    public void LaserOverlayCanvas_StaleActiveRemoteLaser_BecomesInactiveAfterThreeSeconds()
    {
        var overlay = new LaserOverlayCanvas();
        var staleTimestamp = 1_000L;
        var state = CreateRemoteLaserState(SKColors.DeepPink, new Vector2(12f, 18f), timestampMs: staleTimestamp, isActive: true);
        overlay.RemoteLasers = new Dictionary<Guid, RemoteLaserState>
        {
            [Guid.NewGuid()] = state
        };

        InvokeNonPublic(overlay, "OnLaserFadeTick", new[] { typeof(object), typeof(EventArgs) }, null!, EventArgs.Empty);

        Assert.False(state.IsActive);
    }

    [Fact]
    public void LaserOverlayCanvas_StaleTimeout_RefreshesLastPointForFadeOut()
    {
        var overlay = new LaserOverlayCanvas();
        var staleTimestamp = 1_000L;
        var state = CreateRemoteLaserState(SKColors.DeepPink, new Vector2(12f, 18f), timestampMs: staleTimestamp, isActive: true);
        overlay.RemoteLasers = new Dictionary<Guid, RemoteLaserState>
        {
            [Guid.NewGuid()] = state
        };

        InvokeNonPublic(overlay, "ReleaseStaleRemoteLasers", new[] { typeof(long) }, 4_500L);

        var points = state.Trail.GetPoints().ToArray();
        Assert.Single(points);
        Assert.Equal(4_500L, points[0].TimestampMs);
    }

    [Fact]
    public void LaserOverlayCanvas_FreshRemoteLaser_RemainsActive()
    {
        var overlay = new LaserOverlayCanvas();
        var state = CreateRemoteLaserState(SKColors.DeepPink, new Vector2(12f, 18f), timestampMs: 3_000L, isActive: true);
        overlay.RemoteLasers = new Dictionary<Guid, RemoteLaserState>
        {
            [Guid.NewGuid()] = state
        };

        var changed = (bool)(InvokeNonPublic(overlay, "ReleaseStaleRemoteLasers", new[] { typeof(long) }, 5_500L) ?? false);

        Assert.False(changed);
        Assert.True(state.IsActive);
        Assert.Equal(3_000L, state.Trail.GetPoints().ToArray()[0].TimestampMs);
    }

    private static LocalLaserState CreateLocalLaserState(SKColor color, Vector2 head, long timestampMs)
    {
        var state = new LocalLaserState(color)
        {
            IsActive = true,
            HeadPosition = head,
            LastUpdateMs = timestampMs
        };

        state.Trail.Add(head, timestampMs);
        return state;
    }

    private static RemoteLaserState CreateRemoteLaserState(SKColor color, Vector2 head, long timestampMs, bool isActive)
    {
        var state = new RemoteLaserState(color)
        {
            IsActive = isActive,
            LastUpdateMs = timestampMs
        };

        state.Trail.Add(head, timestampMs);
        return state;
    }

    private static object? GetLaserFadeTimer(LaserOverlayCanvas overlay)
    {
        return typeof(LaserOverlayCanvas)
            .GetField("_laserFadeTimer", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(overlay);
    }

    private static object? InvokeNonPublic(object target, string methodName, Type[] parameterTypes, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic, null, parameterTypes, null);
        Assert.NotNull(method);
        return method!.Invoke(target, args);
    }
}
