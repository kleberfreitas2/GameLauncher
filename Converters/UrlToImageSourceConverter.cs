using System.Globalization;
using System.Net.Http;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace GameLauncher.Converters;

/// <summary>
/// Downloads image bytes and decodes via SkiaSharp (supports WEBP, PNG, JPEG, GIF).
/// Use with IsAsync=True on the Binding so the HTTP call runs off the UI thread.
/// </summary>
public class UrlToImageSourceConverter : IValueConverter
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string url || string.IsNullOrEmpty(url))
            return null;
        try
        {
            var bytes = _http.GetByteArrayAsync(url).GetAwaiter().GetResult();

            using var skBitmap = SKBitmap.Decode(bytes);
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
        catch { return null; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
