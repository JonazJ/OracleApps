using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OracleApps.Launcher.Converters;

/// <summary>True collapses, false shows. The mirror image of the built-in converter.</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

/// <summary>Shows an element only when the bound value is not null.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    /// <summary>Set to true to invert: visible only when the value is null.</summary>
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is not null;
        return hasValue != Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
