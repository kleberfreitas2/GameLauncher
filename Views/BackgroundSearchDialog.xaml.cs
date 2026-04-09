using System.Windows;
using System.Windows.Controls;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class BackgroundSearchDialog : Window
{
    private readonly SteamGridDbService _service;
    private readonly string _gameName;
    private int _sgdbGameId;

    private List<SteamGridImage> _heroes = [];

    private string? _selectedFullUrl;

    public string? DownloadedBackgroundPath { get; private set; }

    public BackgroundSearchDialog(string apiKey, string gameName)
    {
        InitializeComponent();
        _service  = new SteamGridDbService(apiKey);
        _gameName = gameName;
        SubtitleText.Text = $"Escolha uma imagem de fundo para \"{gameName}\"";
        Loaded += async (_, _) => await LoadImagesAsync();
    }

    private async Task LoadImagesAsync()
    {
        SetLoading(true);

        var games = await _service.SearchGamesAsync(_gameName);
        if (games.Count == 0)
        {
            SetLoading(false);
            StatusText.Text       = $"Nenhum jogo encontrado no SteamGridDB para \"{_gameName}\".";
            StatusText.Visibility = Visibility.Visible;
            return;
        }

        _sgdbGameId = games[0].Id;
        _heroes = await _service.GetHeroesAsync(_sgdbGameId);

        SetLoading(false);
        ShowHeroes();
    }

    private void ShowHeroes()
    {
        _selectedFullUrl = null;
        ApplyButton.IsEnabled = false;

        if (_heroes.Count == 0)
        {
            ImageList.Visibility  = Visibility.Collapsed;
            StatusText.Text       = "Nenhuma imagem de fundo (hero) encontrada para este jogo.";
            StatusText.Visibility = Visibility.Visible;
            return;
        }

        ImageList.ItemsSource = _heroes;
        ImageList.Visibility  = Visibility.Visible;
        StatusText.Visibility = Visibility.Collapsed;
        FooterText.Text       = $"{_heroes.Count} imagem(ns) de fundo encontrada(s)";
    }

    private void ImageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ImageList.SelectedItem is SteamGridImage img)
        {
            _selectedFullUrl = img.Url;
            ApplyButton.IsEnabled = true;
        }
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFullUrl is null) return;

        ApplyButton.IsEnabled = false;
        FooterText.Text       = "Baixando imagem em alta resolução...";

        DownloadedBackgroundPath = await _service.DownloadHeroAsync(_selectedFullUrl, _gameName);

        if (DownloadedBackgroundPath is not null)
        {
            DialogResult = true;
        }
        else
        {
            ApplyButton.IsEnabled = true;
            FooterText.Text       = string.Empty;
            MessageBox.Show($"Erro ao baixar imagem: {_service.LastError}",
                "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void SetLoading(bool loading)
    {
        LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        if (loading)
        {
            StatusText.Text       = "Buscando imagens no SteamGridDB...";
            StatusText.Visibility = Visibility.Visible;
            ImageList.Visibility  = Visibility.Collapsed;
        }
    }
}
