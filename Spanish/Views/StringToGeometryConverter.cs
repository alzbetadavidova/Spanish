using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Spanish.Views;

/// <summary>Converts SVG path data to a geometry, so view models can describe icons without Avalonia types.</summary>
public class StringToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string data ? StreamGeometry.Parse(data) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
