using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace GameLauncher.Converters;

public class UrlToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string url || string.IsNullOrEmpty(url))
            return null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(url, UriKind.Absolute);
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (parameter is string p && int.TryParse(p, out int decodeWidth) && decodeWidth > 0)
                bitmap.DecodePixelWidth = decodeWidth;
            else
                bitmap.DecodePixelWidth = 320;
            bitmap.EndInit();
            return bitmap;
        }
        catch { return null; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
