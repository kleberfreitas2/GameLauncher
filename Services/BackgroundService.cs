using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameLauncher.Services;

public static class BackgroundService
{
    private static readonly List<string> BackgroundPaths = new()
    {
        "/Assets/Backgrounds/360_F_861510294_6Je3kKMSC7wNfW0JIZf0OyYQcGCzB8y9.jpg",
        "/Assets/Backgrounds/86462-593059278_tiny.jpg",
        "/Assets/Backgrounds/pc-gaming-broken-controller-zvbj1ryoiptz09af.jpg",
        "/Assets/Backgrounds/pngtree-neon-glowing-video-game-controllers-on-a-black-background-image_16521725.jpg",
        "/Assets/Backgrounds/pngtree-vibrant-dual-tone-video-game-controller-a-unique-blend-of-blue-image_16314927.jpg"
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
