using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameLauncher.Services;

public static class BackgroundService
{
    private static readonly List<string> BackgroundPaths = new()
    {
        "/Assets/Backgrounds/1736550.jpg",
        "/Assets/Backgrounds/3090584.jpg",
        "/Assets/Backgrounds/4k-gaming-background-bud9k5ffqi3r2ds9.jpg",
        "/Assets/Backgrounds/b0b982d2b084ed74173750ef5d8f118d.jpg",
        "/Assets/Backgrounds/pexels-cmrcn-30353202.jpg",
        "/Assets/Backgrounds/R.jpg",
        "/Assets/Backgrounds/wp10312652.jpg",
        "/Assets/Backgrounds/wp4585047.jpg",
        "/Assets/Backgrounds/wp9001771.jpg"
    };

    private static readonly Random Random = new();

    public static ImageBrush? GetRandomBackground()
    {
        var randomPath = BackgroundPaths[Random.Next(BackgroundPaths.Count)];

        try
        {
            System.Diagnostics.Debug.WriteLine($"[BackgroundService] Tentando carregar: {randomPath}");

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(randomPath, UriKind.Relative);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.EndInit();
            bitmap.Freeze();

            System.Diagnostics.Debug.WriteLine($"[BackgroundService] Imagem carregada com sucesso: {bitmap.PixelWidth}x{bitmap.PixelHeight}");

            return new ImageBrush(bitmap)
            {
                Stretch = Stretch.UniformToFill,
                Opacity = 1.0
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BackgroundService] ERRO ao carregar background: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[BackgroundService] Stack: {ex.StackTrace}");
            return null;
        }
    }

    public static string GetRandomBackgroundPath()
    {
        return BackgroundPaths[Random.Next(BackgroundPaths.Count)];
    }
}
