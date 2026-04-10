using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using craftersmine.SteamGridDBNet;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class BackgroundSearchDialog : Window
{
    private readonly SteamGridDbService _service;
    private readonly string _gameName;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private int _sgdbGameId;
    private bool _initialized;
    private bool _suppressFilterChange;
    private CancellationTokenSource? _thumbnailCts;

    private string? _selectedFullUrl;

    public string? DownloadedBackgroundPath { get; private set; }

    // Filter item helpers
    private record FilterItem<T>(string Label, T Value)
    {
        public override string ToString() => Label;
    }

    // Display wrapper that holds a lazily-loaded thumbnail
    internal sealed class DisplayImage : INotifyPropertyChanged
    {
        public SteamGridImage Source { get; }
        private ImageSource? _thumbnail;

        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail))); }
        }

        public DisplayImage(SteamGridImage source) => Source = source;
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public BackgroundSearchDialog(string apiKey, string gameName)
    {
        InitializeComponent();
        _service  = new SteamGridDbService(apiKey);
        _gameName = gameName;
        SubtitleText.Text = $"Escolha uma imagem de fundo para \"{gameName}\"";
        PopulateFilters();
        Loaded += async (_, _) => await LoadImagesAsync();
    }

    private void PopulateFilters()
    {
        _suppressFilterChange = true;

        // Dimensions — populated based on current tab
        PopulateDimensionsForTab();

        // Styles
        CbStyles.Items.Clear();
        CbStyles.Items.Add(new FilterItem<SteamGridDbStyles?>("Any Style", null));
        CbStyles.Items.Add(new FilterItem<SteamGridDbStyles?>("Alternate", SteamGridDbStyles.Alternate));
        CbStyles.Items.Add(new FilterItem<SteamGridDbStyles?>("Blurred", SteamGridDbStyles.Blurred));
        CbStyles.Items.Add(new FilterItem<SteamGridDbStyles?>("Material", SteamGridDbStyles.Material));
        CbStyles.SelectedIndex = 0;

        // Formats
        CbFormats.Items.Clear();
        CbFormats.Items.Add(new FilterItem<SteamGridDbFormats>("Any File Type", SteamGridDbFormats.All));
        CbFormats.Items.Add(new FilterItem<SteamGridDbFormats>("PNG", SteamGridDbFormats.Png));
        CbFormats.Items.Add(new FilterItem<SteamGridDbFormats>("JPEG", SteamGridDbFormats.Jpeg));
        CbFormats.Items.Add(new FilterItem<SteamGridDbFormats>("WEBP", SteamGridDbFormats.Webp));
        CbFormats.SelectedIndex = 0;

        // Types
        CbTypes.Items.Clear();
        CbTypes.Items.Add(new FilterItem<SteamGridDbTypes>("All", SteamGridDbTypes.All));
        CbTypes.Items.Add(new FilterItem<SteamGridDbTypes>("Static", SteamGridDbTypes.Static));
        CbTypes.Items.Add(new FilterItem<SteamGridDbTypes>("Animated", SteamGridDbTypes.Animated));
        CbTypes.SelectedIndex = 0;

        _suppressFilterChange = false;
    }

    private void PopulateDimensionsForTab()
    {
        var prevSuppressState = _suppressFilterChange;
        _suppressFilterChange = true;

        CbDimensions.Items.Clear();

        if (TabHeroes.IsChecked == true)
        {
            CbDimensions.Items.Add(new FilterItem<SteamGridDbDimensions?>("Any Dimensions", null));
            CbDimensions.Items.Add(new FilterItem<SteamGridDbDimensions?>("1920x620", SteamGridDbDimensions.W1920H620));
            CbDimensions.Items.Add(new FilterItem<SteamGridDbDimensions?>("3840x1240", SteamGridDbDimensions.W3840H1240));
            CbDimensions.Items.Add(new FilterItem<SteamGridDbDimensions?>("1600x650", SteamGridDbDimensions.W1600H650));
        }
        else
        {
            CbDimensions.Items.Add(new FilterItem<SteamGridDbDimensions?>("Any Dimensions", null));
            CbDimensions.Items.Add(new FilterItem<SteamGridDbDimensions?>("460x215", SteamGridDbDimensions.W460H215));
            CbDimensions.Items.Add(new FilterItem<SteamGridDbDimensions?>("920x430", SteamGridDbDimensions.W920H430));
            CbDimensions.Items.Add(new FilterItem<SteamGridDbDimensions?>("512x512", SteamGridDbDimensions.W512H512));
            CbDimensions.Items.Add(new FilterItem<SteamGridDbDimensions?>("1024x1024", SteamGridDbDimensions.W1024H1024));
        }

        CbDimensions.SelectedIndex = 0;
        _suppressFilterChange = prevSuppressState;
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
        _initialized = true;
        await RefreshImagesAsync();
    }

    private async Task RefreshImagesAsync()
    {
        if (_sgdbGameId == 0) return;

        SetLoading(true);
        _selectedFullUrl = null;
        ApplyButton.IsEnabled = false;

        // Gather filter values
        var dimensions = (CbDimensions.SelectedItem as FilterItem<SteamGridDbDimensions?>)?.Value;
        var styles     = (CbStyles.SelectedItem as FilterItem<SteamGridDbStyles?>)?.Value;
        var formats    = (CbFormats.SelectedItem as FilterItem<SteamGridDbFormats>)?.Value ?? SteamGridDbFormats.All;
        var types      = (CbTypes.SelectedItem as FilterItem<SteamGridDbTypes>)?.Value ?? SteamGridDbTypes.All;
        var humor      = ChkHumor.IsChecked == true;
        var nsfw       = ChkNsfw.IsChecked == true;
        var epilepsy   = ChkEpilepsy.IsChecked == true;

        List<SteamGridImage> images;
        string typeLabel;

        if (TabHeroes.IsChecked == true)
        {
            typeLabel = "hero";
            images = await _service.GetHeroesAsync(_sgdbGameId,
                styles: styles ?? SteamGridDbStyles.AllHeroes,
                dimensions: dimensions ?? SteamGridDbDimensions.AllHeroes,
                types: types, formats: formats,
                nsfw: nsfw, humorous: humor, epilepsy: epilepsy);
        }
        else
        {
            typeLabel = "key art";
            images = await _service.GetGridsWideAsync(_sgdbGameId,
                styles: styles ?? SteamGridDbStyles.AllGrids,
                dimensions: dimensions ?? SteamGridDbDimensions.AllGrids,
                types: types, formats: formats,
                nsfw: nsfw, humorous: humor, epilepsy: epilepsy);
        }

        SetLoading(false);

        if (images.Count == 0)
        {
            ImageList.Visibility  = Visibility.Collapsed;
            StatusText.Text       = _service.LastError is not null
                ? $"Erro: {_service.LastError}"
                : $"Nenhuma imagem ({typeLabel}) encontrada com esses filtros.";
            StatusText.Visibility = Visibility.Visible;
            FooterText.Text       = "";
            return;
        }

        var displayItems = images.Select(img => new DisplayImage(img)).ToList();
        ImageList.ItemsSource = displayItems;
        ImageList.Visibility  = Visibility.Visible;
        StatusText.Visibility = Visibility.Collapsed;
        FooterText.Text       = $"{images.Count} imagem(ns) de {typeLabel} encontrada(s)";

        // Start downloading thumbnails in background
        _thumbnailCts = new CancellationTokenSource();
        _ = LoadThumbnailsAsync(displayItems, _thumbnailCts.Token);
    }

    private void Tab_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        PopulateDimensionsForTab();
        _ = RefreshImagesAsync();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressFilterChange || !_initialized) return;
        _ = RefreshImagesAsync();
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFilterChange || !_initialized) return;
        _ = RefreshImagesAsync();
    }

    private void ImageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ImageList.SelectedItem is DisplayImage item)
        {
            _selectedFullUrl = item.Source.Url;
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

    private async Task LoadThumbnailsAsync(List<DisplayImage> items, CancellationToken ct)
    {
        // Use SemaphoreSlim to limit concurrent downloads
        using var semaphore = new SemaphoreSlim(6);
        var tasks = items.Select(async item =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                if (ct.IsCancellationRequested) return;
                var url = item.Source.ThumbnailUrl ?? item.Source.Url;
                var bytes = await _http.GetByteArrayAsync(url, ct);
                var bitmapSource = DecodeImage(bytes);
                if (bitmapSource is not null)
                    Dispatcher.Invoke(() => item.Thumbnail = bitmapSource);
            }
            catch { }
            finally { semaphore.Release(); }
        });
        await Task.WhenAll(tasks);
    }

    private static BitmapSource? DecodeImage(byte[] bytes)
    {
        // Use System.Drawing (GDI+) which supports WEBP on Windows 10+
        using var inputStream = new MemoryStream(bytes);
        using var bitmap = new System.Drawing.Bitmap(inputStream);

        // Re-encode as PNG and load into WPF BitmapImage
        using var pngStream = new MemoryStream();
        bitmap.Save(pngStream, ImageFormat.Png);
        pngStream.Position = 0;

        var bi = new BitmapImage();
        bi.BeginInit();
        bi.StreamSource = pngStream;
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    private void SetLoading(bool loading)
    {
        // Cancel any in-progress thumbnail downloads
        if (loading)
        {
            _thumbnailCts?.Cancel();
            _thumbnailCts = null;
        }

        LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        if (loading)
        {
            StatusText.Text       = "Buscando imagens no SteamGridDB...";
            StatusText.Visibility = Visibility.Visible;
            ImageList.Visibility  = Visibility.Collapsed;
        }
    }
}
