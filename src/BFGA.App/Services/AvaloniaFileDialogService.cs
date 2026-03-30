using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace BFGA.App.Services;

public sealed class AvaloniaFileDialogService : IFileDialogService
{
    private readonly Window _owner;

    public AvaloniaFileDialogService(Window owner)
    {
        _owner = owner;
    }

    public async Task<string?> OpenBoardPathAsync()
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open BFGA board",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("BFGA Board") { Patterns = new[] { "*.bfga" } }
            }
        }).ConfigureAwait(true);

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> SaveBoardPathAsync(string suggestedFileName)
    {
        var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save BFGA board",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "bfga",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("BFGA Board") { Patterns = new[] { "*.bfga" } }
            }
        }).ConfigureAwait(true);

        return file?.TryGetLocalPath();
    }

    public async Task<string?> OpenImagePathAsync()
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Image",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" } }
            }
        }).ConfigureAwait(true);

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
