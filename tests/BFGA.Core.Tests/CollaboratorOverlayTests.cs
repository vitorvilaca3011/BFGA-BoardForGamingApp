using System.Numerics;
using BFGA.Canvas.Rendering;
using SkiaSharp;

namespace BFGA.Core.Tests;

public class CollaboratorOverlayTests
{
    [Fact]
    public void GetRemoteCursorArrowPoints_StartsAtPointerAndPointsForward()
    {
        var position = new Vector2(40f, 60f);

        var points = CollaboratorOverlayHelper.GetRemoteCursorArrowPoints(position);

        Assert.Equal(new SKPoint(40f, 60f), points[0]);
        Assert.Contains(points, point => point.X > position.X);
        Assert.Contains(points, point => point.Y > position.Y);
    }

    [Fact]
    public void GetRemoteCursorLabelRect_SitsToRightOfPointer()
    {
        using var font = new SKFont { Size = 12f };

        var rect = CollaboratorOverlayHelper.GetRemoteCursorLabelRect(new Vector2(20f, 30f), "Remote", font);

        Assert.True(rect.Left > 20f);
        Assert.True(rect.Top < 30f);
        Assert.True(rect.Bottom < 30f);
        Assert.True(rect.Right > rect.Left);
    }

    [Fact]
    public void GetRemoteStrokePreviewColor_UsesSixtyPercentOpacity()
    {
        var color = CollaboratorOverlayHelper.GetRemoteStrokePreviewColor(SKColors.CornflowerBlue);

        Assert.Equal((byte)153, color.Alpha);
        Assert.Equal(SKColors.CornflowerBlue.Red, color.Red);
        Assert.Equal(SKColors.CornflowerBlue.Green, color.Green);
        Assert.Equal(SKColors.CornflowerBlue.Blue, color.Blue);
    }

    [Fact]
    public void DrawRemoteCursor_PaintsArrowAndLabelPixels()
    {
        using var bitmap = new SKBitmap(120, 90);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var cursor = new RemoteCursorState(Guid.NewGuid(), "Remote", SKColors.Red, new Vector2(20f, 40f));

        CollaboratorOverlayHelper.DrawRemoteCursor(canvas, cursor);

        Assert.NotEqual(SKColors.Transparent, bitmap.GetPixel(20, 40));
        Assert.NotEqual(SKColors.Transparent, bitmap.GetPixel(31, 48));
        Assert.NotEqual(SKColors.Transparent, bitmap.GetPixel(33, 20));
    }

    [Fact]
    public void DrawRemoteStrokePreview_PaintsWithSixtyPercentAlpha()
    {
        using var bitmap = new SKBitmap(80, 80);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var preview = new RemoteStrokePreviewState(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Remote",
            SKColors.Blue,
            new[] { new Vector2(10f, 10f), new Vector2(60f, 60f) });

        CollaboratorOverlayHelper.DrawRemoteStrokePreview(canvas, preview);

        var pixel = bitmap.GetPixel(35, 35);

        Assert.NotEqual(SKColors.Transparent, pixel);
        Assert.Equal((byte)153, pixel.Alpha);
    }
}
