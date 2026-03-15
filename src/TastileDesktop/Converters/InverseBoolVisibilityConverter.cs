using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace TastileDesktop.Converters;

/// <summary>
/// Converts boolean to inverted visibility (true = collapsed, false = visible).
/// </summary>
public sealed class InverseBoolVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility v)
        {
            return v != Visibility.Visible;
        }
        return false;
    }
}
