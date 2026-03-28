using System.Numerics;
using BFGA.Canvas.Rendering;
using SkiaSharp;

namespace BFGA.Core.Tests;

public class DotGridRenderingTests
{
    [Fact]
    public void GetDotPositions_AnchorsGridToOriginAndSpacing()
    {
        var dots = DotGridHelper.GetDotPositions(new SKRect(0, 0, 100, 100), new Vector2(50, 50), 20f).ToList();

        Assert.Contains(new Vector2(50, 50), dots);
        Assert.Contains(new Vector2(30, 50), dots);
        Assert.Contains(new Vector2(50, 30), dots);
        Assert.Contains(new Vector2(70, 70), dots);
    }

    [Fact]
    public void GetDotPositions_UsesVisibleBoundsOnly()
    {
        var visible = DotGridHelper.GetDotPositions(new SKRect(200, 200, 260, 260), new Vector2(0, 0), 20f).ToList();
        var full = DotGridHelper.GetDotPositions(new SKRect(0, 0, 200_000, 200_000), new Vector2(0, 0), 20f).ToList();

        Assert.True(visible.Count < full.Count);
        Assert.All(visible, dot =>
        {
            Assert.InRange(dot.X, 200f, 260f);
            Assert.InRange(dot.Y, 200f, 260f);
        });
    }

    [Fact]
    public void GetVisibleDotRadius_ScalesWithZoom()
    {
        Assert.Equal(1.25f, DotGridHelper.GetVisibleDotRadius(1.25f, 1f));
        Assert.Equal(0.625f, DotGridHelper.GetVisibleDotRadius(1.25f, 2f));
    }

    [Fact]
    public void DrawDots_UsesVisibleBoundsOnly()
    {
        var visibleBounds = DotGridHelper.GetVisibleBoardBounds(new SKRect(300, 300, 500, 500));
        var visibleDots = DotGridHelper.GetDotPositions(visibleBounds, Vector2.Zero, 20f, 2f).ToList();
        var fullDots = DotGridHelper.GetDotPositions(new SKRect(0, 0, 200_000, 200_000), Vector2.Zero, 20f, 2f).ToList();

        Assert.True(visibleDots.Count < fullDots.Count / 1000);
        Assert.All(visibleDots, dot =>
        {
            Assert.InRange(dot.X, 300f, 500f);
            Assert.InRange(dot.Y, 300f, 500f);
        });
    }

    [Fact]
    public void GetVisibleBoardBounds_UsesClipSpaceWithoutDoubleScaling()
    {
        var bounds = DotGridHelper.GetVisibleBoardBounds(new SKRect(200, 300, 600, 700));

        Assert.Equal(200f, bounds.Left);
        Assert.Equal(300f, bounds.Top);
        Assert.Equal(600f, bounds.Right);
        Assert.Equal(700f, bounds.Bottom);
    }
}
