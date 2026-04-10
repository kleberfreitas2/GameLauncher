using System;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameLauncher.Converters;

/// <summary>
/// Loads images from local file paths. Uses System.Drawing (GDI+) for WEBP files,
/// WPF native BitmapImage for other formats.
/// </summary>
public class PathToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".webp")
                return DecodeWithGdi(path);

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = fs;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            if (parameter is string p && int.TryParse(p, out int decodeWidth) && decodeWidth > 0)
                bitmap.DecodePixelWidth = decodeWidth;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch { return null; }
    }

    private static BitmapSource? DecodeWithGdi(string path)
    {
        using var gdiBitmap = new System.Drawing.Bitmap(path);
        using var pngStream = new MemoryStream();
        gdiBitmap.Save(pngStream, ImageFormat.Png);
        pngStream.Position = 0;

        var bi = new BitmapImage();
        bi.BeginInit();
        bi.StreamSource = pngStream;
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
