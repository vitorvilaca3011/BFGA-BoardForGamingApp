using Avalonia.Controls;
using Avalonia.Interactivity;
using BFGA.App.ViewModels;
using SkiaSharp;

namespace BFGA.App.Views;

public partial class SettingsPanel : UserControl
{
    public SettingsPanel()
    {
        InitializeComponent();
    }

    private void OnLaserColorSwatchClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string colorHex } || DataContext is not BoardScreenViewModel viewModel)
        {
            return;
        }

        viewModel.LaserPresenceColor = SKColor.Parse(colorHex);
    }
}
