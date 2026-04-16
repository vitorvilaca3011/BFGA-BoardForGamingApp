using System.Numerics;
using BFGA.Canvas.Rendering;

namespace BFGA.Canvas.Tests;

public class LaserTrailBufferTests
{
    [Fact]
    public void NewBuffer_HasZeroCount_AndEmptyPoints()
    {
        var buffer = new LaserTrailBuffer();

        Assert.Equal(0, buffer.Count);
        Assert.Equal(0, buffer.GetPoints().Length);
    }

    [Fact]
    public void Add5Points_CountIs5_PointsReturnedInOrder()
    {
        var buffer = new LaserTrailBuffer();

        for (int i = 0; i < 5; i++)
            buffer.Add(new Vector2(i, i), i * 100L);

        Assert.Equal(5, buffer.Count);
        var points = buffer.GetPoints();
        Assert.Equal(5, points.Length);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(new Vector2(i, i), points[i].Position);
            Assert.Equal(i * 100L, points[i].TimestampMs);
        }
    }

    [Fact]
    public void Add129Points_CountIs128_OldestOverwritten()
    {
        var buffer = new LaserTrailBuffer(128);

        for (int i = 0; i < 129; i++)
            buffer.Add(new Vector2(i, i), i);

        Assert.Equal(128, buffer.Count);
        var points = buffer.GetPoints();
        // Oldest should be point 1 (point 0 was overwritten)
        Assert.Equal(new Vector2(1, 1), points[0].Position);
        Assert.Equal(new Vector2(128, 128), points[^1].Position);
    }

    [Fact]
    public void GetPoints_AfterWrapAround_ReturnsChronologicalOrder()
    {
        var buffer = new LaserTrailBuffer(4);

        // Fill: 0,1,2,3 then add 4,5 (overwrites 0,1)
        for (int i = 0; i < 6; i++)
            buffer.Add(new Vector2(i, 0), i * 10L);

        Assert.Equal(4, buffer.Count);
        var points = buffer.GetPoints();
        // Should be 2,3,4,5 (oldest to newest)
        Assert.Equal(new Vector2(2, 0), points[0].Position);
        Assert.Equal(new Vector2(3, 0), points[1].Position);
        Assert.Equal(new Vector2(4, 0), points[2].Position);
        Assert.Equal(new Vector2(5, 0), points[3].Position);
    }

    [Fact]
    public void Clear_ResetsCountToZero_GetPointsReturnsEmpty()
    {
        var buffer = new LaserTrailBuffer();

        buffer.Add(new Vector2(1, 2), 100);
        buffer.Add(new Vector2(3, 4), 200);
        Assert.Equal(2, buffer.Count);

        buffer.Clear();

        Assert.Equal(0, buffer.Count);
        Assert.Equal(0, buffer.GetPoints().Length);
    }

    [Fact]
    public void Points_StoreVector2Position_AndTimestampMs_Correctly()
    {
        var buffer = new LaserTrailBuffer();
        var pos = new Vector2(42.5f, -17.3f);
        long ts = 1234567890L;

        buffer.Add(pos, ts);

        var points = buffer.GetPoints();
        Assert.Equal(1, points.Length);
        Assert.Equal(pos, points[0].Position);
        Assert.Equal(ts, points[0].TimestampMs);
    }
}
