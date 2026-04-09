using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameLauncher.Models;
using GameLauncher.Services;
using GameLauncher.Views;
using Microsoft.Win32;

namespace GameLauncher.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private static readonly string SaveFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GameLauncher", "games.json");

    private readonly ICollectionView _gamesView;
    private readonly HardwareMonitorService _hwMonitor;
    private readonly XInputService _xinput;
    private readonly Dispatcher _dispatcher;
    private int _selectedIndex = -1;

    [ObservableProperty]
    private ObservableCollection<Game> games = new();

    [ObservableProperty]
    private string statusMessage = "Clique em '+ ADICIONAR JOGO' para começar";

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackgroundImage))]
    private BitmapSource? backgroundImage;

    [ObservableProperty] private double cpuUsage;
    [ObservableProperty] private double cpuTemp;
    [ObservableProperty] private double gpuUsage;
    [ObservableProperty] private double gpuTemp;
    [ObservableProperty] private double ramUsage;
    [ObservableProperty] private string cpuTempText = "--°C";
    [ObservableProperty] private string gpuTempText = "--°C";

    [ObservableProperty] private Game? selectedGame;
    [ObservableProperty] private Game? detailGame;
    [ObservableProperty] private bool showDetailPanel;
    [ObservableProperty] private bool gamepadConnected;
    [ObservableProperty] private string gamepadStatus = "";

    public bool HasBackgroundImage => BackgroundImage is not null;

    public ICollectionView GamesView => _gamesView;

    public MainViewModel()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;

        _gamesView = CollectionViewSource.GetDefaultView(Games);
        _gamesView.Filter = obj =>
            obj is Game g &&
            (string.IsNullOrWhiteSpace(SearchText) ||
             g.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
             g.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        _gamesView.SortDescriptions.Add(new SortDescription(nameof(Game.IsFavorite), ListSortDirection.Descending));
        _gamesView.SortDescriptions.Add(new SortDescription(nameof(Game.Name), ListSortDirection.Ascending));

        var bgPath = SettingsService.Current.BackgroundImagePath;
        if (!string.IsNullOrEmpty(bgPath))
            BackgroundImage = LoadHighQualityBackground(bgPath);

        LoadGames();

        _hwMonitor = new HardwareMonitorService();
        _hwMonitor.MetricsUpdated += OnMetricsUpdated;
        _hwMonitor.Start();

        _xinput = new XInputService();
        _xinput.ButtonPressed += OnGamepadButton;
        _xinput.ConnectionChanged += OnGamepadConnectionChanged;
        _xinput.Start();
    }

    private void OnMetricsUpdated(HardwareMetrics m)
    {
        _dispatcher.BeginInvoke(() =>
        {
            CpuUsage = m.CpuUsage;
            CpuTemp = m.CpuTemp;
            GpuUsage = m.GpuUsage;
            GpuTemp = m.GpuTemp;
            RamUsage = m.RamUsage;
            CpuTempText = m.CpuTemp > 0 ? $"{m.CpuTemp:F0}°C" : "--°C";
            GpuTempText = m.GpuTemp > 0 ? $"{m.GpuTemp:F0}°C" : "--°C";
        });
    }

    public void Dispose()
    {
        _xinput.Stop();
        _xinput.Dispose();
        _hwMonitor.Stop();
        _hwMonitor.Dispose();
    }

    partial void OnSearchTextChanged(string value) => _gamesView.Refresh();

    private void LoadGames()
    {
        try
        {
            if (!File.Exists(SaveFilePath)) return;
            var json = File.ReadAllText(SaveFilePath);
            var saved = JsonSerializer.Deserialize<List<Game>>(json);
            if (saved is null) return;
            foreach (var g in saved)
                Games.Add(g);
            StatusMessage = $"{Games.Count} jogos na biblioteca";
        }
        catch { }
    }

    private void SaveGames()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SaveFilePath)!);
            var json = JsonSerializer.Serialize(Games.ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SaveFilePath, json);
        }
        catch { }
    }

    [RelayCommand]
    private async Task AddGame()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecione o executável do jogo",
            Filter = "Executável (*.exe)|*.exe",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true) return;

        var newGames = new List<Game>();

        foreach (var file in dialog.FileNames)
        {
            if (Games.Any(g => g.ExecutablePath == file)) continue;

            var game = new Game
            {
                Name = Path.GetFileNameWithoutExtension(file),
                ExecutablePath = file,
                InstallDirectory = Path.GetDirectoryName(file) ?? string.Empty,
                IconPath = IconExtractor.ExtractIcon(file)
            };

            Games.Add(game);
            newGames.Add(game);
        }

        SaveGames();
        StatusMessage = $"{Games.Count} jogos na biblioteca";

        // Auto-busca informações do IGDB para cada jogo adicionado
        foreach (var game in newGames)
            await AutoFetchIgdbAsync(game);
    }

    private async Task AutoFetchIgdbAsync(Game game)
    {
        var clientId     = SettingsService.Current.IgdbClientId;
        var clientSecret = SettingsService.Current.IgdbClientSecret;

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return;

        try
        {
            StatusMessage = $"Buscando info de '{game.DisplayName}'...";

            using var svc = new IgdbService(clientId, clientSecret);
            var results = await svc.SearchGamesAsync(game.DisplayName);

            if (results.Count == 0)
            {
                StatusMessage = $"Nenhum resultado IGDB para '{game.DisplayName}'";
                return;
            }

            var igdbGame = results[0];

            // Traduz descrição para PT-BR
            var summary = igdbGame.Summary;
            if (!string.IsNullOrEmpty(summary))
            {
                StatusMessage = $"Traduzindo descrição de '{game.DisplayName}'...";
                summary = await TranslationService.TranslateToPortugueseAsync(summary);
            }

            game.Summary     = summary;
            game.IgdbRating  = igdbGame.Rating;
            game.IgdbId      = igdbGame.Id;
            game.ReleaseYear = igdbGame.ReleaseYear;

            // Traduz gêneros para PT-BR
            var genres = igdbGame.GenreNames;
            game.Genres = genres == "\u2014" ? null : TranslationService.TranslateGenres(genres);

            if (!string.IsNullOrEmpty(igdbGame.CoverUrl))
            {
                var coverPath = await svc.DownloadCoverAsync(igdbGame.CoverUrl, game.DisplayName);
                if (coverPath is not null)
                    game.CustomImagePath = coverPath;
            }

            SaveGames();
            StatusMessage = $"'{game.DisplayName}' — info IGDB aplicada automaticamente!";
        }
        catch
        {
            StatusMessage = $"{Games.Count} jogos na biblioteca";
        }
    }

    [RelayCommand]
    private void ChangeImage(Game game)
    {
        var dialog = new OpenFileDialog
        {
            Title = $"Escolha uma imagem para '{game.DisplayName}'",
            Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true) return;

        game.CustomImagePath = dialog.FileName;
        SaveGames();
        StatusMessage = $"Imagem de '{game.DisplayName}' atualizada!";
    }

    [RelayCommand]
    private void RemoveGame(Game game)
    {
        Games.Remove(game);
        SaveGames();
        StatusMessage = $"'{game.DisplayName}' removido. {Games.Count} jogos na biblioteca";
    }

    [RelayCommand]
    private void LaunchGame(Game game)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = game.ExecutablePath,
                UseShellExecute = true,
                WorkingDirectory = game.InstallDirectory
            });
            game.LastPlayed = DateTime.Now;
            SaveGames();
            StatusMessage = $"Lançando {game.DisplayName}...";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao lançar {game.DisplayName}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleFavorite(Game game)
    {
        game.IsFavorite = !game.IsFavorite;
        _gamesView.Refresh();
        SaveGames();
        StatusMessage = game.IsFavorite
            ? $"'{game.DisplayName}' adicionado aos favoritos ⭐"
            : $"'{game.DisplayName}' removido dos favoritos";
    }

    [RelayCommand]
    private void RenameGame(Game game)
    {
        var dialog = new RenameDialog(game.DisplayName) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.NewName))
        {
            game.Name = dialog.NewName;
            _gamesView.Refresh();
            SaveGames();
            StatusMessage = $"Jogo renomeado para '{game.DisplayName}'";
        }
    }

    [RelayCommand]
    private void SearchCover(Game game)
    {
        var apiKey = SettingsService.Current.SteamGridDbApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            var keyDialog = new ApiKeyDialog { Owner = Application.Current.MainWindow };
            if (keyDialog.ShowDialog() != true) return;
            SettingsService.Current.SteamGridDbApiKey = keyDialog.ApiKey;
            SettingsService.Save();
            apiKey = keyDialog.ApiKey;
        }

        var dialog = new CoverSearchDialog(apiKey, game.DisplayName)
        {
            Owner = Application.Current.MainWindow
        };
        if (dialog.ShowDialog() == true && dialog.DownloadedImagePath is not null)
        {
            game.CustomImagePath = dialog.DownloadedImagePath;
            SaveGames();
            StatusMessage = $"Capa de '{game.DisplayName}' atualizada!";
        }
    }

    [RelayCommand]
    private void OpenTheme()
    {
        var dialog = new ThemeDialog { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private async Task FetchIgdbInfo(Game game)
    {
        var clientId     = SettingsService.Current.IgdbClientId;
        var clientSecret = SettingsService.Current.IgdbClientSecret;

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            var setup = new IgdbSetupDialog { Owner = Application.Current.MainWindow };
            if (setup.ShowDialog() != true) return;
            SettingsService.Current.IgdbClientId     = setup.ClientId;
            SettingsService.Current.IgdbClientSecret = setup.ClientSecret;
            SettingsService.Save();
            clientId     = setup.ClientId;
            clientSecret = setup.ClientSecret;
        }

        var dialog = new IgdbGameInfoDialog(clientId, clientSecret, game.DisplayName)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && dialog.SelectedGame is { } igdbGame)
        {
            var summary = igdbGame.Summary;
            if (!string.IsNullOrEmpty(summary))
            {
                StatusMessage = $"Traduzindo descrição de '{game.DisplayName}'...";
                summary = await TranslationService.TranslateToPortugueseAsync(summary);
            }

            game.Summary     = summary;
            game.IgdbRating  = igdbGame.Rating;
            game.IgdbId      = igdbGame.Id;
            game.ReleaseYear = igdbGame.ReleaseYear;

            var genres = igdbGame.GenreNames;
            game.Genres = genres == "\u2014" ? null : TranslationService.TranslateGenres(genres);

            if (dialog.DownloadedCoverPath is not null)
                game.CustomImagePath = dialog.DownloadedCoverPath;

            SaveGames();
            StatusMessage = $"'{game.DisplayName}' atualizado com info do IGDB!";
        }
    }

    [RelayCommand]
    private void ChangeBackground()
    {
        var dialog = new OpenFileDialog
        {
            Title  = "Escolha uma imagem de fundo",
            Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.webp"
        };
        if (dialog.ShowDialog() != true) return;
        var image = LoadHighQualityBackground(dialog.FileName);
        if (image is null) return;
        BackgroundImage = image;
        SettingsService.Current.BackgroundImagePath = dialog.FileName;
        SettingsService.Save();
        StatusMessage = "Imagem de fundo aplicada!";
    }

    private static BitmapSource? LoadHighQualityBackground(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }
        catch { return null; }
    }

    [RelayCommand]
    private void RemoveBackground()
    {
        BackgroundImage = null;
        SettingsService.Current.BackgroundImagePath = string.Empty;
        SettingsService.Save();
        StatusMessage = "Imagem de fundo removida.";
    }

    [RelayCommand]
    private void ShowDetail(Game game)
    {
        DetailGame = game;
        ShowDetailPanel = true;
    }

    [RelayCommand]
    private void CloseDetail()
    {
        ShowDetailPanel = false;
        DetailGame = null;
    }

    [RelayCommand]
    private void LaunchDetailGame()
    {
        if (DetailGame is not null)
            LaunchGame(DetailGame);
    }

    // ── Gamepad Navigation ──────────────────────────────────────

    private void OnGamepadConnectionChanged(bool connected)
    {
        _dispatcher.BeginInvoke(() =>
        {
            GamepadConnected = connected;
            GamepadStatus = connected ? "🎮 Controle conectado  |  A = Jogar  |  Y = Favorito" : "";
            if (connected && SelectedGame is null && GetVisibleGames().Count > 0)
            {
                _selectedIndex = 0;
                SelectedGame = GetVisibleGames()[0];
            }
            if (!connected)
            {
                SelectedGame = null;
                _selectedIndex = -1;
            }
        });
    }

    private void OnGamepadButton(GamepadButton button)
    {
        _dispatcher.BeginInvoke(() =>
        {
            var visible = GetVisibleGames();
            if (visible.Count == 0) return;

            // Ensure valid selection
            if (_selectedIndex < 0 || _selectedIndex >= visible.Count)
            {
                _selectedIndex = 0;
                SelectedGame = visible[0];
            }

            int columns = EstimateColumns();

            switch (button)
            {
                case GamepadButton.DPadRight:
                    NavigateBy(1, visible);
                    break;
                case GamepadButton.DPadLeft:
                    NavigateBy(-1, visible);
                    break;
                case GamepadButton.DPadDown:
                    NavigateBy(columns, visible);
                    break;
                case GamepadButton.DPadUp:
                    NavigateBy(-columns, visible);
                    break;
                case GamepadButton.RightShoulder:
                    NavigateBy(columns * 2, visible);
                    break;
                case GamepadButton.LeftShoulder:
                    NavigateBy(-columns * 2, visible);
                    break;
                case GamepadButton.A:
                    if (ShowDetailPanel && DetailGame is not null)
                        LaunchGame(DetailGame);
                    else if (SelectedGame is not null)
                        ShowDetail(SelectedGame);
                    break;
                case GamepadButton.Y:
                    if (SelectedGame is not null)
                        ToggleFavorite(SelectedGame);
                    break;
                case GamepadButton.X:
                    if (SelectedGame is not null)
                        SearchCover(SelectedGame);
                    break;
                case GamepadButton.B:
                    if (ShowDetailPanel)
                        CloseDetail();
                    else if (!string.IsNullOrEmpty(SearchText))
                        SearchText = string.Empty;
                    break;
            }
        });
    }

    private void NavigateBy(int delta, List<Game> visible)
    {
        int newIndex = _selectedIndex + delta;
        newIndex = Math.Clamp(newIndex, 0, visible.Count - 1);
        _selectedIndex = newIndex;
        SelectedGame = visible[newIndex];
    }

    private List<Game> GetVisibleGames()
    {
        var list = new List<Game>();
        foreach (var item in _gamesView)
        {
            if (item is Game g)
                list.Add(g);
        }
        return list;
    }

    private static int EstimateColumns()
    {
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow is null) return 5;
        double availableWidth = mainWindow.ActualWidth - 60; // padding
        int cardWidth = 185 + 20; // card + margin
        int cols = Math.Max(1, (int)(availableWidth / cardWidth));
        return cols;
    }
}


