using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using SkiaSharp;

namespace BFGA.App.Converters;

public sealed class SelectedLaserSwatchThicknessConverter : IValueConverter
{
    public static SelectedLaserSwatchThicknessConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SKColor selectedColor && parameter is string swatchHex && selectedColor == SKColor.Parse(swatchHex))
        {
            return new Thickness(2);
        }

        return new Thickness(1);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
