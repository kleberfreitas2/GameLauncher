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
    private HardwareMonitorService _hwMonitor = null!;
    private XInputService _xinput = null!;
    public  XInputService XInput => _xinput;
    private readonly Dispatcher _dispatcher;
    private int _selectedIndex = -1;
    private string? _runningGameName;
    public  string? RunningGameName => _runningGameName;

    // ── Sistema de troféus ────────────────────────────────────────────────────
    private readonly TrophyService _trophyService = new();
    private DispatcherTimer? _launcherTimeTimer;
    public  TrophyService TrophyService => _trophyService;

    public enum NavZone { Header, Actions, Carousel }

    private static readonly string[] HeaderItems = ["Xbox", "Steam", "Epic", "Discord", "Help", "Settings", "AddGame", "Theme"];

    [ObservableProperty] private NavZone activeZone = NavZone.Carousel;
    [ObservableProperty] private int headerIndex;
    [ObservableProperty] private string focusedHeaderItem = "";

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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGamepadBattery))]
    private bool gamepadConnected;
    [ObservableProperty] private string gamepadStatus = "";
    [ObservableProperty] private bool isAnimationLoading;
    [ObservableProperty] private bool isDiscordLoading;
    [ObservableProperty] private bool isSoundEnabled = SettingsService.Current.SoundEnabled;
    [ObservableProperty] private bool isFpsOverlayEnabled = SettingsService.Current.FpsOverlayEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BigPictureIcon))]
    private bool isBigPictureMode;

    public string BigPictureIcon => IsBigPictureMode ? "Television" : "TelevisionClassic";

    /// <summary>
    /// Callback injetado pela View para executar a animação de transição.
    /// Parâmetro: true = entrando, false = saindo do Big Picture.
    /// </summary>
    public Action<bool>? BigPictureTransitionRequested { get; set; }

    [RelayCommand]
    private void ToggleBigPictureMode()
    {
        bool entering = !IsBigPictureMode;
        IsBigPictureMode = entering;
        // O som de entrada é tocado pelo BigPictureTransitionService; apenas navegar na saída
        if (!entering) SoundService.PlayNavigate();
        StatusMessage = entering ? "Modo Big Picture ativado" : "Modo Big Picture desativado";
        if (entering) _trophyService.OnBigPictureUsed();

        if (entering)
        {
            // Ao entrar no Big Picture, garantir que o foco está no carrossel com um jogo selecionado
            ActiveZone = NavZone.Carousel;
            var visible = GetVisibleGames();
            if (visible.Count > 0 && SelectedGame is null)
            {
                _selectedIndex = 0;
                SelectedGame = visible[0];
            }
            else if (SelectedGame is not null)
            {
                _selectedIndex = visible.IndexOf(SelectedGame);
                if (_selectedIndex < 0) _selectedIndex = 0;
            }
            UpdateGamepadStatusForZone();
        }

        BigPictureTransitionRequested?.Invoke(entering);
    }

    private GpuCapabilities? _gpuCaps;
    [ObservableProperty] private ObservableCollection<TechCompatItem> techCompatItems = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGamepadBattery))]
    [NotifyPropertyChangedFor(nameof(GamepadBatteryIcon))]
    private int gamepadBatteryLevel = -1;

    public bool HasGamepadBattery => GamepadBatteryLevel >= 0 && GamepadConnected;

    // ── Recomendação gráfica ──────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHwRecommendation))]
    private Models.GraphicsRecommendation? hwRecommendation;

    public bool HasHwRecommendation => HwRecommendation is not null;

    [RelayCommand]
    private void OpenHwRecommendation()
    {
        if (DetailGame is null) return;
        var dlg = new Views.HwRecommendationDialog(
            DetailGame.DisplayName,
            GpuName, CpuName, RamTotal)
        {
            Owner = Application.Current.MainWindow
        };
        dlg.ShowDialog();
    }

    public string GamepadBatteryIcon => GamepadBatteryLevel switch
    {
        >= 80 => "Battery",
        >= 50 => "Battery70",
        >= 20 => "Battery40",
        >= 0  => "Battery10",
        _     => "BatteryUnknown"
    };

    private bool _isContextMenuOpen;
    public bool IsContextMenuOpen
    {
        get => _isContextMenuOpen;
        set => SetProperty(ref _isContextMenuOpen, value);
    }

    public Action<GamepadButton>? ContextMenuNavigate { get; set; }

    private bool _isHelpDialogOpen;
    public bool IsHelpDialogOpen
    {
        get => _isHelpDialogOpen;
        set => SetProperty(ref _isHelpDialogOpen, value);
    }

    public Action<GamepadButton>? HelpDialogNavigate { get; set; }

    public Action<double>? HelpDialogScroll { get; set; }

    private bool _isProfileDialogOpen;
    public bool IsProfileDialogOpen
    {
        get => _isProfileDialogOpen;
        set => SetProperty(ref _isProfileDialogOpen, value);
    }

    public Action<GamepadButton>? ProfileDialogNavigate { get; set; }

    private bool _isTrophyDialogOpen;
    public bool IsTrophyDialogOpen
    {
        get => _isTrophyDialogOpen;
        set => SetProperty(ref _isTrophyDialogOpen, value);
    }

    public Action<double>? TrophyDialogScroll { get; set; }

    private FpsOverlayWindow? _fpsOverlay;

    [ObservableProperty] private string currentTime = DateTime.Now.ToString("H:mm");
    [ObservableProperty] private string playerName = SettingsService.Current.PlayerName;

    // ── Troféus: exibição no header ───────────────────────────────────────
    [ObservableProperty] private string trophyGamerscoreDisplay = "0G";
    [ObservableProperty] private string trophyCountDisplay      = "0/27 troféus";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvatar))]
    [NotifyPropertyChangedFor(nameof(HasNoAvatar))]
    private string? avatarPath = string.IsNullOrEmpty(SettingsService.Current.AvatarImagePath) ? null : SettingsService.Current.AvatarImagePath;

    public bool HasAvatar   => !string.IsNullOrEmpty(AvatarPath);
    public bool HasNoAvatar => !HasAvatar;

    private XboxLiveService? _xboxService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsXboxLoggedIn))]
    [NotifyPropertyChangedFor(nameof(XboxButtonText))]
    private XboxProfile? xboxProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsXboxLoggedIn))]
    [NotifyPropertyChangedFor(nameof(XboxButtonText))]
    private bool xboxConnected;

    public bool IsXboxLoggedIn => XboxConnected && XboxProfile is not null;
    public string XboxButtonText => IsXboxLoggedIn ? XboxProfile!.Gamertag : "XBOX";

    private SteamService? _steamService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSteamConnected))]
    [NotifyPropertyChangedFor(nameof(SteamButtonText))]
    private SteamProfile? steamProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSteamConnected))]
    [NotifyPropertyChangedFor(nameof(SteamButtonText))]
    private bool steamConnected;

    public bool IsSteamConnected => SteamConnected && SteamProfile is not null;
    public string SteamButtonText => IsSteamConnected ? SteamProfile!.PersonaName : "STEAM";

    private EpicGamesService? _epicService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEpicConnected))]
    [NotifyPropertyChangedFor(nameof(EpicButtonText))]
    private EpicProfile? epicProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEpicConnected))]
    [NotifyPropertyChangedFor(nameof(EpicButtonText))]
    private bool epicConnected;

    public bool IsEpicConnected => EpicConnected && EpicProfile is not null;
    public string EpicButtonText => IsEpicConnected ? EpicProfile!.DisplayName : "EPIC";

    private DiscordService? _discordService;
    private DiscordRichPresenceService? _discordRpc;
    private DiscordRpcService? _discordRpcPanel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDiscordConnected))]
    [NotifyPropertyChangedFor(nameof(DiscordButtonText))]
    private DiscordProfile? discordProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDiscordConnected))]
    [NotifyPropertyChangedFor(nameof(DiscordButtonText))]
    private bool discordConnected;

    public bool IsDiscordConnected => DiscordConnected && DiscordProfile is not null;
    public string DiscordButtonText => IsDiscordConnected ? DiscordProfile!.DisplayName : "DISCORD";

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
        _gamesView.SortDescriptions.Add(new SortDescription(nameof(Game.SortOrder), ListSortDirection.Ascending));

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _clockTimer.Tick += (_, _) => CurrentTime = DateTime.Now.ToString("H:mm");
        _clockTimer.Start();
    }

    /// <summary>
    /// Heavy initialization that runs without blocking the UI thread.
    /// Call after construction, before showing the main window.
    /// </summary>
    public async Task InitializeAsync(Action<string>? statusCallback = null)
    {
        statusCallback?.Invoke("Carregando biblioteca de jogos...");
        var games = await Task.Run(() =>
        {
            try
            {
                if (!File.Exists(SaveFilePath)) return null;
                var json = File.ReadAllText(SaveFilePath);
                return JsonSerializer.Deserialize<List<Game>>(json);
            }
            catch { return null; }
        });

        if (games is not null)
        {
            // Assign SortOrder to legacy games that don't have one
            bool needsReorder = games.All(g => g.SortOrder == 0);
            if (needsReorder)
            {
                for (int i = 0; i < games.Count; i++)
                    games[i].SortOrder = i;
            }

            foreach (var g in games)
                Games.Add(g);
            StatusMessage = $"{Games.Count} jogos na biblioteca";
            _gamesView.Refresh();
            if (_gamesView.Cast<Game>().FirstOrDefault() is { } first)
                SelectedGame = first;
        }

        statusCallback?.Invoke("Iniciando serviços...");
        await Task.Run(() =>
        {
            SoundService.Initialize();
        });

        _hwMonitor = new HardwareMonitorService();
        _hwMonitor.MetricsUpdated += OnMetricsUpdated;
        _hwMonitor.Start();

        _xinput = new XInputService();
        _xinput.ButtonPressed += OnGamepadButton;
        _xinput.ConnectionChanged += OnGamepadConnectionChanged;
        _xinput.RightStickY += OnRightStickY;
        _xinput.BatteryChanged += OnBatteryChanged;
        _xinput.AiComboTriggered += OnAiComboTriggered;
        _xinput.Start();

        // ── Troféus: subscrever ANTES dos restores de plataformas ────────────
        _trophyService.TrophyUnlocked += OnTrophyUnlocked;
        _trophyService.OnAppStarted();
        RefreshTrophyHeader();
        _launcherTimeTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _launcherTimeTimer.Tick += (_, _) => _trophyService.AddLauncherMinutes(1);
        _launcherTimeTimer.Start();

        statusCallback?.Invoke("Conectando contas...");
        _ = RefreshAllAssetsOnStartupAsync();
        _ = TryRestoreXboxSessionAsync();
        _ = TryRestoreSteamSessionAsync();
        _ = TryRestoreEpicSessionAsync();
        _ = TryRestoreDiscordSessionAsync();
    }

    private void OnMetricsUpdated(HardwareMetrics m)
    {
        _dispatcher.BeginInvoke(() =>
        {
            CpuUsage = Math.Round(m.CpuUsage);
            CpuTemp = Math.Round(m.CpuTemp);
            GpuUsage = Math.Round(m.GpuUsage);
            GpuTemp = Math.Round(m.GpuTemp);
            RamUsage = Math.Round(m.RamUsage);
            CpuTempText = m.CpuTemp > 0 ? $"{m.CpuTemp:F0}°C" : "--°C";
            GpuTempText = m.GpuTemp > 0 ? $"{m.GpuTemp:F0}°C" : "--°C";

            if (!string.IsNullOrEmpty(m.CpuName) && string.IsNullOrEmpty(CpuName))
                CpuName = m.CpuName;
            if (!string.IsNullOrEmpty(m.GpuName) && string.IsNullOrEmpty(GpuName))
            {
                GpuName = m.GpuName;
                _gpuCaps = GpuCapabilityService.Detect(m.GpuName);
                if (DetailGame is not null)
                    ScanGameTech(DetailGame);
            }
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
        _launcherTimeTimer?.Stop();
        _clockTimer.Stop();
        _xinput.Stop();
        _xinput.Dispose();
        _hwMonitor.Stop();
        _hwMonitor.Dispose();
        _xboxService?.Dispose();
        _steamService?.Dispose();
        _epicService?.Dispose();
        _discordService?.Dispose();
        _discordRpc?.Dispose();
        _discordRpcPanel?.Dispose();
        _fpsOverlay?.Close();
        _fpsOverlay = null;
    }

    // ── Fila de toasts (UI thread) ────────────────────────────────────────────
    private readonly Queue<Models.Trophy> _toastQueue = new();
    private bool _toastBusy;

    private void OnTrophyUnlocked(Models.Trophy trophy)
    {
        // Garante execução na UI thread — TrophyService pode vir de qualquer thread
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(() => OnTrophyUnlocked(trophy));
            return;
        }

        RefreshTrophyHeader();
        _xinput.Vibrate(0.3, 0.8, 600);
        _toastQueue.Enqueue(trophy);
        ShowNextToast();
    }

    private void ShowNextToast()
    {
        if (_toastBusy || _toastQueue.Count == 0) return;
        _toastBusy = true;
        var next = _toastQueue.Dequeue();
        var toast = new Views.TrophyToastWindow(next, () =>
        {
            _toastBusy = false;
            ShowNextToast();
        });
        toast.Show();
        SoundService.PlayTrophy();
    }

    private void RefreshTrophyHeader()
    {
        TrophyGamerscoreDisplay = $"{_trophyService.EarnedGamerscore}G";
        TrophyCountDisplay      = $"{_trophyService.TrophyCount}/{_trophyService.TotalTrophies} troféus";
    }

    private void ScanGameTech(Game game)
    {
        if (game.TechInfo is null)
        {
            game.TechInfo = GameTechDetectorService.Scan(game.GameRootDirectory);
            if (game.TechInfo.HasAnyTech)
                SaveGames();
        }

        TechCompatItems.Clear();
        var tech = game.TechInfo;
        if (!tech.HasAnyTech) return;

        var gpu = _gpuCaps;

        if (!string.IsNullOrEmpty(tech.DirectXVersion))
            AddTechItem("DirectX", tech.DirectXVersion, gpu?.SupportsDirectX12 == true
                ? CompatStatus.Compatible : CompatStatus.Unknown, "Gpu");

        if (tech.HasVulkan)
            AddTechItem("Vulkan", "Sim", gpu?.SupportsVulkan == true
                ? CompatStatus.Compatible : CompatStatus.Unknown, "Gpu");

        if (tech.HasRayTracing)
            AddTechItem("Ray Tracing", "Sim", gpu is null ? CompatStatus.Unknown
                : gpu.SupportsRayTracing ? CompatStatus.Compatible : CompatStatus.Incompatible, "FlashOutline");

        if (tech.HasDLSS)
            AddTechItem("DLSS", tech.DlssVersion ?? "Sim", gpu is null ? CompatStatus.Unknown
                : gpu.SupportsDLSS ? CompatStatus.Compatible : CompatStatus.Incompatible, "NvidiaShield");

        if (tech.HasFSR)
            AddTechItem("AMD FSR", tech.FsrVersion ?? "Sim", gpu is null ? CompatStatus.Unknown
                : gpu.SupportsFSR ? CompatStatus.Compatible : CompatStatus.Incompatible, "Gpu");

        if (tech.HasXeSS)
            AddTechItem("Intel XeSS", "Sim", gpu is null ? CompatStatus.Unknown
                : gpu.SupportsXeSS ? CompatStatus.Compatible : CompatStatus.Incompatible, "IntelligenceOutline");

        if (tech.HasFrameGeneration)
            AddTechItem("Frame Generation", "Sim", gpu is null ? CompatStatus.Unknown
                : gpu.SupportsFrameGeneration ? CompatStatus.Compatible : CompatStatus.Incompatible, "MotionPlayOutline");

        if (tech.HasHDR)
            AddTechItem("HDR", "Sim", gpu?.SupportsHDR == true
                ? CompatStatus.Compatible : CompatStatus.Unknown, "Brightness7");
    }

    private void AddTechItem(string name, string detail, CompatStatus status, string icon)
    {
        TechCompatItems.Add(new TechCompatItem
        {
            TechName = name,
            Detail = detail,
            Status = status,
            IconKind = icon,
            StatusText = status switch
            {
                CompatStatus.Compatible => "✓ Compatível",
                CompatStatus.Incompatible => "✗ Não suportado",
                _ => "? Desconhecido"
            },
            StatusColor = status switch
            {
                CompatStatus.Compatible => "#00E676",
                CompatStatus.Incompatible => "#FF5252",
                _ => "#FFD740"
            }
        });
    }

    [RelayCommand]
    private void RescanGameTech()
    {
        if (DetailGame is null) return;
        DetailGame.TechInfo = null;
        ScanGameTech(DetailGame);
        StatusMessage = $"Tecnologias de '{DetailGame.DisplayName}' re-escaneadas";
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
        _trophyService.OnAvatarChanged();
    }

    partial void OnSearchTextChanged(string value) => _gamesView.Refresh();

    partial void OnSelectedGameChanged(Game? value)
    {
        DetailGame = value;
        ShowDetailPanel = value is not null;
        if (value is not null)
        {
            SoundService.PlayNavigate();
            ScanGameTech(value);
            _trophyService.OnGameDetailOpened();
        }
        else
        {
            TechCompatItems.Clear();
        }
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

            if (_gamesView.Cast<Game>().FirstOrDefault() is { } first)
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

    public void ReorderGame(Game dragged, Game target)
    {
        if (dragged == target) return;

        // Get the visible (sorted) list
        var visible = _gamesView.Cast<Game>().ToList();
        var oldIndex = visible.IndexOf(dragged);
        var newIndex = visible.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex) return;

        visible.RemoveAt(oldIndex);
        visible.Insert(newIndex, dragged);

        // Reassign SortOrder preserving favorites-first grouping
        for (int i = 0; i < visible.Count; i++)
            visible[i].SortOrder = i;

        _gamesView.Refresh();
        SaveGames();
    }

    private int NextSortOrder() => Games.Count > 0 ? Games.Max(g => g.SortOrder) + 1 : 0;

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
                IconPath = IconExtractor.ExtractIcon(file),
                SortOrder = NextSortOrder()
            };

            Games.Add(game);
            newGames.Add(game);
        }

        SaveGames();
        StatusMessage = $"{Games.Count} jogos na biblioteca";
        _trophyService.OnGameAdded(Games.Count);

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
            progressDialog.UpdateProgress(0, "Buscando ícone...");
            var apiKey = SettingsService.Current.SteamGridDbApiKey;
            SteamGridDbService? svc = null;
            int sgdbId = 0;
            bool hasSteamGridDb = false;

            if (!string.IsNullOrEmpty(apiKey))
            {
                svc = new SteamGridDbService(apiKey);
                var sgdbGames = await svc.SearchGamesAsync(game.DisplayName);
                if (sgdbGames.Count == 0 && IsUnauthorizedError(svc.LastError))
                {
                    svc = null;
                }
                else if (sgdbGames.Count > 0)
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

            progressDialog.UpdateProgress(100, "Tudo pronto!");
            StatusMessage = $"'{game.DisplayName}' adicionado com sucesso!";

            await Task.Delay(600);
            progressDialog.Finish();

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
            if (games.Count == 0 && IsUnauthorizedError(svc.LastError))
            {
                StatusMessage = "Erro temporário ao acessar SteamGridDB.";
                return;
            }
            if (games.Count == 0) return;

            var sgdbId = games[0].Id;

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
    private void ChangeLogo(Game game)
    {
        var dialog = new OpenFileDialog
        {
            Title = $"Escolha um logotipo para '{game.DisplayName}'",
            Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true) return;

        game.LogoPath = dialog.FileName;
        SaveGames();
        StatusMessage = $"Logotipo de '{game.DisplayName}' atualizado!";
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
            SoundService.PlayLaunch();
            _xinput.Vibrate(0.6, 0.4, 400);
            _runningGameName = game.DisplayName;

            var workDir = string.IsNullOrEmpty(game.InstallDirectory)
                ? Path.GetDirectoryName(game.ExecutablePath) ?? string.Empty
                : game.InstallDirectory;

            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = game.ExecutablePath,
                UseShellExecute = true,
                WorkingDirectory = workDir
            });
            game.LastPlayed = DateTime.Now;
            SaveGames();
            StatusMessage = $"Iniciando {game.DisplayName}...";

            SetDiscordRichPresence(game.DisplayName);

            _xinput.Stop();
            var mainWin = Application.Current.MainWindow;
            if (mainWin is not null)
                mainWin.WindowState = WindowState.Minimized;

            if (SettingsService.Current.FpsOverlayEnabled)
            {
                _fpsOverlay = new FpsOverlayWindow();
                _fpsOverlay.Show();
            }

            // Mostra dica da IA com a hotkey após o jogo iniciar
            var aiHint = new AiHintOverlay(_xinput);
            aiHint.Show();

            var launchTime = DateTime.UtcNow;
            _ = Task.Run(async () =>
            {
                var playedMinutes = await GameProcessMonitor.WaitForGameExitAsync(
                    game.ExecutablePath, proc);

                await _dispatcher.BeginInvoke(() =>
                {
                    game.TotalPlayTimeMinutes += playedMinutes;
                    _trophyService.AddPlayedMinutes(playedMinutes);
                    SaveGames();
                    CleanupAfterGameExit();

                    if (mainWin is not null)
                    {
                        mainWin.Show();
                        mainWin.WindowState = WindowState.Normal;
                        mainWin.Activate();
                    }
                    _xinput.Start();
                    StatusMessage = $"{Games.Count} jogos na biblioteca";
                });
            });
        }
        catch (Exception ex)
        {
            SoundService.PlayError();
            CleanupAfterGameExit();
            _xinput.Start();
            StatusMessage = $"Erro ao iniciar {game.DisplayName}: {ex.Message}";
            Debug.WriteLine($"LaunchGame error: {ex}");
        }
    }

    private void CleanupAfterGameExit()
    {
        ClearDiscordRichPresence();
        _runningGameName = null;
        _fpsOverlay?.Close();
        _fpsOverlay = null;

        // Fecha o chat da IA que foi aberto como overlay durante o jogo
        foreach (var w in Application.Current.Windows.OfType<AiAssistantDialog>().ToList())
            w.Close();
    }

    [RelayCommand]
    private void ToggleFpsOverlay()
    {
        SettingsService.Current.FpsOverlayEnabled = !SettingsService.Current.FpsOverlayEnabled;
        SettingsService.Save();
        IsFpsOverlayEnabled = SettingsService.Current.FpsOverlayEnabled;
        StatusMessage = SettingsService.Current.FpsOverlayEnabled
            ? "FPS Overlay ativado — será exibido durante os jogos"
            : "FPS Overlay desativado";
    }

    [RelayCommand]
    private void ToggleFavorite(Game game)
    {
        game.IsFavorite = !game.IsFavorite;
        SoundService.PlayFavorite();
        _gamesView.Refresh();
        SaveGames();
        if (game.IsFavorite) _trophyService.OnFavoriteAdded();
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
            _trophyService.OnGameRenamed();
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
        _trophyService.OnSettingsOpened();
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void ToggleSound()
    {
        SettingsService.Current.SoundEnabled = !SettingsService.Current.SoundEnabled;
        SettingsService.Save();
        IsSoundEnabled = SettingsService.Current.SoundEnabled;
        StatusMessage = SettingsService.Current.SoundEnabled
            ? "Sons ativados 🔊"
            : "Sons desativados 🔇";
        if (SettingsService.Current.SoundEnabled)
            SoundService.PlaySelect();
    }

    [RelayCommand]
    private void OpenHelp()
    {
        var dialog = new HelpDialog { Owner = Application.Current.MainWindow };
        IsHelpDialogOpen = true;
        HelpDialogNavigate = dialog.HandleGamepadInput;
        HelpDialogScroll = dialog.HandleRightStickScroll;
        dialog.ShowDialog();
        IsHelpDialogOpen = false;
        HelpDialogNavigate = null;
        HelpDialogScroll = null;
    }

    [RelayCommand]
    private void OpenAiAssistant()
    {
        // Aberto pelo launcher → chat geral, sem foco em jogo específico
        OpenAiAssistantCore(viaGamepad: false, fromLauncher: true);
    }

    private void OnAiComboTriggered()
    {
        // Ativado por hotkey/controle durante o jogo → focado no jogo em execução
        _dispatcher.BeginInvoke(() => OpenAiAssistantCore(viaGamepad: true, fromLauncher: false));
    }

    private void OpenAiAssistantCore(bool viaGamepad, bool fromLauncher = false)
    {
        // Prioridade: Groq (grátis) → OpenAI (pago)
        var groqKey   = SettingsService.Current.GroqApiKey;
        var openAiKey = SettingsService.Current.OpenAiApiKey;

        AiProvider provider;
        string apiKey;

        if (!string.IsNullOrWhiteSpace(groqKey))
        {
            provider = AiProvider.Groq;
            apiKey   = groqKey;
        }
        else if (!string.IsNullOrWhiteSpace(openAiKey))
        {
            provider = AiProvider.OpenAI;
            apiKey   = openAiKey;
        }
        else
        {
            var dlg = new GroqKeyDialog { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;
            SettingsService.Current.GroqApiKey = dlg.ApiKey;
            SettingsService.Save();
            provider = AiProvider.Groq;
            apiKey   = dlg.ApiKey;
        }

        // Quando aberto pelo launcher: chat geral (sem jogo em foco).
        // Quando aberto por hotkey/controle durante o jogo: focado no jogo em execução.
        string? gameName = fromLauncher ? null : _runningGameName;

        var chatDialog = new AiAssistantDialog(provider, apiKey, gameName, viaGamepad, _xinput)
        {
            Owner = Application.Current.MainWindow
        };
        chatDialog.Show();
        _trophyService.OnAiUsed();
    }

    [RelayCommand]
    private void OpenDonation()
    {
        var dialog = new PixDonationDialog { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void OpenTrophies()
    {
        var dialog = new Views.TrophiesDialog(_trophyService)
        {
            Owner = Application.Current.MainWindow
        };
        _trophyService.OnSettingsOpened();
        IsTrophyDialogOpen = true;
        TrophyDialogScroll = dialog.ScrollBy;
        dialog.ShowDialog();
        IsTrophyDialogOpen = false;
        TrophyDialogScroll = null;
    }

    /// <summary>Chamado pelo code-behind quando o Easter Egg é ativado.</summary>
    public void NotifyEasterEggFound() => _trophyService.OnEasterEggFound();


    private async Task TryRestoreXboxSessionAsync()
    {
        var clientId = SettingsService.Current.XboxClientId;
        if (string.IsNullOrEmpty(clientId) || clientId.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase))
            return;

        _xboxService?.Dispose();
        _xboxService = new XboxLiveService(clientId);

        var restored = await _xboxService.TrySilentLoginAsync();
        if (!restored) return;

        var profile = await _xboxService.GetProfileAsync();
        if (profile is not null)
        {
            XboxProfile = profile;
            XboxConnected = true;
            _trophyService.OnXboxConnected();
            StatusMessage = $"Xbox Live: {profile.Gamertag} — Gamerscore: {profile.Gamerscore:N0}";
        }
    }

    [RelayCommand]
    private async Task XboxLogin()
    {
        var clientId = SettingsService.Current.XboxClientId;

        if (string.IsNullOrEmpty(clientId) || clientId == AppSettings.DefaultXboxClientId)
        {
            if (!string.IsNullOrEmpty(AppSettings.DefaultXboxClientId)
                && AppSettings.DefaultXboxClientId != "REPLACE_WITH_YOUR_XBOX_CLIENT_ID")
            {
                clientId = AppSettings.DefaultXboxClientId;
            }
            else
            {
                var setup = new XboxSetupDialog { Owner = Application.Current.MainWindow };
                if (setup.ShowDialog() != true) return;
                SettingsService.Current.XboxClientId = setup.ClientId;
                SettingsService.Save();
                clientId = setup.ClientId;
            }
        }

        StatusMessage = "Conectando ao Xbox Live...";

        _xboxService?.Dispose();
        _xboxService = new XboxLiveService(clientId);

        var success = await _xboxService.LoginAsync();
        if (!success)
        {
            StatusMessage = "Falha ao conectar ao Xbox Live.";
            MessageBox.Show(
                "Não foi possível autenticar com o Xbox Live.\n\n" +
                "Verifique se o Client ID está correto e se o app Azure possui a permissão Xboxlive.signin.",
                "Xbox Live", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StatusMessage = "Carregando perfil Xbox...";
        var profile = await _xboxService.GetProfileAsync();

        if (profile is not null)
        {
            XboxProfile = profile;
            XboxConnected = true;
            _trophyService.OnXboxConnected();
            StatusMessage = $"Xbox Live: {profile.Gamertag} — Gamerscore: {profile.Gamerscore:N0}";
        }
        else
        {
            XboxConnected = true;
            StatusMessage = "Xbox Live conectado (perfil indisponível).";
        }
    }

    [RelayCommand]
    private async Task OpenXboxProfile()
    {
        if (!IsXboxLoggedIn || XboxProfile is null)
        {
            await XboxLogin();
            return;
        }

        var importedCount = Games.Count(g =>
            g.InstallDirectory is not null &&
            g.InstallDirectory.Contains("XboxGames", StringComparison.OrdinalIgnoreCase));

        var availableCount = 0;
        if (_xboxService is not null)
        {
            availableCount = await _xboxService.GetLibraryGamesCountAsync();
        }

        var dialog = new XboxProfileDialog(XboxProfile, importedCount, availableCount)
        {
            Owner = Application.Current.MainWindow
        };

        IsProfileDialogOpen = true;
        ProfileDialogNavigate = dialog.HandleGamepadInput;

        if (dialog.ShowDialog() == true)
        {
            if (dialog.LogoutRequested)
            {
                if (_xboxService is not null)
                    await _xboxService.LogoutAsync();

                XboxProfile = null;
                XboxConnected = false;
                StatusMessage = "Desconectado do Xbox Live.";
            }
            else if (dialog.ImportGamesRequested)
            {
                await ImportXboxGames();
            }
        }

        IsProfileDialogOpen = false;
        ProfileDialogNavigate = null;
    }

    [RelayCommand]
    private async Task ImportXboxGames()
    {
        if (_xboxService is null)
        {
            StatusMessage = "Conecte-se ao Xbox Live primeiro.";
            return;
        }

        StatusMessage = "Escaneando jogos Xbox instalados...";

        var xboxGames = await Task.Run(() => _xboxService.ScanXboxInstalledGames());

        if (xboxGames.Count == 0)
        {
            StatusMessage = "Nenhum jogo Xbox encontrado nas pastas padrão (XboxGames).";
            MessageBox.Show(
                "Nenhum jogo Xbox Game Pass encontrado.\n\n" +
                "Verifique se há jogos instalados nas pastas XboxGames dos seus discos.",
                "Xbox Games", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        int added = 0;
        var newGames = new List<Game>();

        foreach (var xg in xboxGames)
        {
            if (Games.Any(g => g.ExecutablePath.Equals(xg.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                continue;

            xg.SortOrder = NextSortOrder();
            Games.Add(xg);
            newGames.Add(xg);
            added++;
        }

        SaveGames();
        _gamesView.Refresh();
        StatusMessage = added > 0
            ? $"{added} jogo(s) Xbox importado(s)! {Games.Count} jogos na biblioteca."
            : "Todos os jogos Xbox já estavam na biblioteca.";

        foreach (var game in newGames)
        {
            await AutoFetchAllWithProgressAsync(game);
        }
    }


    private async Task TryRestoreSteamSessionAsync()
    {
        var steamId = SettingsService.Current.SteamId;

        if (string.IsNullOrEmpty(steamId))
            steamId = SteamService.DetectLocalSteamId();

        if (string.IsNullOrEmpty(steamId))
            return;

        _steamService?.Dispose();
        _steamService = new SteamService();

        var connected = await _steamService.ConnectAsync(steamId);
        if (!connected) return;

        if (string.IsNullOrEmpty(SettingsService.Current.SteamId))
        {
            SettingsService.Current.SteamId = steamId;
            SettingsService.Save();
        }

        var profile = await _steamService.GetProfileAsync();
        if (profile is not null)
        {
            profile.OwnedGamesCount = _steamService.GetInstalledGamesCount();
            SteamProfile = profile;
            SteamConnected = true;
            _trophyService.OnSteamConnected();
            StatusMessage = $"Steam: {profile.PersonaName} — {profile.OwnedGamesCount:N0} jogos";
        }
    }

    [RelayCommand]
    private async Task SteamLogin()
    {
        var steamId = SteamService.DetectLocalSteamId()
                      ?? SettingsService.Current.SteamId;

        if (string.IsNullOrEmpty(steamId))
        {
            var setup = new SteamSetupDialog
            {
                Owner = Application.Current.MainWindow
            };

            if (setup.ShowDialog() != true) return;

            steamId = setup.SteamIdOrVanity;
        }

        StatusMessage = "Conectando à Steam...";

        _steamService?.Dispose();
        _steamService = new SteamService();

        var success = await _steamService.ConnectAsync(steamId);
        if (!success)
        {
            StatusMessage = "Falha ao conectar à Steam.";
            MessageBox.Show(
                "Não foi possível conectar à Steam.\n\n" +
                "Verifique se o Steam está instalado e você está logado.",
                "Steam", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SettingsService.Current.SteamId = _steamService.SteamId ?? steamId;
        SettingsService.Save();

        StatusMessage = "Carregando perfil Steam...";
        var profile = await _steamService.GetProfileAsync();

        if (profile is not null)
        {
            profile.OwnedGamesCount = _steamService.GetInstalledGamesCount();
            SteamProfile = profile;
            SteamConnected = true;
            _trophyService.OnSteamConnected();
            StatusMessage = $"Steam: {profile.PersonaName} — {profile.OwnedGamesCount:N0} jogos";
        }
        else
        {
            SteamConnected = true;
            StatusMessage = "Steam conectada (perfil indisponível).";
        }
    }

    [RelayCommand]
    private async Task OpenSteamProfile()
    {
        if (!IsSteamConnected || SteamProfile is null)
        {
            await SteamLogin();
            return;
        }

        var importedCount = Games.Count(g =>
            g.InstallDirectory is not null &&
            g.InstallDirectory.Contains("steamapps", StringComparison.OrdinalIgnoreCase));

        var availableCount = SteamProfile.OwnedGamesCount;

        var dialog = new SteamProfileDialog(SteamProfile, importedCount, availableCount)
        {
            Owner = Application.Current.MainWindow
        };

        IsProfileDialogOpen = true;
        ProfileDialogNavigate = dialog.HandleGamepadInput;

        if (dialog.ShowDialog() == true)
        {
            if (dialog.LogoutRequested)
            {
                _steamService?.Disconnect();
                _steamService?.Dispose();
                _steamService = null;

                SteamProfile = null;
                SteamConnected = false;
                SettingsService.Current.SteamId = string.Empty;
                SettingsService.Save();
                StatusMessage = "Desconectado da Steam.";
            }
            else if (dialog.ImportGamesRequested)
            {
                await ImportSteamGames();
            }
        }

        IsProfileDialogOpen = false;
        ProfileDialogNavigate = null;
    }

    [RelayCommand]
    private async Task ImportSteamGames()
    {
        if (_steamService is null)
        {
            StatusMessage = "Conecte-se à Steam primeiro.";
            return;
        }

        StatusMessage = "Escaneando jogos Steam instalados...";

        var steamGames = await Task.Run(() => _steamService.ScanSteamInstalledGames());

        if (steamGames.Count == 0)
        {
            StatusMessage = "Nenhum jogo Steam instalado encontrado.";
            MessageBox.Show(
                "Nenhum jogo Steam instalado encontrado.\n\n" +
                "Verifique se há jogos instalados nas pastas do Steam.",
                "Steam Games", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        int added = 0;
        var newGames = new List<Game>();

        foreach (var sg in steamGames)
        {
            if (Games.Any(g => g.ExecutablePath.Equals(sg.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                continue;

            sg.SortOrder = NextSortOrder();
            Games.Add(sg);
            newGames.Add(sg);
            added++;
        }

        SaveGames();
        _gamesView.Refresh();
        StatusMessage = added > 0
            ? $"{added} jogo(s) Steam importado(s)! {Games.Count} jogos na biblioteca."
            : "Todos os jogos Steam já estavam na biblioteca.";

        foreach (var game in newGames)
        {
            await AutoFetchAllWithProgressAsync(game);
        }
    }


    private Task TryRestoreEpicSessionAsync()
    {
        _epicService?.Dispose();
        _epicService = new EpicGamesService();

        var connected = _epicService.Connect();
        if (!connected) return Task.CompletedTask;

        var profile = _epicService.GetProfile();
        if (profile is not null)
        {
            EpicProfile = profile;
            EpicConnected = true;
            _trophyService.OnEpicConnected();
            StatusMessage = $"Epic Games: {profile.DisplayName} — {profile.InstalledGamesCount:N0} jogos";
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task EpicLogin()
    {
        var epicPath = EpicGamesService.DetectEpicInstallPath();

        if (string.IsNullOrEmpty(epicPath))
        {
            var setup = new EpicSetupDialog
            {
                Owner = Application.Current.MainWindow
            };

            if (setup.ShowDialog() != true) return Task.CompletedTask;

            epicPath = setup.EpicPath;
        }

        StatusMessage = "Conectando à Epic Games...";

        _epicService?.Dispose();
        _epicService = new EpicGamesService();

        var success = _epicService.Connect(epicPath);
        if (!success)
        {
            StatusMessage = "Falha ao conectar à Epic Games.";
            MessageBox.Show(
                "Não foi possível detectar jogos da Epic Games.\n\n" +
                "Verifique se o Epic Games Launcher está instalado e há jogos instalados.",
                "Epic Games", MessageBoxButton.OK, MessageBoxImage.Warning);
            return Task.CompletedTask;
        }

        var profile = _epicService.GetProfile();

        if (profile is not null)
        {
            EpicProfile = profile;
            EpicConnected = true;
            _trophyService.OnEpicConnected();
            StatusMessage = $"Epic Games: {profile.DisplayName} — {profile.InstalledGamesCount:N0} jogos";
        }
        else
        {
            EpicConnected = true;
            StatusMessage = "Epic Games conectada (perfil indisponível).";
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenEpicProfile()
    {
        if (!IsEpicConnected || EpicProfile is null)
        {
            await EpicLogin();
            return;
        }

        var importedCount = Games.Count(g =>
            g.InstallDirectory is not null &&
            (g.InstallDirectory.Contains("Epic Games", StringComparison.OrdinalIgnoreCase) ||
             g.InstallDirectory.Contains("Epic", StringComparison.OrdinalIgnoreCase)));

        var availableCount = EpicProfile.InstalledGamesCount;

        var dialog = new EpicProfileDialog(EpicProfile, importedCount, availableCount)
        {
            Owner = Application.Current.MainWindow
        };

        IsProfileDialogOpen = true;
        ProfileDialogNavigate = dialog.HandleGamepadInput;

        if (dialog.ShowDialog() == true)
        {
            if (dialog.LogoutRequested)
            {
                _epicService?.Disconnect();
                _epicService?.Dispose();
                _epicService = null;

                EpicProfile = null;
                EpicConnected = false;
                StatusMessage = "Desconectado da Epic Games.";
            }
            else if (dialog.ImportGamesRequested)
            {
                await ImportEpicGames();
            }
        }

        IsProfileDialogOpen = false;
        ProfileDialogNavigate = null;
    }

    [RelayCommand]
    private async Task ImportEpicGames()
    {
        if (_epicService is null)
        {
            StatusMessage = "Conecte-se à Epic Games primeiro.";
            return;
        }

        StatusMessage = "Escaneando jogos Epic Games instalados...";

        var epicGames = await Task.Run(() => _epicService.ScanEpicInstalledGames());

        if (epicGames.Count == 0)
        {
            StatusMessage = "Nenhum jogo Epic Games instalado encontrado.";
            MessageBox.Show(
                "Nenhum jogo da Epic Games encontrado.\n\n" +
                "Verifique se há jogos instalados pelo Epic Games Launcher.",
                "Epic Games", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        int added = 0;
        var newGames = new List<Game>();

        foreach (var eg in epicGames)
        {
            if (Games.Any(g => g.ExecutablePath.Equals(eg.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                continue;

            eg.SortOrder = NextSortOrder();
            Games.Add(eg);
            newGames.Add(eg);
            added++;
        }

        SaveGames();
        _gamesView.Refresh();
        StatusMessage = added > 0
            ? $"{added} jogo(s) Epic importado(s)! {Games.Count} jogos na biblioteca."
            : "Todos os jogos Epic já estavam na biblioteca.";

        foreach (var game in newGames)
        {
            await AutoFetchAllWithProgressAsync(game);
        }
    }


    private async Task TryRestoreDiscordSessionAsync()
    {
        if (!DiscordService.HasCachedToken())
            return;

        var clientId = SettingsService.Current.DiscordClientId;
        if (string.IsNullOrEmpty(clientId))
            clientId = AppSettings.DefaultDiscordClientId;

        var clientSecret = SettingsService.Current.DiscordClientSecret;
        if (string.IsNullOrEmpty(clientSecret))
            clientSecret = AppSettings.DefaultDiscordClientSecret;

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return;

        IsDiscordLoading = true;
        try
        {
            _discordService?.Dispose();
            _discordService = new DiscordService(clientId, clientSecret);

            var restored = await _discordService.TrySilentLoginAsync();
            if (!restored) return;

            var profile = await _discordService.GetProfileAsync();
            if (profile is not null)
            {
                DiscordProfile = profile;
                DiscordConnected = true;
                _trophyService.OnDiscordConnected();
                StatusMessage = $"Discord: {profile.DisplayName}";

                var rid = SettingsService.Current.DiscordClientId;
                if (string.IsNullOrEmpty(rid)) rid = AppSettings.DefaultDiscordClientId;
                if (!string.IsNullOrEmpty(rid)) InitDiscordRpcPanel(rid);
            }
        }
        finally
        {
            IsDiscordLoading = false;
        }
    }

    [RelayCommand]
    private async Task DiscordLogin()
    {
        var clientId = SettingsService.Current.DiscordClientId;

        if (string.IsNullOrEmpty(clientId) || clientId == AppSettings.DefaultDiscordClientId)
        {
            if (!string.IsNullOrEmpty(AppSettings.DefaultDiscordClientId))
            {
                clientId = AppSettings.DefaultDiscordClientId;
            }
            else
            {
                var setup = new DiscordSetupDialog { Owner = Application.Current.MainWindow };
                if (setup.ShowDialog() != true) return;
                clientId = setup.ClientId;
                SettingsService.Current.DiscordClientId = clientId;
                SettingsService.Save();
            }
        }

        StatusMessage = "Conectando ao Discord...";

        var clientSecret = SettingsService.Current.DiscordClientSecret;
        if (string.IsNullOrEmpty(clientSecret))
            clientSecret = AppSettings.DefaultDiscordClientSecret;

        _discordService?.Dispose();
        _discordService = new DiscordService(clientId, clientSecret);

        var success = await _discordService.LoginAsync();
        if (!success)
        {
            StatusMessage = "Falha ao conectar ao Discord.";
            MessageBox.Show(
                "Não foi possível autenticar com o Discord.\n\n" +
                "Verifique se o Client ID está correto e se o redirect URI\n" +
                "http://localhost:9547/callback está configurado no app.",
                "Discord", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsDiscordLoading = true;
        try
        {
            StatusMessage = "Carregando perfil Discord...";
            var profile = await _discordService.GetProfileAsync();

            if (profile is not null)
            {
                DiscordProfile = profile;
                DiscordConnected = true;
                _trophyService.OnDiscordConnected();
                StatusMessage = $"Discord: {profile.DisplayName}";
            }
            else
            {
                DiscordConnected = true;
                StatusMessage = "Discord conectado (perfil indisponível).";
            }

            InitDiscordRpcPanel(clientId);
        }
        finally
        {
            IsDiscordLoading = false;
        }
    }

    private void InitDiscordRpcPanel(string clientId)
    {
        try
        {
            _discordRpcPanel?.Dispose();
            _discordRpcPanel = new DiscordRpcService(clientId);

            if (!_discordRpcPanel.TryConnect())
            {
                _discordRpcPanel = null;
                return;
            }

            var token = _discordService?.AccessToken;
            if (!string.IsNullOrEmpty(token))
            {
                if (_discordRpcPanel.Authenticate(token))
                    _discordRpcPanel.SubscribeNotifications();
            }
        }
        catch
        {
            _discordRpcPanel?.Dispose();
            _discordRpcPanel = null;
        }
    }

    [RelayCommand]
    private async Task OpenDiscordProfile()
    {
        if (!IsDiscordConnected || DiscordProfile is null)
        {
            await DiscordLogin();
            return;
        }

        var dialog = new DiscordPanelDialog(_discordRpcPanel, _discordService)
        {
            Owner = Application.Current.MainWindow
        };

        IsProfileDialogOpen = true;
        ProfileDialogNavigate = dialog.HandleGamepadInput;

        dialog.ShowDialog();

        IsProfileDialogOpen = false;
        ProfileDialogNavigate = null;

        if (dialog.LogoutRequested && _discordService is not null)
        {
            await _discordService.LogoutAsync();
            _discordRpcPanel?.Dispose();
            _discordRpcPanel = null;
            DiscordProfile = null;
            DiscordConnected = false;
            StatusMessage = "Discord desconectado.";
        }
    }

    private void SetDiscordRichPresence(string gameName)
    {
        try
        {
            var clientId = SettingsService.Current.DiscordClientId;
            if (string.IsNullOrEmpty(clientId))
                clientId = AppSettings.DefaultDiscordClientId;

            if (string.IsNullOrEmpty(clientId))
                return;

            _discordRpc ??= new DiscordRichPresenceService(clientId);
            _discordRpc.SetActivity(gameName, DateTimeOffset.UtcNow);
        }
        catch { }
    }

    private void ClearDiscordRichPresence()
    {
        try
        {
            _discordRpc?.ClearActivity();
        }
        catch { }
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


    private void OnGamepadConnectionChanged(bool connected)
    {
        _dispatcher.BeginInvoke(() =>
        {
            GamepadConnected = connected;
            var name = _xinput.ControllerName;
            bool isPlayStation = name.Contains("DualSense", StringComparison.OrdinalIgnoreCase)
                              || name.Contains("DualShock", StringComparison.OrdinalIgnoreCase);
            var buttons = isPlayStation ? "✕ = Jogar  |  △ = Favorito" : "A = Jogar  |  Y = Favorito";
            GamepadStatus = connected
                ? string.IsNullOrEmpty(name)
                    ? $"🎮 Controle conectado  |  {buttons}"
                    : $"🎮 {name} conectado  |  {buttons}"
                : "";
            if (!connected)
                GamepadBatteryLevel = -1;
            if (connected)
            {
                ActiveZone = NavZone.Carousel;
                FocusedHeaderItem = "";
                if (SelectedGame is null && GetVisibleGames().Count > 0)
                {
                    _selectedIndex = 0;
                    SelectedGame = GetVisibleGames()[0];
                }
            }
            if (!connected)
            {
                SelectedGame = null;
                _selectedIndex = -1;
                FocusedHeaderItem = "";
                ActiveZone = NavZone.Carousel;
            }
        });
    }

    private void OnBatteryChanged(int percent)
    {
        _dispatcher.BeginInvoke(() =>
        {
            GamepadBatteryLevel = percent;
        });
    }

    private void OnGamepadButton(GamepadButton button)
    {
        _dispatcher.BeginInvoke(() =>
        {
            if (IsAnimationLoading) return;

            if (IsHelpDialogOpen)
            {
                HelpDialogNavigate?.Invoke(button);
                return;
            }

            if (IsTrophyDialogOpen)
            {
                if (button == GamepadButton.B)
                    Application.Current.Windows.OfType<Views.TrophiesDialog>().FirstOrDefault()?.Close();
                return;
            }

            if (IsProfileDialogOpen)
            {
                ProfileDialogNavigate?.Invoke(button);
                return;
            }

            if (IsContextMenuOpen)
            {
                ContextMenuNavigate?.Invoke(button);
                return;
            }

            switch (button)
            {
                case GamepadButton.DPadUp:
                    SwitchZone(-1);
                    return;
                case GamepadButton.DPadDown:
                    SwitchZone(1);
                    return;

                case GamepadButton.DPadLeft:
                    NavigateInZone(-1);
                    return;
                case GamepadButton.DPadRight:
                    NavigateInZone(1);
                    return;

                case GamepadButton.LeftShoulder:
                    if (ActiveZone == NavZone.Carousel)
                    {
                        var vis = GetVisibleGames();
                        if (vis.Count > 0) { NavigateCarousel(-5, vis); SoundService.PlayNavigate(); }
                    }
                    return;
                case GamepadButton.RightShoulder:
                    if (ActiveZone == NavZone.Carousel)
                    {
                        var vis = GetVisibleGames();
                        if (vis.Count > 0) { NavigateCarousel(5, vis); SoundService.PlayNavigate(); }
                    }
                    return;

                case GamepadButton.A:
                    // No Big Picture, A sempre lança o jogo selecionado no carrossel
                    if (IsBigPictureMode && ActiveZone == NavZone.Carousel && SelectedGame is not null)
                    {
                        SoundService.PlaySelect();
                        LaunchGame(SelectedGame);
                    }
                    else
                    {
                        ActivateCurrentItem();
                    }
                    return;

                case GamepadButton.Y:
                    if (SelectedGame is not null)
                        ToggleFavorite(SelectedGame);
                    return;
                case GamepadButton.X:
                    if (SelectedGame is not null)
                        SearchCover(SelectedGame);
                    return;
                case GamepadButton.B:
                    HandleBack();
                    return;

                case GamepadButton.Start:
                    OpenSettingsMenu();
                    return;
                case GamepadButton.Back:
                    OpenHelp();
                    return;

                case GamepadButton.RightThumb:
                    ToggleBigPictureMode();
                    return;
            }
        });
    }

    private void OnRightStickY(double value)
    {
        _dispatcher.BeginInvoke(() =>
        {
            if (IsHelpDialogOpen)
                HelpDialogScroll?.Invoke(value);
            else if (IsTrophyDialogOpen)
                TrophyDialogScroll?.Invoke(value);
        });
    }

    private void SwitchZone(int direction)
    {
        var zones = Enum.GetValues<NavZone>();
        int current = (int)ActiveZone;
        int next = Math.Clamp(current + direction, 0, zones.Length - 1);

        if ((NavZone)next == NavZone.Actions && !ShowDetailPanel)
            next = Math.Clamp(next + direction, 0, zones.Length - 1);

        if (next == current) return;

        SoundService.PlayZoneChange();
        ActiveZone = (NavZone)next;

        switch (ActiveZone)
        {
            case NavZone.Header:
                FocusedHeaderItem = HeaderItems[HeaderIndex];
                break;
            case NavZone.Carousel:
                FocusedHeaderItem = "";
                var visible = GetVisibleGames();
                if (visible.Count > 0 && (_selectedIndex < 0 || _selectedIndex >= visible.Count))
                {
                    _selectedIndex = 0;
                    SelectedGame = visible[0];
                }
                break;
            case NavZone.Actions:
                FocusedHeaderItem = "";
                break;
        }

        UpdateGamepadStatusForZone();
    }

    private void NavigateInZone(int direction)
    {
        switch (ActiveZone)
        {
            case NavZone.Header:
                HeaderIndex = Math.Clamp(HeaderIndex + direction, 0, HeaderItems.Length - 1);
                FocusedHeaderItem = HeaderItems[HeaderIndex];
                SoundService.PlayNavigate();
                break;

            case NavZone.Carousel:
                var visible = GetVisibleGames();
                if (visible.Count > 0)
                {
                    NavigateCarousel(direction, visible);
                    SoundService.PlayNavigate();
                }
                break;

            case NavZone.Actions:
                break;
        }
    }

    private void NavigateCarousel(int delta, List<Game> visible)
    {
        if (_selectedIndex < 0 || _selectedIndex >= visible.Count)
            _selectedIndex = 0;

        int newIndex = Math.Clamp(_selectedIndex + delta, 0, visible.Count - 1);
        _selectedIndex = newIndex;
        SelectedGame = visible[newIndex];
    }

    private void ActivateCurrentItem()
    {
        SoundService.PlaySelect();
        switch (ActiveZone)
        {
            case NavZone.Header:
                ActivateHeaderItem(HeaderItems[HeaderIndex]);
                break;

            case NavZone.Carousel:
                if (SelectedGame is not null)
                    LaunchGame(SelectedGame);
                break;

            case NavZone.Actions:
                if (SelectedGame is not null)
                    LaunchGame(SelectedGame);
                break;
        }
    }

    private void ActivateHeaderItem(string item)
    {
        switch (item)
        {
            case "Xbox":
                OpenXboxProfileCommand.Execute(null);
                break;
            case "Steam":
                OpenSteamProfileCommand.Execute(null);
                break;
            case "Discord":
                OpenDiscordProfileCommand.Execute(null);
                break;
            case "Help":
                OpenHelp();
                break;
            case "Settings":
                OpenSettingsMenu();
                break;
            case "AddGame":
                AddGameCommand.Execute(null);
                break;
            case "Theme":
                OpenTheme();
                break;
        }
    }

    private void HandleBack()
    {
        SoundService.PlayBack();
        if (ActiveZone == NavZone.Header)
        {
            ActiveZone = NavZone.Carousel;
            FocusedHeaderItem = "";
            UpdateGamepadStatusForZone();
        }
        else if (ActiveZone == NavZone.Actions)
        {
            ActiveZone = NavZone.Carousel;
            UpdateGamepadStatusForZone();
        }
        else if (!string.IsNullOrEmpty(SearchText))
        {
            SearchText = string.Empty;
        }
    }

    private void OpenSettingsMenu()
    {
        _dispatcher.BeginInvoke(() =>
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow?.FindName("BtnGear") is System.Windows.Controls.Button gearBtn
                && gearBtn.ContextMenu is not null)
            {
                gearBtn.ContextMenu.PlacementTarget = gearBtn;
                gearBtn.ContextMenu.IsOpen = true;
            }
        });
    }

    private void UpdateGamepadStatusForZone()
    {
        var name = _xinput.ControllerName;
        bool isPS = name.Contains("DualSense", StringComparison.OrdinalIgnoreCase)
                 || name.Contains("DualShock", StringComparison.OrdinalIgnoreCase);

        string hint = ActiveZone switch
        {
            NavZone.Header => isPS
                ? "⬅➡ Navegar  |  ✕ = Selecionar  |  ⬇ Jogos"
                : "⬅➡ Navegar  |  A = Selecionar  |  ⬇ Jogos",
            NavZone.Actions => isPS
                ? "✕ = Jogar  |  ⬆ Menu  |  ⬇ Jogos"
                : "A = Jogar  |  ⬆ Menu  |  ⬇ Jogos",
            NavZone.Carousel => isPS
                ? "⬅➡ Jogos  |  ✕ = Jogar  |  △ = Favorito  |  ⬆ Ações"
                : "⬅➡ Jogos  |  A = Jogar  |  Y = Favorito  |  ⬆ Ações",
            _ => ""
        };

        string prefix = string.IsNullOrEmpty(name) ? "🎮" : $"🎮 {name}";
        string battery = GamepadBatteryLevel >= 0 ? $"  |  🔋 {GamepadBatteryLevel}%" : "";
        GamepadStatus = $"{prefix}{battery}  |  {hint}";
    }

    private List<Game> GetVisibleGames() =>
        _gamesView.Cast<Game>().ToList();

    private static bool IsUnauthorizedError(string? error)
    {
        return error is not null &&
               (error.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("API key", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("401", StringComparison.Ordinal));
    }

    }

