using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameLauncher.Converters;

/// <summary>
/// Downloads image bytes and decodes via System.Drawing (GDI+) which supports WEBP on Windows 10+.
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

            using var inputStream = new MemoryStream(bytes);
            using var gdiBitmap = new System.Drawing.Bitmap(inputStream);
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
        catch { return null; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
