using System.Numerics;

namespace BFGA.Canvas.Rendering;

public sealed class LaserTrailBuffer
{
    private readonly (Vector2 Position, long TimestampMs)[] _points;
    private readonly (Vector2 Position, long TimestampMs)[] _scratch;
    private int _head;
    private int _count;

    public LaserTrailBuffer(int capacity = 128)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _points = new (Vector2, long)[capacity];
        _scratch = new (Vector2, long)[capacity];
    }

    public int Count => _count;

    public int Capacity => _points.Length;

    public void Add(Vector2 position, long timestampMs)
    {
        _points[_head] = (position, timestampMs);
        _head = (_head + 1) % _points.Length;
        if (_count < _points.Length)
            _count++;
    }

    public ReadOnlySpan<(Vector2 Position, long TimestampMs)> GetPoints()
    {
        if (_count == 0)
            return ReadOnlySpan<(Vector2 Position, long TimestampMs)>.Empty;

        if (_count < _points.Length)
        {
            // Not wrapped — contiguous from 0
            return new ReadOnlySpan<(Vector2, long)>(_points, 0, _count);
        }

        // Wrapped — oldest is at _head, copy to scratch oldest-to-newest
        int oldest = _head; // _head points to next write = oldest slot
        for (int i = 0; i < _count; i++)
        {
            _scratch[i] = _points[(oldest + i) % _points.Length];
        }

        return new ReadOnlySpan<(Vector2, long)>(_scratch, 0, _count);
    }

    public void Clear()
    {
        _count = 0;
        _head = 0;
    }
}
