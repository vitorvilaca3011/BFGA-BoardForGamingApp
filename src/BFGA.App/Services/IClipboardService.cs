using System.Threading.Tasks;

namespace BFGA.App.Services;

public sealed record ClipboardImageData(byte[] ImageData, string FileName);

public interface IClipboardService
{
    Task<ClipboardImageData?> ReadImageAsync();
    Task WriteImageAsync(byte[] imageData, string fileName);
}
