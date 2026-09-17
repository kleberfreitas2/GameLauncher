using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Net.Http;
using System.IO;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class ScreenshotViewerDialog : Window
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly IReadOnlyList<string> _screenshots;
    private int _index;

    public ScreenshotViewerDialog(IReadOnlyList<string> screenshots, int initialIndex = 0)
    {
        InitializeComponent();
        _screenshots = screenshots;
        _index = Math.Clamp(initialIndex, 0, Math.Max(0, screenshots.Count - 1));
        Loaded += async (_, _) =>
        {
            Focus();
            await ShowCurrentScreenshotAsync();
        };
    }

    private async Task ShowCurrentScreenshotAsync()
    {
        if (_screenshots.Count == 0)
        {
            Close();
            return;
        }

        try
        {
            var bytes = await Http.GetByteArrayAsync(_screenshots[_index]);
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmap.EndInit();
            bitmap.Freeze();
            ScreenshotImage.Source = bitmap;
        }
        catch
        {
            ScreenshotImage.Source = null;
        }

        CounterText.Text = $"{_index + 1} / {_screenshots.Count}   |   Setas esquerda/direita para navegar   |   Esc para fechar";
        PreviousButton.IsEnabled = _screenshots.Count > 1;
        NextButton.IsEnabled = _screenshots.Count > 1;
    }

    private void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (_screenshots.Count == 0) return;
        _index = (_index - 1 + _screenshots.Count) % _screenshots.Count;
        _ = ShowCurrentScreenshotAsync();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_screenshots.Count == 0) return;
        _index = (_index + 1) % _screenshots.Count;
        _ = ShowCurrentScreenshotAsync();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                Previous_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.Right:
                Next_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }

    public void HandleGamepadInput(GamepadButton button)
    {
        switch (button)
        {
            case GamepadButton.DPadLeft:
            case GamepadButton.LeftShoulder:
                Previous_Click(this, new RoutedEventArgs());
                break;
            case GamepadButton.DPadRight:
            case GamepadButton.RightShoulder:
                Next_Click(this, new RoutedEventArgs());
                break;
            case GamepadButton.B:
            case GamepadButton.Back:
                Close();
                break;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
