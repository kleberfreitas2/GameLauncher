using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameLauncher.Models;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class SteamProfileDialog : Window
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public bool LogoutRequested { get; private set; }
    public bool ImportGamesRequested { get; private set; }

    public SteamProfileDialog(SteamProfile profile, int importedCount = 0, int availableCount = 0)
    {
        InitializeComponent();

        PersonaNameText.Text = profile.PersonaName;
        TotalGamesText.Text = profile.OwnedGamesCount.ToString("N0");
        ImportedCountText.Text = importedCount.ToString("N0");
        AvailableCountText.Text = availableCount.ToString("N0");
        SteamIdText.Text = $"Steam ID: {profile.SteamId}";

        LoadAvatarAsync(profile.AvatarUrl);
    }

    private async void LoadAvatarAsync(string url)
    {
        if (string.IsNullOrEmpty(url)) return;

        try
        {
            var bytes = await _http.GetByteArrayAsync(url);

            using var stream = new MemoryStream(bytes);
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.StreamSource = stream;
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
