using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class CoverSearchDialog : Window
{
    private SteamGridDbService _service;
    private readonly string _gameName;
    private string? _selectedImageUrl;
    private Border? _selectedBorder;

    public string? DownloadedImagePath { get; private set; }

    public CoverSearchDialog(string apiKey, string gameName)
    {
        InitializeComponent();
        _service  = new SteamGridDbService(apiKey);
        _gameName = gameName;
        SearchBox.Text = gameName;
        Loaded += async (_, _) => await DoSearchAsync(gameName);
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) _ = DoSearchAsync(SearchBox.Text.Trim());
    }

    private void Search_Click(object sender, RoutedEventArgs e)
        => _ = DoSearchAsync(SearchBox.Text.Trim());

    private async Task DoSearchAsync(string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return;

        SetLoading(true);
        ResultsScroll.Visibility = Visibility.Collapsed;
        ResultsPanel.Children.Clear();
        _selectedImageUrl = null;
        _selectedBorder   = null;
        ApplyButton.IsEnabled  = false;
        SelectedText.Text = "Nenhuma capa selecionada";

        var games = await _service.SearchGamesAsync(term);
        if (games.Count == 0 && IsUnauthorizedError())
        {
            SetLoading(false);
            if (PromptNewApiKey())
            {
                await DoSearchAsync(term);
                return;
            }
            StatusText.Text = "API Key inválida. Configure uma chave válida em steamgriddb.com/profile/preferences/api";
            StatusText.Visibility = Visibility.Visible;
            return;
        }
        if (games.Count == 0)
        {
            SetLoading(false);
            StatusText.Text = _service.LastError is not null
                ? $"Erro ao buscar: {_service.LastError}"
                : $"Nenhum jogo encontrado para '{term}'.";
            StatusText.Visibility = Visibility.Visible;
            return;
        }

        var covers = await _service.GetCoversAsync(games[0].Id);
        SetLoading(false);

        if (covers.Count == 0)
        {
            StatusText.Text = _service.LastError is not null
                ? $"Erro ao buscar capas: {_service.LastError}"
                : $"Sem capas disponíveis para '{games[0].Name}'.";
            StatusText.Visibility = Visibility.Visible;
            return;
        }

        StatusText.Visibility = Visibility.Collapsed;
        foreach (var cover in covers)
            AddThumbnail(cover.Url, cover.ThumbnailUrl);

        ResultsScroll.Visibility = Visibility.Visible;
    }

    private void AddThumbnail(string fullUrl, string? thumbnailUrl)
    {
        var border = new Border
        {
            Width            = 112,
            Height           = 168,
            Margin           = new Thickness(5),
            CornerRadius     = new CornerRadius(6),
            BorderThickness  = new Thickness(2),
            BorderBrush      = Brushes.Transparent,
            Cursor           = Cursors.Hand,
            ClipToBounds     = true,
            Background       = new SolidColorBrush(Color.FromRgb(10, 10, 30))
        };

        try
        {
            var previewUrl = !string.IsNullOrEmpty(thumbnailUrl) ? thumbnailUrl : fullUrl;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource        = new Uri(previewUrl);
            bmp.DecodePixelWidth = 112;
            bmp.EndInit();
            border.Child = new Image { Source = bmp, Stretch = Stretch.UniformToFill };
        }
        catch
        {
            border.Child = new TextBlock
            {
                Text = "⚠",
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
        }

        border.MouseLeftButtonDown += (_, _) => SelectCover(border, fullUrl);
        ResultsPanel.Children.Add(border);
    }

    private void SelectCover(Border border, string url)
    {
        if (_selectedBorder is not null)
            _selectedBorder.BorderBrush = Brushes.Transparent;

        var green = (SolidColorBrush?)Application.Current.Resources["AccentGreenBrush"]
                    ?? new SolidColorBrush(Color.FromRgb(0, 230, 118));
        border.BorderBrush = green;
        _selectedBorder    = border;
        _selectedImageUrl  = url;
        ApplyButton.IsEnabled = true;
        SelectedText.Text  = "✓ Capa selecionada";
        SelectedText.Foreground = green;
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedImageUrl is null) return;
        SetLoading(true);
        DownloadedImagePath = await _service.DownloadCoverAsync(_selectedImageUrl, _gameName);
        SetLoading(false);

        if (DownloadedImagePath is not null)
            DialogResult = true;
        else
            MessageBox.Show("Erro ao baixar a capa. Verifique sua conexão e tente novamente.",
                "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void SetLoading(bool loading)
    {
        LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        if (loading) StatusText.Visibility = Visibility.Collapsed;
    }

    private bool IsUnauthorizedError()
    {
        return _service.LastError is not null &&
               (_service.LastError.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
                _service.LastError.Contains("API key", StringComparison.OrdinalIgnoreCase) ||
                _service.LastError.Contains("401", StringComparison.Ordinal));
    }

    private bool PromptNewApiKey()
    {
        var keyDialog = new ApiKeyDialog { Owner = this };
        if (keyDialog.ShowDialog() != true) return false;
        SettingsService.Current.SteamGridDbApiKey = keyDialog.ApiKey;
        SettingsService.Save();
        _service = new SteamGridDbService(keyDialog.ApiKey);
        return true;
    }
}
