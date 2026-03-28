using System.Collections.Generic;
using BFGA.Core.Models;
using SkiaSharp;

namespace BFGA.Canvas.Rendering;

/// <summary>
/// Per-canvas cache for decoded image bitmaps.
/// Cache entries are scoped to a single board canvas to avoid cross-canvas interference.
/// </summary>
public sealed class ImageDecodeCache
{
    private readonly Dictionary<Guid, CacheEntry> _cache = new();

    public SKBitmap? GetOrAdd(Guid elementId, byte[] imageData)
    {
        if (_cache.TryGetValue(elementId, out var entry))
        {
            if (ReferenceEquals(entry.ImageData, imageData))
                return entry.Bitmap;

            entry.Bitmap.Dispose();
            _cache.Remove(elementId);
        }

        var bitmap = SKBitmap.Decode(imageData);
        if (bitmap is null)
            return null;

        _cache[elementId] = new CacheEntry(bitmap, imageData);
        return bitmap;
    }

    public void SyncImages(IEnumerable<ImageElement> images)
    {
        var liveImageIds = new HashSet<Guid>();

        foreach (var image in images)
        {
            liveImageIds.Add(image.Id);

            if (_cache.TryGetValue(image.Id, out var entry) &&
                !ReferenceEquals(entry.ImageData, image.ImageData))
            {
                entry.Bitmap.Dispose();
                _cache.Remove(image.Id);
            }
        }

        if (_cache.Count == 0)
            return;

        var removedIds = new List<Guid>();
        foreach (var key in _cache.Keys)
        {
            if (!liveImageIds.Contains(key))
                removedIds.Add(key);
        }

        foreach (var key in removedIds)
        {
            if (_cache.Remove(key, out var entry))
                entry.Bitmap.Dispose();
        }
    }

    public void Invalidate(Guid elementId)
    {
        if (_cache.Remove(elementId, out var entry))
            entry.Bitmap.Dispose();
    }

    public void Clear()
    {
        foreach (var entry in _cache.Values)
            entry.Bitmap.Dispose();

        _cache.Clear();
    }

    private readonly record struct CacheEntry(SKBitmap Bitmap, byte[] ImageData);
}
