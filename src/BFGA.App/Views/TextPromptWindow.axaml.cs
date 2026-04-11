using Avalonia.Controls;
using Avalonia.Interactivity;

namespace BFGA.App.Views;

public partial class TextPromptWindow : Window
{
    public TextPromptWindow()
    {
        InitializeComponent();
    }

    public TextPromptWindow(string title, string prompt, string placeholder) : this()
    {
        Title = title;
        PromptTextBlock.Text = prompt;
        InputTextBox.Watermark = placeholder;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close(InputTextBox.Text);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
