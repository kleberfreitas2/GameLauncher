using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace GameLauncher.Converters;

public class PathToImageSourceConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, WeakReference<BitmapSource>> _cache = new();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        int decodeWidth = 0;
        if (parameter is string p && int.TryParse(p, out int dw) && dw > 0)
            decodeWidth = dw;

        var cacheKey = $"{path}|{decodeWidth}";
        if (_cache.TryGetValue(cacheKey, out var weakRef) && weakRef.TryGetTarget(out var cached))
            return cached;

        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            BitmapSource? result = ext is ".webp"
                ? DecodeWithSkiaSharp(path)
                : DecodeWithWpf(path, decodeWidth);

            if (result is not null)
                _cache[cacheKey] = new WeakReference<BitmapSource>(result);

            return result;
        }
        catch { return null; }
    }

    private static BitmapSource? DecodeWithSkiaSharp(string path)
    {
        using var skData = SKData.Create(path);
        if (skData is null) return null;

        using var codec = SKCodec.Create(skData);
        if (codec is null) return null;

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height,
            SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        codec.GetPixels(info, bmp.GetPixels());

        var pixels = new byte[bmp.RowBytes * bmp.Height];
        Marshal.Copy(bmp.GetPixels(), pixels, 0, pixels.Length);

        var bs = BitmapSource.Create(info.Width, info.Height, 96, 96,
            PixelFormats.Pbgra32, null, pixels, bmp.RowBytes);
        bs.Freeze();
        return bs;
    }

    private static BitmapSource? DecodeWithWpf(string path, int decodeWidth)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = fs;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (decodeWidth > 0)
            bitmap.DecodePixelWidth = decodeWidth;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
