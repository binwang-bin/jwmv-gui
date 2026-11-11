using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Jwmv.Gui.Converters;

/// <summary>
/// Visible when the bound collection/count is empty (for empty-state overlays).
/// Pass ConverterParameter=Invert to show when non-empty.
/// </summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var empty = value switch
        {
            null => true,
            int count => count == 0,
            ICollection collection => collection.Count == 0,
            IEnumerable enumerable => !enumerable.Cast<object?>().Any(),
            _ => true
        };

        var invert = parameter is string p && p.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        if (invert)
        {
            empty = !empty;
        }

        return empty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
