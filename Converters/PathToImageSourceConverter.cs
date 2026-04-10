using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace GameLauncher.Converters;

/// <summary>
/// Loads images from local file paths. Uses SkiaSharp for WEBP files,
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
                return DecodeWithSkia(path);

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

    private static BitmapSource? DecodeWithSkia(string path)
    {
        using var skBitmap = SKBitmap.Decode(path);
        if (skBitmap is null) return null;

        var info = new SKImageInfo(skBitmap.Width, skBitmap.Height,
            SKColorType.Bgra8888, SKAlphaType.Premul);
        using var converted = new SKBitmap(info);
        using var canvas = new SKCanvas(converted);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(skBitmap, 0, 0);

        var bs = BitmapSource.Create(
            info.Width, info.Height, 96, 96,
            PixelFormats.Pbgra32, null,
            converted.GetPixels(),
            converted.RowBytes * converted.Height,
            converted.RowBytes);
        bs.Freeze();
        return bs;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
