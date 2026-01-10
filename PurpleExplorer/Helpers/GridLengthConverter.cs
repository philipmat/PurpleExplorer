using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace PurpleExplorer.Helpers;

public class GridLengthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d) return new GridLength(d);
        return new GridLength(300);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is GridLength gl) return gl.Value;
        return 300.0;
    }
}
