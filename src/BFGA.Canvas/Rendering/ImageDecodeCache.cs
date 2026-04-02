using System.Collections.Generic;
using BFGA.Core.Models;
using SkiaSharp;

namespace BFGA.Canvas.Rendering;

/// <summary>
/// Per-canvas cache for decoded image bitmaps.
/// Cache entries are scoped to a single board canvas to avoid cross-canvas interference.
/// Caches SKImage (GPU-compatible) rather than SKBitmap to avoid native crashes on
/// Windows WinUI compositor when DrawBitmap tries to create an SKImage internally.
/// </summary>
public sealed class ImageDecodeCache
{
    private readonly Dictionary<Guid, CacheEntry> _cache = new();

    public SKImage? GetOrAdd(Guid elementId, byte[] imageData)
    {
        if (_cache.TryGetValue(elementId, out var entry))
        {
            if (ReferenceEquals(entry.ImageData, imageData))
                return entry.Image;

            entry.Image.Dispose();
            _cache.Remove(elementId);
        }

        var image = DecodeToImage(imageData);
        if (image is null)
            return null;

        _cache[elementId] = new CacheEntry(image, imageData);
        return image;
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
                entry.Image.Dispose();
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
                entry.Image.Dispose();
        }
    }

    public void Invalidate(Guid elementId)
    {
        if (_cache.Remove(elementId, out var entry))
            entry.Image.Dispose();
    }

    public void Clear()
    {
        foreach (var entry in _cache.Values)
            entry.Image.Dispose();

        _cache.Clear();
    }

    /// <summary>
    /// Decodes raw image bytes into an SKImage.
    /// Uses SKImage.FromEncodedData so the image is decoded in the format required by
    /// the active GPU/software backend, avoiding the sk_image_new_from_bitmap crash on
    /// Windows WinUI compositor when pixel formats are incompatible.
    /// </summary>
    private static SKImage? DecodeToImage(byte[] imageData)
    {
        // SKImage.FromEncodedData lets Skia decode the compressed bytes (PNG/JPEG/etc.)
        // directly into a backend-compatible format, skipping the SKBitmap pixel layout.
        var image = SKImage.FromEncodedData(imageData);
        if (image is not null)
            return image;

        // Fallback: decode through SKBitmap for formats SKImage doesn't handle directly.
        using var bitmap = SKBitmap.Decode(imageData);
        if (bitmap is null)
            return null;

        return SKImage.FromBitmap(bitmap);
    }

    private readonly record struct CacheEntry(SKImage Image, byte[] ImageData);
}
