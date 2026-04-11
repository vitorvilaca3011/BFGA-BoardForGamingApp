using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace BFGA.App.Services;

public sealed class AvaloniaClipboardService : IClipboardService
{
    private readonly TopLevel _owner;

    public AvaloniaClipboardService(TopLevel owner)
    {
        _owner = owner;
    }

    public async Task<ClipboardImageData?> ReadImageAsync()
    {
        var clipboard = _owner.Clipboard;
        if (clipboard is null)
            return null;

#pragma warning disable CS0618
        var formats = await clipboard.GetFormatsAsync();
#pragma warning restore CS0618
        if (formats is null)
            return null;

        byte[]? imageData = null;
        var fileName = "clipboard.png";

        var imageFormat = formats.FirstOrDefault(f =>
            f.Contains("image", StringComparison.OrdinalIgnoreCase) ||
            f.Contains("png", StringComparison.OrdinalIgnoreCase) ||
            f.Contains("bitmap", StringComparison.OrdinalIgnoreCase));

        if (imageFormat is not null)
        {
#pragma warning disable CS0618
            var data = await clipboard.GetDataAsync(imageFormat);
#pragma warning restore CS0618
            if (data is byte[] bytes)
            {
                imageData = bytes;
            }
            else if (data is Stream stream)
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                imageData = ms.ToArray();
            }
        }

        if (imageData is null)
        {
            var fileFormats = new[] { "Files", "FileNames", "text/uri-list" };
            foreach (var ff in fileFormats)
            {
                if (!formats.Contains(ff))
                    continue;

#pragma warning disable CS0618
                var data = await clipboard.GetDataAsync(ff);
#pragma warning restore CS0618
                if (data is IEnumerable<string> filePaths)
                {
                    var path = filePaths.FirstOrDefault(p =>
                        p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        p.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                        p.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                        p.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                        p.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));
                    if (path is not null && File.Exists(path))
                    {
                        imageData = await File.ReadAllBytesAsync(path);
                        fileName = Path.GetFileName(path);
                    }
                }

                if (imageData is not null)
                    break;
            }
        }

        return imageData is null || imageData.Length == 0
            ? null
            : new ClipboardImageData(imageData, fileName);
    }

    public async Task WriteImageAsync(byte[] imageData, string fileName)
    {
        if (imageData.Length == 0)
            return;

        var clipboard = _owner.Clipboard;
        if (clipboard is null)
            return;

#pragma warning disable CS0618
        var dataObject = new DataObject();
        dataObject.Set("image/png", imageData);
        await clipboard.SetDataObjectAsync(dataObject);
#pragma warning restore CS0618
    }
}
