using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameLauncher.Models;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class DiscordProfileDialog : Window
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public bool LogoutRequested { get; private set; }

    public DiscordProfileDialog(DiscordProfile profile)
    {
        InitializeComponent();

        DisplayNameText.Text = profile.DisplayName;
        UsernameText.Text = $"@{profile.Username}";
        IdText.Text = $"ID: {profile.Id}";

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
