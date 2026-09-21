using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Spanish.Core;

namespace Spanish.Views;

/// <summary>Whether a word breaks the usual rules, for the exception badge in the word list.</summary>
public class IrregularWordConverter : IValueConverter
{
    public static IrregularWordConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is LearnUnit unit && Irregularities.Of(unit).Count > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
