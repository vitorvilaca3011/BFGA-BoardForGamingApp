using System.Threading.Tasks;

namespace BFGA.App.Services;

public interface ITextPromptService
{
    Task<string?> PromptAsync(string title, string prompt, string placeholder = "");
}
