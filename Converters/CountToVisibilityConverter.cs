using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace CheckHash.Converters;

public class CountToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // If count is 0, return true (Visible). Otherwise false (Collapsed/Hidden).
        if (value is int count && count == 0) return true;

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}