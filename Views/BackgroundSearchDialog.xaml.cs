using System.ComponentModel;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using craftersmine.SteamGridDBNet;
using GameLauncher.Services;
using SkiaSharp;

namespace GameLauncher.Views;

public partial class BackgroundSearchDialog : Window
{
    private SteamGridDbService _service;
    private readonly string _gameName;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private int _sgdbGameId;
    private bool _initialized;
    private bool _suppressFilterChange;
    private CancellationTokenSource? _thumbnailCts;

    private string? _selectedFullUrl;

    public string? DownloadedBackgroundPath { get; private set; }

    private record FilterItem<T>(string Label, T Value)
    {
        public override string ToString() => Label;
    }

    internal sealed class DisplayImage : INotifyPropertyChanged, IDisposable
    {
        public SteamGridImage Source { get; }
        private ImageSource? _thumbnail;
        private WriteableBitmap? _wb;
        private List<byte[]>? _framePixels;
        private long[]? _cumulativeMs;
        private long _totalDurationMs;
        private int _frameWidth, _frameHeight, _frameStride;
        private int _frameIndex;
        private TimeSpan _lastRenderTime;
        private double _elapsedMs;
        private bool _renderingAttached;

        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail))); }
        }

        public DisplayImage(SteamGridImage source) => Source = source;
        public event PropertyChangedEventHandler? PropertyChanged;

        public void StartAnimation(List<byte[]> framePixels, List<int> delays, int width, int height, int stride)
        {
            _framePixels = framePixels;
            _frameWidth = width;
            _frameHeight = height;
            _frameStride = stride;
            _frameIndex = 0;

            _cumulativeMs = new long[delays.Count];
            long total = 0;
            for (int i = 0; i < delays.Count; i++)
            {
                total += delays[i];
                _cumulativeMs[i] = total;
            }
            _totalDurationMs = total;

            _wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
            _wb.WritePixels(new Int32Rect(0, 0, width, height), framePixels[0], stride, 0);
            Thumbnail = _wb;

            _lastRenderTime = TimeSpan.Zero;
            _elapsedMs = 0;
            CompositionTarget.Rendering += OnRendering;
            _renderingAttached = true;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (_framePixels is null || _wb is null || _cumulativeMs is null) return;

            var args = (RenderingEventArgs)e;
            if (_lastRenderTime == TimeSpan.Zero)
            {
                _lastRenderTime = args.RenderingTime;
                return;
            }

            var delta = (args.RenderingTime - _lastRenderTime).TotalMilliseconds;
            _lastRenderTime = args.RenderingTime;
            _elapsedMs += delta;

            if (_totalDurationMs > 0 && _elapsedMs >= _totalDurationMs)
                _elapsedMs %= _totalDurationMs;

            int newIndex = _cumulativeMs.Length - 1;
            for (int i = 0; i < _cumulativeMs.Length; i++)
            {
                if (_elapsedMs < _cumulativeMs[i])
                {
                    newIndex = i;
                    break;
                }
            }

            if (newIndex != _frameIndex)
            {
                _frameIndex = newIndex;
                _wb.WritePixels(new Int32Rect(0, 0, _frameWidth, _frameHeight),
                    _framePixels[_frameIndex], _frameStride, 0);
            }
        }

        public void Dispose()
        {
            if (_renderingAttached)
            {
                CompositionTarget.Rendering -= OnRendering;
                _renderingAttached = false;
            }
            _framePixels = null;
            _cumulativeMs = null;
            _wb = null;
        }
    }

    public BackgroundSearchDialog(string apiKey, string gameName)
    {
        InitializeComponent();
        _service  = new SteamGridDbService(apiKey);
        _gameName = gameName;
        SubtitleText.Text = $"Escolha uma imagem de fundo para \"{gameName}\"";
        PopulateFilters();
        Loaded += async (_, _) => await LoadImagesAsync();
        Closed += (_, _) => DisposeCurrentItems();
    }

    private void PopulateFilters()
    {
        _suppressFilterChange = true;
        PopulateDimensionsForTab();

        CbStyles.Items.Clear();
        CbStyles.Items.Add(new FilterItem<SteamGridDbStyles?>("Any Style", null));
        CbStyles.Items.Add(new FilterItem<SteamGridDbStyles?>("Alternate", SteamGridDbStyles.Alternate));
        CbStyles.Items.Add(new FilterItem<SteamGridDbStyles?>("Blurred", SteamGridDbStyles.Blurred));
        CbStyles.Items.Add(new FilterItem<SteamGridDbStyles?>("Material", SteamGridDbStyles.Material));
        CbStyles.SelectedIndex = 0;

        CbFormats.Items.Clear();
        CbFormats.Items.Add(new FilterItem<SteamGridDbFormats>("Any File Type", SteamGridDbFormats.All));
        CbFormats.Items.Add(new FilterItem<SteamGridDbFormats>("PNG", SteamGridDbFormats.Png));
        CbFormats.Items.Add(new FilterItem<SteamGridDbFormats>("JPEG", SteamGridDbFormats.Jpeg));
        CbFormats.Items.Add(new FilterItem<SteamGridDbFormats>("WEBP", SteamGridDbFormats.Webp));
        CbFormats.SelectedIndex = 0;

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
        if (games.Count == 0 && IsUnauthorizedError())
        {
            SetLoading(false);
            if (PromptNewApiKey())
            {
                await LoadImagesAsync();
                return;
            }
            StatusText.Text       = "API Key inválida. Configure uma chave válida em steamgriddb.com/profile/preferences/api";
            StatusText.Visibility = Visibility.Visible;
            return;
        }
        if (games.Count == 0)
        {
            SetLoading(false);
            StatusText.Text       = _service.LastError is not null
                ? $"Erro: {_service.LastError}"
                : $"Nenhum jogo encontrado no SteamGridDB para \"{_gameName}\".";
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
        using var semaphore = new SemaphoreSlim(6);
        var tasks = items.Select(async item =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                if (ct.IsCancellationRequested) return;

                var thumbUrl = !string.IsNullOrEmpty(item.Source.ThumbnailUrl)
                    ? item.Source.ThumbnailUrl
                    : null;
                var fullUrl = item.Source.Url;

                byte[]? bytes = null;
                if (thumbUrl is not null)
                    try { bytes = await _http.GetByteArrayAsync(thumbUrl, ct); } catch { }
                if (bytes is null || bytes.Length == 0)
                    bytes = await _http.GetByteArrayAsync(fullUrl, ct);

                if (ct.IsCancellationRequested) return;

                await Task.Run(() =>
                {
                    using var skData = SKData.CreateCopy(bytes);
                    using var codec = SKCodec.Create(skData);
                    if (codec is null) return;

                    var info = new SKImageInfo(codec.Info.Width, codec.Info.Height,
                        SKColorType.Bgra8888, SKAlphaType.Premul);

                    if (codec.FrameCount > 1)
                    {
                        var framePixels = new List<byte[]>(codec.FrameCount);
                        var delays = new List<int>(codec.FrameCount);
                        var skBitmaps = new List<SKBitmap>(codec.FrameCount);
                        int actualStride = 0;

                        try
                        {
                            for (int i = 0; i < codec.FrameCount; i++)
                            {
                                ct.ThrowIfCancellationRequested();

                                var fi = codec.FrameInfo[i];
                                var frameBmp = new SKBitmap(info);

                                if (fi.RequiredFrame >= 0 && fi.RequiredFrame < skBitmaps.Count)
                                    skBitmaps[fi.RequiredFrame].CopyTo(frameBmp);

                                codec.GetPixels(info, frameBmp.GetPixels(), new SKCodecOptions(i));
                                skBitmaps.Add(frameBmp);

                                actualStride = frameBmp.RowBytes;
                                var pixels = new byte[frameBmp.RowBytes * frameBmp.Height];
                                Marshal.Copy(frameBmp.GetPixels(), pixels, 0, pixels.Length);
                                framePixels.Add(pixels);
                                delays.Add(fi.Duration > 0 ? fi.Duration : 100);
                            }
                        }
                        finally
                        {
                            foreach (var b in skBitmaps) b.Dispose();
                        }

                        if (ct.IsCancellationRequested) return;

                        Dispatcher.Invoke(() =>
                        {
                            if (ct.IsCancellationRequested) return;
                            item.StartAnimation(framePixels, delays, info.Width, info.Height, actualStride);
                        });
                    }
                    else
                    {
                        using var bmp = new SKBitmap(info);
                        codec.GetPixels(info, bmp.GetPixels());

                        var pixels = new byte[bmp.RowBytes * bmp.Height];
                        Marshal.Copy(bmp.GetPixels(), pixels, 0, pixels.Length);
                        if (ct.IsCancellationRequested) return;

                        var bs = BitmapSource.Create(info.Width, info.Height, 96, 96,
                            PixelFormats.Pbgra32, null, pixels, bmp.RowBytes);
                        bs.Freeze();
                        Dispatcher.Invoke(() => item.Thumbnail = bs);
                    }
                }, ct);
            }
            catch { }
            finally { semaphore.Release(); }
        });
        await Task.WhenAll(tasks);
    }

    private void SetLoading(bool loading)
    {
        if (loading)
        {
            _thumbnailCts?.Cancel();
            _thumbnailCts = null;
            DisposeCurrentItems();
        }

        LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        if (loading)
        {
            StatusText.Text       = "Buscando imagens no SteamGridDB...";
            StatusText.Visibility = Visibility.Visible;
            ImageList.Visibility  = Visibility.Collapsed;
        }
    }

    private void DisposeCurrentItems()
    {
        if (ImageList.ItemsSource is IEnumerable<DisplayImage> items)
        {
            foreach (var item in items)
                item.Dispose();
        }
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
