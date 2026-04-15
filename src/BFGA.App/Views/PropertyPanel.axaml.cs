using Avalonia.Controls;
using Avalonia.Interactivity;
using BFGA.App.ViewModels;
using SkiaSharp;

namespace BFGA.App.Views;

public partial class PropertyPanel : UserControl
{
    public PropertyPanel()
    {
        InitializeComponent();
        FontFamilyComboBox.ItemsSource = BoardScreenViewModel.AvailableFontFamilies;
    }

    private void OnStrokeSwatchClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BoardScreenViewModel viewModel)
            return;

        viewModel.SelectedStrokeColor = ParseSkColor((sender as Button)?.Tag);
    }

    private void OnFillSwatchClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BoardScreenViewModel viewModel)
            return;

        viewModel.SelectedFillColor = ParseSkColor((sender as Button)?.Tag);
    }

    private static SKColor ParseSkColor(object? tag)
        => tag is string hex ? SKColor.Parse(hex) : SKColors.Transparent;
}
