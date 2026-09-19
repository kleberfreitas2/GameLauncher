using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GameLauncher.Views;

public partial class SplashWindow : Window
{
    private const int MinimumBackgroundWidth = 1280;
    private const int MinimumBackgroundHeight = 720;

    private static readonly string[] EmbeddedBackgroundFiles =
    [
        "1736550.jpg",
        "3090584.jpg",
        "4k-gaming-background-bud9k5ffqi3r2ds9.jpg",
        "b0b982d2b084ed74173750ef5d8f118d.jpg",
        "pexels-cmrcn-30353202.jpg",
        "R.jpg",
        "wp10312652.jpg",
        "wp4585047.jpg",
        "wp9001771.jpg",
    ];

    private static readonly string LastSplashBgPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GameLauncher",
        "last_splash_bg.txt");

    public SplashWindow()
    {
        InitializeComponent();
        Loaded += SplashWindow_Loaded;
    }

    private void SplashWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyRandomBackground();
    }

    private void ApplyRandomBackground()
    {
        var candidates = EmbeddedBackgroundFiles
            .Select(f => new { File = f, Uri = new Uri($"pack://application:,,,/Assets/Backgrounds/{f}", UriKind.Absolute) })
            .ToList();

        if (candidates.Count == 0)
            return;

        var lastFile = TryReadLastSplashBg();
        var notLast = !string.IsNullOrWhiteSpace(lastFile)
            ? candidates.Where(c => !string.Equals(c.File, lastFile, StringComparison.OrdinalIgnoreCase)).ToList()
            : candidates;

        var pool = notLast.Count > 0 ? notLast : candidates;
        var chosen = pool[Random.Shared.Next(pool.Count)];

        // tenta aplicar; se falhar, tenta as demais
        foreach (var item in pool.OrderBy(_ => Random.Shared.Next()))
        {
            if (TrySetBackground(item.Uri))
            {
                TryWriteLastSplashBg(item.File);
                return;
            }
        }

        // fallback final
        if (TrySetBackground(chosen.Uri))
            TryWriteLastSplashBg(chosen.File);
    }

    private bool TrySetBackground(Uri uri)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.EndInit();

            // N?o ampliar imagens pequenas para preencher a tela. Isso deixa
            // o fundo pixelado/estourado mesmo quando a imagem original tem
            // boa qualidade em seu tamanho nativo.
            if (bitmap.PixelWidth < MinimumBackgroundWidth ||
                bitmap.PixelHeight < MinimumBackgroundHeight)
                return false;

            bitmap.Freeze();

            BackgroundImage.Source = bitmap;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryReadLastSplashBg()
    {
        try
        {
            return File.Exists(LastSplashBgPath)
                ? File.ReadAllText(LastSplashBgPath).Trim()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static void TryWriteLastSplashBg(string fileName)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LastSplashBgPath)!);
            File.WriteAllText(LastSplashBgPath, fileName);
        }
        catch
        {
            // ignore
        }
    }

    public void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnMaxRestore_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
