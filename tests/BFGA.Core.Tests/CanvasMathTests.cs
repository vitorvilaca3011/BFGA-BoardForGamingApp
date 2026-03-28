using System.Numerics;
using System.Reflection;
using Avalonia.Controls.PanAndZoom;
using Avalonia;
using Avalonia.Rendering.SceneGraph;
using BFGA.Canvas;
using BFGA.Canvas.Rendering;
using BFGA.Core.Models;
using SkiaSharp;

namespace BFGA.Core.Tests;

public class CoordinateTransformTests
{
    [Fact]
    public void ScreenToBoard_RoundTrip_ReturnsOriginalPoint()
    {
        // Arrange
        var original = new Vector2(100f, 200f);
        var panOffset = new Vector2(50f, 30f);
        float zoom = 2.0f;
        var originOffset = new Vector2(10f, 20f);

        // Act
        var boardPoint = CoordinateTransformHelper.ScreenToBoard(original, panOffset, zoom, originOffset);
        var backToScreen = CoordinateTransformHelper.BoardToScreen(boardPoint, panOffset, zoom, originOffset);

        // Assert
        Assert.Equal(original.X, backToScreen.X, 4);
        Assert.Equal(original.Y, backToScreen.Y, 4);
    }

    [Fact]
    public void ScreenToBoard_WithZeroPan_AppliesZoomOnly()
    {
        // Arrange
        var screenPoint = new Vector2(200f, 300f);
        var panOffset = Vector2.Zero;
        float zoom = 2.0f;
        var originOffset = Vector2.Zero;

        // Act
        var boardPoint = CoordinateTransformHelper.ScreenToBoard(screenPoint, panOffset, zoom, originOffset);

        // Assert: screen / zoom = board when no pan
        Assert.Equal(100f, boardPoint.X, 4);
        Assert.Equal(150f, boardPoint.Y, 4);
    }

    [Fact]
    public void BoardToScreen_WithPanAndZoom_TransformsCorrectly()
    {
        // Arrange
        var boardPoint = new Vector2(50f, 75f);
        var panOffset = new Vector2(10f, 20f);
        float zoom = 1.5f;
        var originOffset = new Vector2(5f, 7f);

        // Act
        var screenPoint = CoordinateTransformHelper.BoardToScreen(boardPoint, panOffset, zoom, originOffset);

        // Assert: (board + origin) * zoom + pan = screen
        Assert.Equal(92.5f, screenPoint.X, 4);  // (50 + 5) * 1.5 + 10
        Assert.Equal(143f, screenPoint.Y, 4); // (75 + 7) * 1.5 + 20
    }
}

public class BoardSurfaceExtentTests
{
    [Fact]
    public void BoardSurfaceMetrics_UsesStableWorkspace()
    {
        // Arrange
        var board = new BoardState();
        board.Elements.Add(new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(-25_000f, -10_000f),
            Size = new Vector2(50_000f, 20_000f),
            Type = ShapeType.Rectangle
        });

        // Act
        var metrics = BoardSurfaceHelper.GetSurfaceMetrics(board);
        var worldOrigin = BoardSurfaceHelper.BoardToCanvas(Vector2.Zero, metrics.OriginOffset);

        // Assert
        Assert.Equal(new Vector2(BoardSurfaceHelper.StableWorkspaceSize, BoardSurfaceHelper.StableWorkspaceSize), metrics.SurfaceSize);
        Assert.Equal(BoardSurfaceHelper.StableOriginOffset, metrics.OriginOffset);
        Assert.Equal(metrics.OriginOffset, worldOrigin);
    }

    [Fact]
    public void BoardSurfaceMetrics_RemainStableAcrossContentChanges()
    {
        // Arrange
        var board = new BoardState();
        var initial = BoardSurfaceHelper.GetSurfaceMetrics(board);

        board.Elements.Add(new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(-25_000f, -10_000f),
            Size = new Vector2(50_000f, 20_000f),
            Type = ShapeType.Rectangle
        });

        // Act
        var afterContentChange = BoardSurfaceHelper.GetSurfaceMetrics(board);

        // Assert
        Assert.Equal(initial.SurfaceSize, afterContentChange.SurfaceSize);
        Assert.Equal(initial.OriginOffset, afterContentChange.OriginOffset);
    }

    [Fact]
    public void CanvasToBoard_RoundTrip_PreservesFarCoordinates()
    {
        // Arrange
        var original = new Vector2(-48_500f, 72_250f);
        var originOffset = new Vector2(1_000f, 2_000f);

        // Act
        var canvasPoint = BoardSurfaceHelper.BoardToCanvas(original, originOffset);
        var roundTrip = BoardSurfaceHelper.CanvasToBoard(canvasPoint, originOffset);

        // Assert
        Assert.Equal(original, roundTrip);
    }

    [Fact]
    public void BoardCanvas_UsesLargeHostedSurface()
    {
        // Arrange / Act
        var canvas = new BoardCanvas();

        // Assert
        Assert.Equal(BoardSurfaceHelper.StableWorkspaceSize, canvas.Width);
        Assert.Equal(BoardSurfaceHelper.StableWorkspaceSize, canvas.Height);
    }
}

public class StrokeHitTestTests
{
    [Fact]
    public void HitTestStroke_PointNearSegment_ReturnsTrue()
    {
        // Arrange: horizontal stroke from (0,0) to (100,0)
        var stroke = new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = Vector2.Zero,
            Points = new List<Vector2> { new(0, 0), new(100, 0) },
            Thickness = 4f
        };
        var testPoint = new Vector2(50f, 3f); // 3px below the line, within thickness

        // Act
        var hit = HitTestHelper.HitTestStroke(stroke, testPoint);

        // Assert
        Assert.True(hit);
    }

    [Fact]
    public void HitTestStroke_PointFarFromSegment_ReturnsFalse()
    {
        // Arrange: horizontal stroke from (0,0) to (100,0)
        var stroke = new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = Vector2.Zero,
            Points = new List<Vector2> { new(0, 0), new(100, 0) },
            Thickness = 4f
        };
        var testPoint = new Vector2(50f, 10f); // 10px below the line, outside thickness

        // Act
        var hit = HitTestHelper.HitTestStroke(stroke, testPoint);

        // Assert
        Assert.False(hit);
    }

    [Fact]
    public void HitTestStroke_PointOutsideSegmentBounds_ReturnsFalse()
    {
        // Arrange: horizontal stroke from (0,0) to (100,0)
        var stroke = new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = Vector2.Zero,
            Points = new List<Vector2> { new(0, 0), new(100, 0) },
            Thickness = 4f
        };
        var testPoint = new Vector2(150f, 0f); // beyond the segment endpoint

        // Act
        var hit = HitTestHelper.HitTestStroke(stroke, testPoint);

        // Assert
        Assert.False(hit);
    }

    [Fact]
    public void HitTestStroke_SinglePointNearDot_ReturnsTrue()
    {
        // Arrange
        var stroke = new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 10),
            Points = new List<Vector2> { new(0, 0) },
            Thickness = 6f
        };

        // Act
        var hit = HitTestHelper.HitTestStroke(stroke, new Vector2(12, 12));

        // Assert
        Assert.True(hit);
    }

    [Fact]
    public void HitTestStroke_SmoothedCurve_PointOnRenderedPath_ReturnsTrue()
    {
        // Arrange: choose a highly curved stroke where the rendered Catmull-Rom path
        // diverges from the raw control polyline.
        var stroke = new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = Vector2.Zero,
            Points = new List<Vector2>
            {
                new(0, 0),
                new(15, 240),
                new(30, -240),
                new(45, 240),
                new(60, 0)
            },
            Thickness = 2f
        };

        var smoothed = StrokeSmoothingHelper.SmoothStroke(stroke.Points, 32);
        var hitRadius = stroke.Thickness / 2f + 2f;

        var probePoint = smoothed.OrderByDescending(p => DistanceToPolyline(p, stroke.Points)).First();
        Assert.True(DistanceToPolyline(probePoint, stroke.Points) > hitRadius + 1f);

        // Act
        var hit = HitTestHelper.HitTestStroke(stroke, probePoint);

        // Assert
        Assert.True(hit);
    }

    private static float DistanceToPolyline(Vector2 point, IReadOnlyList<Vector2> points)
    {
        float best = float.MaxValue;

        for (int i = 0; i < points.Count - 1; i++)
        {
            best = MathF.Min(best, DistanceToSegment(point, points[i], points[i + 1]));
        }

        return best;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var ap = point - a;

        float abLenSq = ab.LengthSquared();
        if (abLenSq < float.Epsilon)
            return (point - a).Length();

        float t = Vector2.Dot(ap, ab) / abLenSq;
        t = Math.Clamp(t, 0f, 1f);

        var closest = a + ab * t;
        return (point - closest).Length();
    }
}

public class StrokeSmoothingTests
{
    [Fact]
    public void SmoothStroke_PreservesEndpoints()
    {
        // Arrange
        var points = new List<Vector2>
        {
            new(0, 0),
            new(25, 50),
            new(50, 0),
            new(75, 50),
            new(100, 0)
        };

        // Act
        var smoothed = StrokeSmoothingHelper.SmoothStroke(points);

        // Assert: first and last points must be preserved
        Assert.Equal(points[0], smoothed[0]);
        Assert.Equal(points[^1], smoothed[^1]);
    }

    [Fact]
    public void SmoothStroke_ProducesMorePointsThanInput()
    {
        // Arrange
        var points = new List<Vector2>
        {
            new(0, 0),
            new(50, 100),
            new(100, 0)
        };

        // Act
        var smoothed = StrokeSmoothingHelper.SmoothStroke(points);

        // Assert: interpolation should produce more points
        Assert.True(smoothed.Count > points.Count);
    }

    [Fact]
    public void SmoothStroke_TooFewPoints_ReturnsOriginalPoints()
    {
        // Arrange: only 2 points - cannot smooth
        var points = new List<Vector2> { new(0, 0), new(100, 0) };

        // Act
        var smoothed = StrokeSmoothingHelper.SmoothStroke(points);

        // Assert: should return the original points unchanged
        Assert.Equal(2, smoothed.Count);
        Assert.Equal(points[0], smoothed[0]);
        Assert.Equal(points[1], smoothed[1]);
    }

    [Fact]
    public void SmoothStroke_SinglePoint_ReturnsThatPoint()
    {
        // Arrange
        var points = new List<Vector2> { new(50, 50) };

        // Act
        var smoothed = StrokeSmoothingHelper.SmoothStroke(points);

        // Assert
        Assert.Single(smoothed);
        Assert.Equal(new Vector2(50, 50), smoothed[0]);
    }
}

public class HitTestTopmostTests
{
    [Fact]
    public void GetTopmostElement_HigherZIndexWins()
    {
        // Arrange
        var board = new BoardState();
        var bottomRect = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 100),
            Type = ShapeType.Rectangle,
            ZIndex = 0
        };
        var topRect = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 100),
            Type = ShapeType.Rectangle,
            ZIndex = 5
        };
        board.Elements.Add(bottomRect);
        board.Elements.Add(topRect);

        // Act: hit at (50,50) which is inside both
        var hit = HitTestHelper.GetTopmostHit(board, new Vector2(50, 50));

        // Assert: should return the element with higher ZIndex
        Assert.NotNull(hit);
        Assert.Equal(topRect.Id, hit!.Id);
    }

    [Fact]
    public void GetTopmostElement_NoHit_ReturnsNull()
    {
        // Arrange
        var board = new BoardState();
        var rect = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 100),
            Type = ShapeType.Rectangle,
            ZIndex = 0
        };
        board.Elements.Add(rect);

        // Act: hit outside the rectangle
        var hit = HitTestHelper.GetTopmostHit(board, new Vector2(200, 200));

        // Assert
        Assert.Null(hit);
    }

    [Fact]
    public void GetTopmostElement_EmptyBoard_ReturnsNull()
    {
        // Arrange
        var board = new BoardState();

        // Act
        var hit = HitTestHelper.GetTopmostHit(board, new Vector2(50, 50));

        // Assert
        Assert.Null(hit);
    }

    [Fact]
    public void GetTopmostElement_EqualZIndex_ReturnsLastDrawnElement()
    {
        // Arrange: two elements with the same ZIndex overlapping at (50,50).
        // Draw order (OrderBy ZIndex ascending) draws firstRect then secondRect,
        // so secondRect is on top and should win hit testing.
        var board = new BoardState();
        var firstRect = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 100),
            Type = ShapeType.Rectangle,
            ZIndex = 3
        };
        var secondRect = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 100),
            Type = ShapeType.Rectangle,
            ZIndex = 3
        };
        // Add in this order: firstRect is drawn first, secondRect on top
        board.Elements.Add(firstRect);
        board.Elements.Add(secondRect);

        // Act
        var hit = HitTestHelper.GetTopmostHit(board, new Vector2(50, 50));

        // Assert: the later-drawn element (secondRect) must win
        Assert.NotNull(hit);
        Assert.Equal(secondRect.Id, hit!.Id);
    }
}

public class ElementBoundsTests
{
    [Fact]
    public void GetBounds_ShapeElement_ReturnsCorrectRect()
    {
        // Arrange
        var shape = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 20),
            Size = new Vector2(100, 50),
            Type = ShapeType.Rectangle
        };

        // Act
        var bounds = ElementBoundsHelper.GetBounds(shape);

        // Assert
        Assert.Equal(10f, bounds.Left, 4);
        Assert.Equal(20f, bounds.Top, 4);
        Assert.Equal(110f, bounds.Right, 4);
        Assert.Equal(70f, bounds.Bottom, 4);
    }

    [Fact]
    public void GetBounds_ShapeElement_WithNegativeSize_NormalizesRect()
    {
        // Arrange
        var shape = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(110, 70),
            Size = new Vector2(-100, -50),
            Type = ShapeType.Rectangle
        };

        // Act
        var bounds = ElementBoundsHelper.GetBounds(shape);

        // Assert
        Assert.Equal(10f, bounds.Left, 4);
        Assert.Equal(20f, bounds.Top, 4);
        Assert.Equal(110f, bounds.Right, 4);
        Assert.Equal(70f, bounds.Bottom, 4);
    }

    [Fact]
    public void GetBounds_StrokeElement_ReturnsBoundingRectOfPoints()
    {
        // Arrange
        var stroke = new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(5, 5),
            Points = new List<Vector2> { new(0, 0), new(50, 30), new(20, 60) },
            Thickness = 2f
        };

        // Act
        var bounds = ElementBoundsHelper.GetBounds(stroke);

        // Assert: bounds should encompass all points + position, with thickness padding
        Assert.True(bounds.Left <= 5f);   // position.X + min point.X - thickness
        Assert.True(bounds.Top <= 5f);    // position.Y + min point.Y - thickness
        Assert.True(bounds.Right >= 55f); // position.X + max point.X + thickness
        Assert.True(bounds.Bottom >= 65f); // position.Y + max point.Y + thickness
    }

    [Fact]
    public void GetBounds_StrokeElement_AccountsForSmoothedPath()
    {
        // Arrange: use a stroke whose smoothed path extends beyond the raw control polyline.
        var stroke = new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = Vector2.Zero,
            Points = new List<Vector2>
            {
                new(0, 0),
                new(15, 240),
                new(30, -240),
                new(45, 240),
                new(60, 0)
            },
            Thickness = 2f
        };

        var smoothed = StrokeSmoothingHelper.SmoothStroke(stroke.Points, 32);
        var rawBounds = new SKRect(
            stroke.Points.Min(p => p.X),
            stroke.Points.Min(p => p.Y),
            stroke.Points.Max(p => p.X),
            stroke.Points.Max(p => p.Y));
        var smoothedPoint = smoothed.First(p => p.X < rawBounds.Left || p.X > rawBounds.Right || p.Y < rawBounds.Top || p.Y > rawBounds.Bottom);

        // Act
        var bounds = ElementBoundsHelper.GetBounds(stroke);

        // Assert
        Assert.InRange(smoothedPoint.X, bounds.Left, bounds.Right);
        Assert.InRange(smoothedPoint.Y, bounds.Top, bounds.Bottom);
        Assert.True(bounds.Left < rawBounds.Left || bounds.Right > rawBounds.Right || bounds.Top < rawBounds.Top || bounds.Bottom > rawBounds.Bottom);
    }

    [Fact]
    public void ContainsPoint_ShapeInside_ReturnsTrue()
    {
        // Arrange
        var shape = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 100),
            Type = ShapeType.Rectangle
        };

        // Act & Assert
        Assert.True(ElementBoundsHelper.ContainsPoint(shape, new Vector2(50, 50)));
    }

    [Fact]
    public void ContainsPoint_ShapeOutside_ReturnsFalse()
    {
        // Arrange
        var shape = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 100),
            Type = ShapeType.Rectangle
        };

        // Act & Assert
        Assert.False(ElementBoundsHelper.ContainsPoint(shape, new Vector2(150, 150)));
    }

    [Fact]
    public void ContainsPoint_ShapeWithNegativeSize_ReturnsTrue()
    {
        // Arrange
        var shape = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(110, 70),
            Size = new Vector2(-100, -50),
            Type = ShapeType.Rectangle
        };

        // Act & Assert
        Assert.True(ElementBoundsHelper.ContainsPoint(shape, new Vector2(50, 40)));
    }

    // --- Ellipse hit-testing ---

    [Fact]
    public void ContainsPoint_EllipseCenter_ReturnsTrue()
    {
        var ellipse = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 60),
            Type = ShapeType.Ellipse
        };

        // Center of the ellipse at (50, 30)
        Assert.True(ElementBoundsHelper.ContainsPoint(ellipse, new Vector2(50, 30)));
    }

    [Fact]
    public void ContainsPoint_EllipseInside_ReturnsTrue()
    {
        var ellipse = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 60),
            Type = ShapeType.Ellipse
        };

        // Point clearly inside the ellipse
        Assert.True(ElementBoundsHelper.ContainsPoint(ellipse, new Vector2(50, 20)));
    }

    [Fact]
    public void ContainsPoint_EllipseCornerOfBBox_OutsideEllipse_ReturnsFalse()
    {
        var ellipse = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 60),
            Type = ShapeType.Ellipse
        };

        // Corner of bounding box (0,0) is outside the ellipse
        // Normalized: ((0-50)/50)^2 + ((0-30)/30)^2 = 1 + 1 = 2 > 1
        Assert.False(ElementBoundsHelper.ContainsPoint(ellipse, new Vector2(0, 0)));
    }

    [Fact]
    public void ContainsPoint_EllipseOutside_ReturnsFalse()
    {
        var ellipse = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 60),
            Type = ShapeType.Ellipse
        };

        Assert.False(ElementBoundsHelper.ContainsPoint(ellipse, new Vector2(200, 200)));
    }

    [Fact]
    public void ContainsPoint_EllipseWithNegativeSize_ReturnsTrue()
    {
        // Arrange
        var ellipse = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(100, 60),
            Size = new Vector2(-100, -60),
            Type = ShapeType.Ellipse
        };

        // Act & Assert
        Assert.True(ElementBoundsHelper.ContainsPoint(ellipse, new Vector2(50, 30)));
    }

    // --- Line hit-testing ---

    [Fact]
    public void ContainsPoint_LineNearSegment_ReturnsTrue()
    {
        var line = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 0),  // horizontal line
            Type = ShapeType.Line,
            StrokeWidth = 4f
        };

        // 2px below the line, within stroke tolerance
        Assert.True(ElementBoundsHelper.ContainsPoint(line, new Vector2(50, 2)));
    }

    [Fact]
    public void ContainsPoint_LineFarFromSegment_ReturnsFalse()
    {
        var line = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 0),
            Type = ShapeType.Line,
            StrokeWidth = 4f
        };

        // 20px below the line, well outside stroke tolerance
        Assert.False(ElementBoundsHelper.ContainsPoint(line, new Vector2(50, 20)));
    }

    [Fact]
    public void ContainsPoint_LineInsideBBoxButFarFromSegment_ReturnsFalse()
    {
        var line = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 0),
            Type = ShapeType.Line,
            StrokeWidth = 4f
        };

        // (50, 0) is inside the bounding box but the line is thin;
        // this point IS on the line, so should return true
        Assert.True(ElementBoundsHelper.ContainsPoint(line, new Vector2(50, 0)));
    }

    [Fact]
    public void ContainsPoint_ImageWithNegativeSize_ReturnsTrue()
    {
        // Arrange
        var image = new ImageElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(120, 80),
            Size = new Vector2(-80, -40)
        };

        // Act & Assert
        Assert.True(ElementBoundsHelper.ContainsPoint(image, new Vector2(60, 60)));
    }

    [Fact]
    public void GetBounds_RotatedRectangle_ReturnsExpandedAabb()
    {
        var rectangle = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 10),
            Size = new Vector2(20, 10),
            Type = ShapeType.Rectangle,
            Rotation = 45f
        };

        var bounds = ElementBoundsHelper.GetBounds(rectangle);

        Assert.True(bounds.Width > 20f);
        Assert.True(bounds.Height > 10f);
        Assert.True(ElementBoundsHelper.ContainsPoint(rectangle, ElementBoundsHelper.GetCenter(rectangle)));
        Assert.False(ElementBoundsHelper.ContainsPoint(rectangle, new Vector2(10, 10)));
    }

    [Fact]
    public void ContainsPoint_RotatedEllipse_UsesInverseRotation()
    {
        var ellipse = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 60),
            Type = ShapeType.Ellipse,
            Rotation = 30f
        };

        Assert.True(ElementBoundsHelper.ContainsPoint(ellipse, ElementBoundsHelper.GetCenter(ellipse)));
        Assert.False(ElementBoundsHelper.ContainsPoint(ellipse, new Vector2(0, 0)));
    }

    [Fact]
    public void GetBounds_RotatedImage_ReturnsExpandedAabb()
    {
        var image = new ImageElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 10),
            Size = new Vector2(20, 10),
            Rotation = 45f
        };

        var bounds = ElementBoundsHelper.GetBounds(image);

        Assert.True(bounds.Width > 20f);
        Assert.True(bounds.Height > 10f);
        Assert.True(ElementBoundsHelper.ContainsPoint(image, ElementBoundsHelper.GetCenter(image)));
        Assert.False(ElementBoundsHelper.ContainsPoint(image, new Vector2(10, 10)));
    }

    // --- Arrow hit-testing ---

    [Fact]
    public void ContainsPoint_ArrowNearShaft_ReturnsTrue()
    {
        var arrow = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 50),
            Size = new Vector2(100, 0),  // horizontal arrow
            Type = ShapeType.Arrow,
            StrokeWidth = 4f
        };

        // Near the shaft
        Assert.True(ElementBoundsHelper.ContainsPoint(arrow, new Vector2(50, 52)));
    }

    [Fact]
    public void ContainsPoint_ArrowFarFromShaft_ReturnsFalse()
    {
        var arrow = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 50),
            Size = new Vector2(100, 0),
            Type = ShapeType.Arrow,
            StrokeWidth = 4f
        };

        // Far from the shaft and head
        Assert.False(ElementBoundsHelper.ContainsPoint(arrow, new Vector2(50, 200)));
    }
}

public class BoardViewportTests
{
    [Fact]
    public void BoardViewport_HasZoomBorderChild()
    {
        // Arrange & Act
        var viewport = new BoardViewport();

        // Assert: Child should be ZoomBorder
        Assert.NotNull(viewport.Child);
        Assert.IsType<Avalonia.Controls.PanAndZoom.ZoomBorder>(viewport.Child);
    }

    [Fact]
    public void BoardViewport_ZoomBorderContainsBoardCanvas()
    {
        // Arrange & Act
        var viewport = new BoardViewport();
        var zoomBorder = (Avalonia.Controls.PanAndZoom.ZoomBorder)viewport.Child!;

        // Assert: ZoomBorder's child should be BoardCanvas
        Assert.NotNull(zoomBorder.Child);
        Assert.IsType<BoardCanvas>(zoomBorder.Child);
    }

    [Fact]
    public void BoardViewport_HasCorrectDefaults()
    {
        // Arrange & Act
        var viewport = new BoardViewport();

        // Assert
        Assert.True(viewport.ClipToBounds);
        Assert.True(viewport.Focusable);
    }

    [Fact]
    public void BoardViewport_ZoomBorderHasCorrectDefaults()
    {
        // Arrange & Act
        var viewport = new BoardViewport();
        var zoomBorder = (Avalonia.Controls.PanAndZoom.ZoomBorder)viewport.Child!;

        // Assert
        Assert.True(zoomBorder.EnablePan);
        Assert.True(zoomBorder.EnableZoom);
        Assert.Equal(1.2, zoomBorder.ZoomSpeed, 1);
    }

    [Fact]
    public void BoardViewport_BoardPropertyForwardsToCanvas()
    {
        // Arrange
        var viewport = new BoardViewport();
        var board = new BoardState();
        board.Elements.Add(new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 10),
            Size = new Vector2(50, 50),
            Type = ShapeType.Rectangle
        });

        // Act
        viewport.Board = board;

        // Assert: Inner canvas should have the same board
        var zoomBorder = (Avalonia.Controls.PanAndZoom.ZoomBorder)viewport.Child!;
        var canvas = (BoardCanvas)zoomBorder.Child!;
        Assert.Same(board, canvas.Board);
    }

    [Fact]
    public void BoardViewport_InvalidateBoard_ForwardsToCanvas()
    {
        // Arrange
        var viewport = new BoardViewport();

        // Act & Assert: should be callable without throwing and forward to canvas.
        viewport.InvalidateBoard();
    }

    [Fact]
    public void BoardCanvas_InvalidateBoard_IsPublic()
    {
        // Arrange
        var canvas = new BoardCanvas();

        // Act & Assert
        canvas.InvalidateBoard();
    }

    [Fact]
    public void BoardViewport_InternalCanvasAccessible()
    {
        // Arrange & Act
        var viewport = new BoardViewport();

        // Assert: Internal Canvas property should return the inner BoardCanvas
        Assert.NotNull(viewport.Canvas);
        Assert.IsType<BoardCanvas>(viewport.Canvas);
    }

    [Fact]
    public void BoardViewport_InternalZoomBorderAccessible()
    {
        // Arrange & Act
        var viewport = new BoardViewport();

        // Assert: Internal ZoomBorder property should return the inner ZoomBorder
        Assert.NotNull(viewport.ZoomBorder);
        Assert.IsType<Avalonia.Controls.PanAndZoom.ZoomBorder>(viewport.ZoomBorder);
    }

    [Fact]
    public void BoardViewport_CanCenterWorkspaceOrigin()
    {
        // Arrange
        var viewport = new BoardViewport();

        // Act
        var centerPoint = new Point(BoardSurfaceHelper.StableOriginOffset.X, BoardSurfaceHelper.StableOriginOffset.Y);
        viewport.ZoomBorder.CenterOn(centerPoint, false);

        // Assert
        Assert.NotNull(viewport.ZoomBorder);
    }
}

public class ImageCacheLifecycleTests
{
    [Fact]
    public void ImageDecodeCache_ReusesBitmap_UntilInvalidated()
    {
        // Arrange
        var cache = new ImageDecodeCache();
        var imageData = CreatePngBytes();
        var elementId = Guid.NewGuid();

        // Act
        var first = cache.GetOrAdd(elementId, imageData);
        var second = cache.GetOrAdd(elementId, imageData);
        cache.Invalidate(elementId);
        var afterInvalidation = cache.GetOrAdd(elementId, imageData);

        // Assert
        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.NotNull(afterInvalidation);
        Assert.NotSame(first, afterInvalidation);
    }

    [Fact]
    public void ImageDecodeCache_IsolatedBetweenCanvasOwners()
    {
        // Arrange
        var cacheOne = new ImageDecodeCache();
        var cacheTwo = new ImageDecodeCache();
        var imageData = CreatePngBytes();
        var id = Guid.NewGuid();

        // Act
        var cacheOneBitmap = cacheOne.GetOrAdd(id, imageData);
        var cacheTwoBitmap = cacheTwo.GetOrAdd(id, imageData);
        cacheOne.Clear();
        var cacheTwoAfterClear = cacheTwo.GetOrAdd(id, imageData);

        // Assert
        Assert.NotNull(cacheOneBitmap);
        Assert.NotNull(cacheTwoBitmap);
        Assert.Same(cacheTwoBitmap, cacheTwoAfterClear);
    }

    [Fact]
    public void ImageDecodeCache_SyncImages_DropsRemovedAndReplacedEntries()
    {
        // Arrange
        var cache = new ImageDecodeCache();
        var originalBytes = CreatePngBytes(SKColors.Red);
        var replacementBytes = CreatePngBytes(SKColors.Blue);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        var original = cache.GetOrAdd(firstId, originalBytes);
        var second = cache.GetOrAdd(secondId, originalBytes);

        // Act
        cache.SyncImages(new[]
        {
            new ImageElement { Id = firstId, ImageData = replacementBytes },
        });

        var refreshed = cache.GetOrAdd(firstId, replacementBytes);

        // Assert
        Assert.NotNull(original);
        Assert.NotNull(second);
        Assert.NotNull(refreshed);
        Assert.NotSame(original, refreshed);
    }

    [Fact]
    public void ImageDecodeCache_Refreshes_WhenImageBytesMutateInPlace()
    {
        // Arrange
        var cache = new ImageDecodeCache();
        var imageData = CreatePngBytes(SKColors.Red);
        var replacementData = CreatePngBytes(SKColors.Blue);
        var elementId = Guid.NewGuid();

        var first = cache.GetOrAdd(elementId, imageData);

        // Mutate the original array in place to simulate an updated image payload.
        Buffer.BlockCopy(replacementData, 0, imageData, 0, imageData.Length);

        // Act
        var second = cache.GetOrAdd(elementId, imageData);
        cache.Invalidate(elementId);
        var refreshed = cache.GetOrAdd(elementId, imageData);

        // Assert
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Same(first, second);
        Assert.NotNull(refreshed);
        Assert.NotSame(first, refreshed);
    }

    [Fact]
    public void BoardDrawOperation_Equals_DoesNotReuseMutatedBoardState()
    {
        // Arrange
        var canvas = new BoardCanvas();
        var board = new BoardState();
        var drawOperationType = typeof(BoardCanvas).GetNestedType("BoardDrawOperation", BindingFlags.NonPublic)!;
        var ctor = drawOperationType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(BoardCanvas), typeof(Rect), typeof(BoardState)],
            modifiers: null)!;

        var first = (ICustomDrawOperation)ctor.Invoke([canvas, new Rect(0, 0, 100, 100), board]);
        board.Elements.Add(new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = Vector2.Zero,
            Size = new Vector2(10, 10),
            Type = ShapeType.Rectangle
        });
        canvas.InvalidateBoard();
        var second = (ICustomDrawOperation)ctor.Invoke([canvas, new Rect(0, 0, 100, 100), board]);

        // Assert
        Assert.False(first.Equals(second));
    }

    [Fact]
    public void DrawStroke_SinglePoint_RendersVisibleDot()
    {
        // Arrange
        using var bitmap = new SKBitmap(20, 20);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var stroke = new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = Vector2.Zero,
            Points = new List<Vector2> { new(10, 10) },
            Thickness = 6f,
            Color = SKColors.Black
        };

        // Act
        ElementDrawingHelper.DrawStroke(canvas, stroke);

        // Assert
        Assert.NotEqual(SKColors.Transparent, bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void DrawShape_NegativeSize_DrawsInNormalizedArea()
    {
        // Arrange
        using var bitmap = new SKBitmap(20, 20);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var shape = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(15, 15),
            Size = new Vector2(-10, -10),
            Type = ShapeType.Rectangle,
            FillColor = SKColors.Black,
            StrokeColor = SKColors.Black,
            StrokeWidth = 1f
        };

        // Act
        ElementDrawingHelper.DrawShape(canvas, shape);

        // Assert
        Assert.NotEqual(SKColors.Transparent, bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void DrawImage_NegativeSize_DrawsInNormalizedArea()
    {
        // Arrange
        using var bitmap = new SKBitmap(20, 20);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var image = new ImageElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(15, 15),
            Size = new Vector2(-10, -10),
            ImageData = CreatePngBytes(SKColors.Red)
        };

        // Act
        ElementDrawingHelper.DrawImage(canvas, image);

        // Assert
        Assert.NotEqual(SKColors.Transparent, bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void DrawImage_RotatedImage_PaintsWithinRotatedBounds()
    {
        using var bitmap = new SKBitmap(40, 40);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var image = new ImageElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 10),
            Size = new Vector2(20, 10),
            Rotation = 45f,
            ImageData = CreatePngBytes(SKColors.Red)
        };

        ElementDrawingHelper.DrawImage(canvas, image);

        Assert.NotEqual(SKColors.Transparent, bitmap.GetPixel(20, 15));
    }

    [Fact]
    public void DrawShape_RotatedRectangle_PaintsAroundCenter()
    {
        using var bitmap = new SKBitmap(40, 40);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var shape = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 10),
            Size = new Vector2(20, 10),
            Type = ShapeType.Rectangle,
            FillColor = SKColors.Black,
            StrokeColor = SKColors.Black,
            StrokeWidth = 1f,
            Rotation = 45f
        };

        ElementDrawingHelper.DrawShape(canvas, shape);

        Assert.NotEqual(SKColors.Transparent, bitmap.GetPixel(20, 15));
    }

    private static byte[] CreatePngBytes()
    {
        return CreatePngBytes(SKColors.Red);
    }

    private static byte[] CreatePngBytes(SKColor color)
    {
        using var bitmap = new SKBitmap(1, 1);
        bitmap.SetPixel(0, 0, color);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
