using BFGA.App.Views;

namespace BFGA.App.Services;

public sealed class AvaloniaTextPromptService(Avalonia.Controls.Window owner) : ITextPromptService
{
    public async Task<string?> PromptAsync(string title, string prompt, string placeholder = "")
    {
        var window = new TextPromptWindow(title, prompt, placeholder);
        return await window.ShowDialog<string?>(owner);
    }
}
