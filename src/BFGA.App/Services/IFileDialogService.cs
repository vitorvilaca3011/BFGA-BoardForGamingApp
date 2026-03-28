using System.Threading.Tasks;

namespace BFGA.App.Services;

public interface IFileDialogService
{
    Task<string?> OpenBoardPathAsync();
    Task<string?> SaveBoardPathAsync(string suggestedFileName);
}
