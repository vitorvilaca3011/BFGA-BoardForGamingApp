using System.Numerics;
using BFGA.Canvas.Rendering;
using SkiaSharp;

namespace BFGA.Canvas.Tests;

public sealed class LocalLaserRendererTests
{
    [Fact]
    public void HasVisibleLocalLaser_ReturnsTrueForActiveLaserWithFreshPoint()
    {
        // Arrange
        var laser = new LocalLaserState(SKColors.DeepPink)
        {
            IsActive = true,
            HeadPosition = new Vector2(10f, 12f),
            LastUpdateMs = 1000
        };
        laser.Trail.Add(laser.HeadPosition, 1000);

        // Act
        bool visible = LaserTrailRenderer.HasVisibleLocalLaser(laser, 1000);

        // Assert
        Assert.True(visible);
    }

    [Fact]
    public void HasVisibleLocalLaser_ReturnsFalseWhenEndedLaserOnlyHasExpiredPoints()
    {
        // Arrange
        var laser = new LocalLaserState(SKColors.DeepPink)
        {
            IsActive = false,
            LastUpdateMs = 1000
        };
        laser.Trail.Add(new Vector2(10f, 12f), 1000);

        // Act
        bool visible = LaserTrailRenderer.HasVisibleLocalLaser(laser, 2500);

        // Assert
        Assert.False(visible);
    }

    [Fact]
    public void HasVisiblePing_RemainsVisibleForLifetimeThenDisappears()
    {
        // Arrange
        var ping = new PingMarkerState(new Vector2(20f, 30f), 1000, SKColors.DeepPink);

        // Act
        bool visibleDuringLifetime = LaserTrailRenderer.HasVisiblePing(ping, 3399);
        bool visibleAfterLifetime = LaserTrailRenderer.HasVisiblePing(ping, 3400);

        // Assert
        Assert.True(visibleDuringLifetime);
        Assert.False(visibleAfterLifetime);
    }

    [Fact]
    public void GetWorldSize_ConvertsScreenPixelsUsingZoom()
    {
        // Arrange
        const float screenSize = 5f;
        const float zoom = 2f;

        // Act
        float worldSize = LaserTrailRenderer.GetWorldSize(screenSize, zoom);

        // Assert
        Assert.Equal(2.5f, worldSize, 3);
    }

    [Fact]
    public void GetPingRingScreenRadius_InterpolatesAcrossLifetime()
    {
        // Arrange
        const long startedAtMs = 1000;

        // Act
        float startRadius = LaserTrailRenderer.GetPingRingScreenRadius(startedAtMs, startedAtMs);
        float endRadius = LaserTrailRenderer.GetPingRingScreenRadius(startedAtMs, 3400);

        // Assert
        Assert.Equal(10f, startRadius, 3);
        Assert.Equal(24f, endRadius, 3);
    }
}
