using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EchoPlayer.Desktop;

/// <summary>
/// Shows an element when the value is "present": <see langword="true"/>, or any non-null object.
/// With <c>ConverterParameter=Invert</c> it does the opposite.
/// </summary>
public sealed class VisibleWhenConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool present = value is bool b ? b : value is not null;
        if (parameter is "Invert")
            present = !present;
        return present ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when all bound values are the same object, for example "this preset is the active one".</summary>
public sealed class SameConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Length > 1 && values.All(v => Equals(v, values[0]));

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
