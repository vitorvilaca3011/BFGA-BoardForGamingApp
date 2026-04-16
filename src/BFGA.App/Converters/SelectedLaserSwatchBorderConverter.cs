using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace BFGA.App.Converters;

public sealed class SelectedLaserSwatchBorderConverter : IValueConverter
{
    public static SelectedLaserSwatchBorderConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Brushes.White;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
