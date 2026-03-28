using System.Globalization;
using Avalonia.Data.Converters;
using BFGA.App.Helpers;

namespace BFGA.App.Converters;

public sealed class InitialsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => RosterHelpers.GetInitials(value?.ToString() ?? string.Empty);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
