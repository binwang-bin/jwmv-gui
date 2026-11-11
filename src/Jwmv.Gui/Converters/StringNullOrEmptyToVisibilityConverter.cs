using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Jwmv.Gui.Converters;

public sealed class StringNullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string;
        var visible = !string.IsNullOrWhiteSpace(text);
        var invert = parameter is string p && p.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        if (invert)
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
