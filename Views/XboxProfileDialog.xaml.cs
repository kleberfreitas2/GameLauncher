using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameLauncher.Models;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class XboxProfileDialog : Window
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public bool LogoutRequested { get; private set; }
    public bool ImportGamesRequested { get; private set; }

    public XboxProfileDialog(XboxProfile profile, int importedCount = 0, int availableCount = 0)
    {
        InitializeComponent();

        GamertagText.Text = profile.Gamertag;
        GamerscoreText.Text = profile.Gamerscore.ToString("N0");
        ImportedCountText.Text = importedCount.ToString("N0");
        AvailableCountText.Text = availableCount.ToString("N0");
        XuidText.Text = $"XUID: {profile.Xuid}";

        if (!string.IsNullOrEmpty(profile.AccountTier))
            TierText.Text = $"Xbox Live {profile.AccountTier}";

        LoadAvatarAsync(profile.AvatarUrl);
    }

    private async void LoadAvatarAsync(string url)
    {
        if (string.IsNullOrEmpty(url)) return;

        try
        {
            var imageUrl = url.Contains('?')
                ? $"{url}&format=png&w=208&h=208"
                : $"{url}?format=png&w=208&h=208";

            var bytes = await _http.GetByteArrayAsync(imageUrl);

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

            AvatarEllipse.Fill = new ImageBrush(bi)
            {
                Stretch = Stretch.UniformToFill
            };
            FallbackIcon.Visibility = Visibility.Collapsed;
        }
        catch { }
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        LogoutRequested = true;
        DialogResult = true;
    }

    private void ImportGames_Click(object sender, RoutedEventArgs e)
    {
        ImportGamesRequested = true;
        DialogResult = true;
    }

    public void HandleGamepadInput(GamepadButton button)
    {
        switch (button)
        {
            case GamepadButton.B:
            case GamepadButton.Back:
                DialogResult = false;
                break;
        }
    }
}
