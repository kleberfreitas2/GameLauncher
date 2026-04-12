using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace GameLauncher.Services;

public static class IconExtractor
{
    private static readonly string IconCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GameLauncher", "icons");

    public static string? ExtractIcon(string exePath)
    {
        try
        {
            if (!File.Exists(exePath)) return null;

            Directory.CreateDirectory(IconCacheDir);

            var hash = Math.Abs(exePath.GetHashCode()).ToString();
            var iconPath = Path.Combine(IconCacheDir, hash + ".png");

            if (File.Exists(iconPath)) return iconPath;

            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon is null) return null;

            using var bitmap = icon.ToBitmap();
            using var resized = new Bitmap(bitmap, new Size(128, 128));
            resized.Save(iconPath, ImageFormat.Png);

            return iconPath;
        }
        catch
        {
            return null;
        }
    }
}
