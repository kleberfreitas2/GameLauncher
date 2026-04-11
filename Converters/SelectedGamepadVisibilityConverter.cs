using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GameLauncher.Converters;

public class SelectedGamepadVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3) return Visibility.Collapsed;

        var item           = values[0];
        var selected       = values[1];
        var gamepadConnected = values[2] is bool b && b;

        return item is not null && ReferenceEquals(item, selected) && gamepadConnected
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
