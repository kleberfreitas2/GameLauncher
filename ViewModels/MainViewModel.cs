using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
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

    [ObservableProperty] private double cpuUsage;
    [ObservableProperty] private double cpuTemp;
    [ObservableProperty] private double gpuUsage;
    [ObservableProperty] private double gpuTemp;
    [ObservableProperty] private double ramUsage;
    [ObservableProperty] private string cpuTempText = "--°C";
    [ObservableProperty] private string gpuTempText = "--°C";

    [ObservableProperty] private string cpuName = "";
    [ObservableProperty] private string gpuName = "";
    [ObservableProperty] private string ramTotal = "";
    [ObservableProperty] private ObservableCollection<string> storageDrives = [];

    [ObservableProperty] private Game? selectedGame;
    [ObservableProperty] private Game? detailGame;
    [ObservableProperty] private bool showDetailPanel;
    [ObservableProperty] private bool gamepadConnected;
    [ObservableProperty] private string gamepadStatus = "";

    [ObservableProperty] private string currentTime = DateTime.Now.ToString("H:mm");
    [ObservableProperty] private string playerName = SettingsService.Current.PlayerName;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvatar))]
    [NotifyPropertyChangedFor(nameof(HasNoAvatar))]
    private string? avatarPath = string.IsNullOrEmpty(SettingsService.Current.AvatarImagePath) ? null : SettingsService.Current.AvatarImagePath;

    public bool HasAvatar   => !string.IsNullOrEmpty(AvatarPath);
    public bool HasNoAvatar => !HasAvatar;

    private readonly DispatcherTimer _clockTimer;

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

        LoadGames();
        _ = RefreshAllAssetsOnStartupAsync();

        _hwMonitor = new HardwareMonitorService();
        _hwMonitor.MetricsUpdated += OnMetricsUpdated;
        _hwMonitor.Start();

        _xinput = new XInputService();
        _xinput.ButtonPressed += OnGamepadButton;
        _xinput.ConnectionChanged += OnGamepadConnectionChanged;
        _xinput.Start();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _clockTimer.Tick += (_, _) => CurrentTime = DateTime.Now.ToString("H:mm");
        _clockTimer.Start();
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

            if (!string.IsNullOrEmpty(m.CpuName) && string.IsNullOrEmpty(CpuName))
                CpuName = m.CpuName;
            if (!string.IsNullOrEmpty(m.GpuName) && string.IsNullOrEmpty(GpuName))
                GpuName = m.GpuName;
            if (!string.IsNullOrEmpty(m.RamTotal) && string.IsNullOrEmpty(RamTotal))
                RamTotal = m.RamTotal;
            if (m.StorageDrives.Count > 0 && StorageDrives.Count == 0)
            {
                foreach (var d in m.StorageDrives)
                    StorageDrives.Add(d);
            }
        });
    }

    public void Dispose()
    {
        _clockTimer.Stop();
        _xinput.Stop();
        _xinput.Dispose();
        _hwMonitor.Stop();
        _hwMonitor.Dispose();
    }

    [RelayCommand]
    private void ChangeAvatar()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Escolha uma imagem de avatar",
            Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.webp"
        };
        if (dialog.ShowDialog() != true) return;
        AvatarPath = dialog.FileName;
        SettingsService.Current.AvatarImagePath = dialog.FileName;
        SettingsService.Save();
        StatusMessage = "Avatar atualizado!";
    }

    partial void OnSearchTextChanged(string value) => _gamesView.Refresh();

    partial void OnSelectedGameChanged(Game? value)
    {
        DetailGame = value;
        ShowDetailPanel = value is not null;
    }

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
            _gamesView.Refresh();
            var first = _gamesView.Cast<Game>().FirstOrDefault();
            if (first is not null)
                SelectedGame = first;
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

            var defaultName = Path.GetFileNameWithoutExtension(file);

            var nameDialog = new GameNameInputDialog(defaultName)
            {
                Owner = Application.Current.MainWindow
            };

            var gameName = nameDialog.ShowDialog() == true
                ? nameDialog.GameName
                : defaultName;

            var game = new Game
            {
                Name = gameName,
                ExecutablePath = file,
                InstallDirectory = Path.GetDirectoryName(file) ?? string.Empty,
                IconPath = IconExtractor.ExtractIcon(file)
            };

            Games.Add(game);
            newGames.Add(game);
        }

        SaveGames();
        StatusMessage = $"{Games.Count} jogos na biblioteca";

        // Auto-busca com progresso visual para jogos recém-adicionados
        foreach (var game in newGames)
        {
            await AutoFetchAllWithProgressAsync(game);
        }
    }

    private async Task AutoFetchAllWithProgressAsync(Game game)
    {
        var progressDialog = new LoadingProgressDialog(game.DisplayName)
        {
            Owner = Application.Current.MainWindow
        };
        progressDialog.Show();

        try
        {
            // Passo 1/6 — Ícone (0→15%)
            progressDialog.UpdateProgress(0, "Buscando ícone...");
            var apiKey = SettingsService.Current.SteamGridDbApiKey;
            SteamGridDbService? svc = null;
            int sgdbId = 0;
            bool hasSteamGridDb = false;

            if (!string.IsNullOrEmpty(apiKey))
            {
                svc = new SteamGridDbService(apiKey);
                var sgdbGames = await svc.SearchGamesAsync(game.DisplayName);
                if (sgdbGames.Count > 0)
                {
                    sgdbId = sgdbGames[0].Id;
                    hasSteamGridDb = true;
                }
            }

            if (hasSteamGridDb && svc is not null)
            {
                if (string.IsNullOrEmpty(game.IconPath) || !System.IO.File.Exists(game.IconPath))
                {
                    var icons = await svc.GetIconsAsync(sgdbId);
                    if (icons.Count > 0)
                    {
                        var path = await svc.DownloadIconAsync(icons[0].Url, game.DisplayName);
                        if (path is not null) game.IconPath = path;
                    }
                }
            }
            progressDialog.UpdateProgress(15, "Ícone concluído!");

            // Passo 2/6 — Logo (15→30%)
            progressDialog.UpdateProgress(18, "Buscando logo...");
            if (hasSteamGridDb && svc is not null && string.IsNullOrEmpty(game.LogoPath))
            {
                var logos = await svc.GetLogosAsync(sgdbId);
                if (logos.Count > 0)
                {
                    var path = await svc.DownloadLogoAsync(logos[0].Url, game.DisplayName);
                    if (path is not null) game.LogoPath = path;
                }
            }
            progressDialog.UpdateProgress(30, "Logo concluído!");

            // Passo 3/6 — Capa (30→50%)
            progressDialog.UpdateProgress(33, "Buscando capa...");
            if (hasSteamGridDb && svc is not null && string.IsNullOrEmpty(game.CustomImagePath))
            {
                var covers = await svc.GetCoversAsync(sgdbId);
                if (covers.Count > 0)
                {
                    var path = await svc.DownloadCoverAsync(covers[0].Url, game.DisplayName);
                    if (path is not null) game.CustomImagePath = path;
                }
            }
            progressDialog.UpdateProgress(50, "Capa concluída!");

            // Passo 4/6 — Fundo (50→65%)
            progressDialog.UpdateProgress(53, "Buscando fundo...");
            if (hasSteamGridDb && svc is not null && string.IsNullOrEmpty(game.BackgroundImagePath))
            {
                var heroes = await svc.GetHeroesAsync(sgdbId);
                if (heroes.Count > 0)
                {
                    var path = await svc.DownloadHeroAsync(heroes[0].Url, game.DisplayName);
                    if (path is not null) game.BackgroundImagePath = path;
                }
            }
            progressDialog.UpdateProgress(65, "Fundo concluído!");

            SaveGames();

            // Passo 5/6 — IGDB info (65→85%)
            progressDialog.UpdateProgress(68, "Buscando descrição e informações (IGDB)...");
            var clientId     = SettingsService.Current.IgdbClientId;
            var clientSecret = SettingsService.Current.IgdbClientSecret;

            if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
            {
                using var igdb = new IgdbService(clientId, clientSecret);
                var results = await igdb.SearchGamesAsync(game.DisplayName);

                if (results.Count > 0)
                {
                    var igdbGame = results[0];

                    progressDialog.UpdateProgress(80, "Traduzindo descrição...");
                    var summary = igdbGame.Summary;
                    if (!string.IsNullOrEmpty(summary))
                        summary = await TranslationService.TranslateToPortugueseAsync(summary);

                    game.Summary     = summary;
                    game.IgdbRating  = igdbGame.Rating;
                    game.IgdbId      = igdbGame.Id;
                    game.ReleaseYear = igdbGame.ReleaseYear;
                    game.IsSummaryTranslated = true;

                    var genres = igdbGame.GenreNames;
                    game.Genres = genres == "\u2014" ? null : TranslationService.TranslateGenres(genres);
                }
            }
            progressDialog.UpdateProgress(90, "Informações IGDB concluídas!");

            SaveGames();

            // Passo 6/6 — Finalizado (90→100%)
            progressDialog.UpdateProgress(100, "Tudo pronto!");
            StatusMessage = $"'{game.DisplayName}' adicionado com sucesso!";

            await Task.Delay(600); // pequena pausa para o usuário ver 100%
            progressDialog.Finish();

            // Força atualização da tela de detalhes e lista
            _gamesView.Refresh();
            if (SelectedGame == game)
            {
                DetailGame = null;
                DetailGame = game;
                ShowDetailPanel = true;
            }
        }
        catch
        {
            StatusMessage = $"{Games.Count} jogos na biblioteca";
            try { progressDialog.Close(); } catch { }
        }
    }

    private async Task RefreshAllAssetsOnStartupAsync()
    {
        // 1. Visuais — SteamGridDB (logo, capa, fundo, ícone)
        var apiKey = SettingsService.Current.SteamGridDbApiKey;
        if (!string.IsNullOrEmpty(apiKey))
        {
            var visualGames = Games
                .Where(g => string.IsNullOrEmpty(g.LogoPath)
                         || string.IsNullOrEmpty(g.CustomImagePath)
                         || string.IsNullOrEmpty(g.BackgroundImagePath))
                .ToList();

            if (visualGames.Count > 0)
            {
                StatusMessage = $"Atualizando visuais de {visualGames.Count} jogo(s)...";
                foreach (var game in visualGames)
                    await AutoFetchSteamGridDbAssetsAsync(game);
            }
        }

        // 2. Texto — IGDB (sinopse, gêneros, nota, ano) + tradução PT-BR
        var clientId     = SettingsService.Current.IgdbClientId;
        var clientSecret = SettingsService.Current.IgdbClientSecret;
        if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
        {
            var textGames = Games
                .Where(g => !g.HasIgdbInfo)
                .ToList();

            if (textGames.Count > 0)
            {
                StatusMessage = $"Buscando informações de {textGames.Count} jogo(s) no IGDB...";
                foreach (var game in textGames)
                    await AutoFetchIgdbAsync(game);
            }

            // 3. Re-traduzir descrições que ficaram em inglês
            var untranslated = Games
                .Where(g => g.HasIgdbInfo && !string.IsNullOrEmpty(g.Summary) && !g.IsSummaryTranslated)
                .ToList();

            if (untranslated.Count > 0)
            {
                StatusMessage = $"Traduzindo descrições de {untranslated.Count} jogo(s)...";
                foreach (var game in untranslated)
                {
                    try
                    {
                        StatusMessage = $"Traduzindo '{game.DisplayName}'...";
                        game.Summary = await TranslationService.TranslateToPortugueseAsync(game.Summary!);
                        game.IsSummaryTranslated = true;
                        SaveGames();
                    }
                    catch { }
                }
            }
        }

        StatusMessage = $"{Games.Count} jogos na biblioteca";
    }

    private async Task AutoFetchSteamGridDbAssetsAsync(Game game)
    {
        var apiKey = SettingsService.Current.SteamGridDbApiKey;
        if (string.IsNullOrEmpty(apiKey))
            return;

        try
        {
            var svc = new SteamGridDbService(apiKey);
            var games = await svc.SearchGamesAsync(game.DisplayName);
            if (games.Count == 0) return;

            var sgdbId = games[0].Id;

            // Ícone
            if (string.IsNullOrEmpty(game.IconPath) || !System.IO.File.Exists(game.IconPath))
            {
                StatusMessage = $"Buscando ícone de '{game.DisplayName}'...";
                var icons = await svc.GetIconsAsync(sgdbId);
                if (icons.Count > 0)
                {
                    var path = await svc.DownloadIconAsync(icons[0].Url, game.DisplayName);
                    if (path is not null) game.IconPath = path;
                }
            }

            // Logo
            if (string.IsNullOrEmpty(game.LogoPath))
            {
                StatusMessage = $"Buscando logo de '{game.DisplayName}'...";
                var logos = await svc.GetLogosAsync(sgdbId);
                if (logos.Count > 0)
                {
                    var path = await svc.DownloadLogoAsync(logos[0].Url, game.DisplayName);
                    if (path is not null) game.LogoPath = path;
                }
            }

            // Capa (Grid)
            if (string.IsNullOrEmpty(game.CustomImagePath))
            {
                StatusMessage = $"Buscando capa de '{game.DisplayName}'...";
                var covers = await svc.GetCoversAsync(sgdbId);
                if (covers.Count > 0)
                {
                    var path = await svc.DownloadCoverAsync(covers[0].Url, game.DisplayName);
                    if (path is not null) game.CustomImagePath = path;
                }
            }

            // Fundo (Hero)
            if (string.IsNullOrEmpty(game.BackgroundImagePath))
            {
                StatusMessage = $"Buscando fundo de '{game.DisplayName}'...";
                var heroes = await svc.GetHeroesAsync(sgdbId);
                if (heroes.Count > 0)
                {
                    var path = await svc.DownloadHeroAsync(heroes[0].Url, game.DisplayName);
                    if (path is not null) game.BackgroundImagePath = path;
                }
            }

            SaveGames();
            StatusMessage = $"Assets de '{game.DisplayName}' atualizados!";
        }
        catch
        {
            StatusMessage = $"{Games.Count} jogos na biblioteca";
        }
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
            game.IsSummaryTranslated = true;

            // Traduz gêneros para PT-BR
            var genres = igdbGame.GenreNames;
            game.Genres = genres == "\u2014" ? null : TranslationService.TranslateGenres(genres);

            SaveGames();
            StatusMessage = $"'{game.DisplayName}' — info IGDB aplicada!";
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
        var visible = GetVisibleGames();
        var idx = visible.IndexOf(game);
        Games.Remove(game);
        SaveGames();
        StatusMessage = $"'{game.DisplayName}' removido. {Games.Count} jogos na biblioteca";
        visible = GetVisibleGames();
        if (visible.Count > 0)
            SelectedGame = visible[Math.Clamp(idx, 0, visible.Count - 1)];
        else
            SelectedGame = null;
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
    private void SearchBackground(Game game)
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

        var dialog = new BackgroundSearchDialog(apiKey, game.DisplayName)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && dialog.DownloadedBackgroundPath is not null)
        {
            game.BackgroundImagePath = dialog.DownloadedBackgroundPath;
            SaveGames();
            StatusMessage = $"Fundo de '{game.DisplayName}' atualizado!";
        }
    }

    [RelayCommand]
    private void OpenTheme()
    {
        var dialog = new ThemeDialog { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void OpenHelp()
    {
        var dialog = new HelpDialog { Owner = Application.Current.MainWindow };
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

            SaveGames();
            StatusMessage = $"'{game.DisplayName}' — info IGDB aplicada!";

            // Auto-busca visuais do SteamGridDB (logo, capa, fundo)
            await AutoFetchSteamGridDbAssetsAsync(game);

            SaveGames();
            StatusMessage = $"'{game.DisplayName}' atualizado!";
        }
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

            switch (button)
            {
                case GamepadButton.DPadDown:
                case GamepadButton.DPadRight:
                    NavigateBy(1, visible);
                    break;
                case GamepadButton.DPadUp:
                case GamepadButton.DPadLeft:
                    NavigateBy(-1, visible);
                    break;
                case GamepadButton.RightShoulder:
                    NavigateBy(5, visible);
                    break;
                case GamepadButton.LeftShoulder:
                    NavigateBy(-5, visible);
                    break;
                case GamepadButton.A:
                    if (SelectedGame is not null)
                        LaunchGame(SelectedGame);
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
                    if (!string.IsNullOrEmpty(SearchText))
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

    }


