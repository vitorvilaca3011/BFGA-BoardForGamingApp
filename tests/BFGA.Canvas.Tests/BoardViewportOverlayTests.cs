using System.Numerics;
using System.Reflection;
using Avalonia.Controls;
using BFGA.Canvas.Rendering;
using SkiaSharp;

namespace BFGA.Canvas.Tests;

public sealed class BoardViewportOverlayTests
{
    [Fact]
    public void BoardViewport_UsesDedicatedLaserOverlayAboveBoardCanvas()
    {
        // Arrange
        var viewport = new BoardViewport();

        // Act
        var host = Assert.IsType<Grid>(viewport.Child);

        // Assert
        Assert.Collection(
            host.Children,
            child => Assert.Same(viewport.Canvas, child),
            child => Assert.IsType<LaserOverlayCanvas>(child));
        Assert.Equal(1, host.Children[1].GetValue(Panel.ZIndexProperty));
    }

    [Fact]
    public void BoardViewport_RemoteLaserUpdates_DoNotAdvanceBoardCanvasRenderGeneration()
    {
        // Arrange
        var viewport = new BoardViewport();
        var before = GetRenderGeneration(viewport.Canvas);
        var lasers = new Dictionary<Guid, RemoteLaserState>
        {
            [Guid.NewGuid()] = CreateRemoteLaserState()
        };

        // Act
        viewport.RemoteLasers = lasers;

        // Assert
        Assert.Equal(before, GetRenderGeneration(viewport.Canvas));
        Assert.Same(lasers, GetLaserOverlay(viewport).RemoteLasers);
    }

    [Fact]
    public void BoardViewport_SyncCanvasState_UpdatesLaserOverlayTransforms()
    {
        // Arrange
        var viewport = new BoardViewport();
        var expectedPan = new Vector2(45f, 90f);

        // Act
        viewport.Zoom = 1.5;
        viewport.Pan = expectedPan;

        // Assert
        var overlay = GetLaserOverlay(viewport);
        Assert.Equal(1.5f, viewport.Canvas.Zoom, 3);
        Assert.Equal(expectedPan, viewport.Canvas.Pan);
        Assert.Equal(1.5f, overlay.Zoom, 3);
        Assert.Equal(expectedPan, overlay.Pan);
    }

    private static LaserOverlayCanvas GetLaserOverlay(BoardViewport viewport)
    {
        var field = typeof(BoardViewport).GetField("_laserOverlay", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<LaserOverlayCanvas>(field!.GetValue(viewport));
    }

    private static long GetRenderGeneration(BoardCanvas canvas)
    {
        var property = typeof(BoardCanvas).GetProperty("RenderGeneration", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsType<long>(property!.GetValue(canvas));
    }

    private static RemoteLaserState CreateRemoteLaserState()
    {
        var timestampMs = Environment.TickCount64;
        var state = new RemoteLaserState(SKColors.DeepPink)
        {
            IsActive = true,
            LastUpdateMs = timestampMs
        };

        state.Trail.Add(new Vector2(10f, 20f), timestampMs);
        return state;
    }
}
