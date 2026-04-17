using System.IO;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GameLauncher.Models;

public partial class Game : ObservableObject
{
    private List<string> _tags = [];
    public List<string> Tags
    {
        get => _tags;
        set
        {
            if (SetProperty(ref _tags, value))
                OnPropertyChanged(nameof(TagsDisplay));
        }
    }

    public void NotifyTagsChanged()
    {
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(TagsDisplay));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string name = string.Empty;

    public string ExecutablePath { get; set; } = string.Empty;
    public string? InstallDirectory { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastPlayedText))]
    private DateTime? lastPlayed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteIcon))]
    [NotifyPropertyChangedFor(nameof(FavoriteColor))]
    private bool isFavorite;

    [ObservableProperty]
    private int sortOrder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayTimeDisplay))]
    private double totalPlayTimeMinutes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveImagePath))]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    [NotifyPropertyChangedFor(nameof(HasNoImage))]
    private string? iconPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveImagePath))]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    [NotifyPropertyChangedFor(nameof(HasNoImage))]
    private string? customImagePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveBackgroundPath))]
    [NotifyPropertyChangedFor(nameof(HasBackground))]
    private string? backgroundImagePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogo))]
    [NotifyPropertyChangedFor(nameof(HasNoLogo))]
    private string? logoPath;

    public bool HasLogo   => !string.IsNullOrEmpty(LogoPath);
    public bool HasNoLogo => !HasLogo;

    public int?    IgdbId      { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIgdbInfo))]
    [NotifyPropertyChangedFor(nameof(SummaryDisplay))]
    private string? summary;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIgdbInfo))]
    [NotifyPropertyChangedFor(nameof(RatingDisplay))]
    private double? igdbRating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GenresDisplay))]
    private string? genres;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(YearDisplay))]
    private int? releaseYear;

    public bool    IsSummaryTranslated { get; set; }

    public GameTechInfo? TechInfo { get; set; }

    [JsonIgnore]
    public bool HasTechInfo => TechInfo is not null && TechInfo.HasAnyTech;

    public bool HasIgdbInfo => !string.IsNullOrEmpty(Summary) || IgdbRating.HasValue;

    public string RatingDisplay  => IgdbRating.HasValue ? $"{IgdbRating.Value:F0} / 100" : "—";
    public string YearDisplay    => ReleaseYear?.ToString() ?? "—";
    public string GenresDisplay  => !string.IsNullOrEmpty(Genres) ? Genres : "—";
    public string SummaryDisplay => !string.IsNullOrEmpty(Summary) ? Summary : "Sem descrição disponível.";

    private static readonly HashSet<string> _binSubfolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "binaries", "binary", "x64", "x86", "win64", "win32",
        "win_x64", "win_x86", "game", "build", "release", "debug"
    };

    public string GameRootDirectory
    {
        get
        {
            var exeDir = Path.GetDirectoryName(ExecutablePath);
            if (string.IsNullOrEmpty(exeDir))
                return InstallDirectory ?? string.Empty;

            var dir = new DirectoryInfo(exeDir);
            while (dir.Parent != null && _binSubfolders.Contains(dir.Name))
                dir = dir.Parent;

            return dir.FullName;
        }
    }

    private string? _installSizeCache;

    public string InstallSizeDisplay
    {
        get
        {
            if (_installSizeCache is not null) return _installSizeCache;

            var root = GameRootDirectory;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return _installSizeCache = "—";

            try
            {
                var dir = new DirectoryInfo(root);
                long bytes = dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);

                return _installSizeCache = bytes switch
                {
                    < 1024L              => $"{bytes} B",
                    < 1024L * 1024       => $"{bytes / 1024.0:F1} KB",
                    < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
                    _                     => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
                };
            }
            catch
            {
                return _installSizeCache = "—";
            }
        }
    }

    public string DisplayName => Name.Replace(".exe", "").Replace("_", " ");

    [JsonIgnore]
    public string PlatformSource
    {
        get
        {
            var path = InstallDirectory ?? ExecutablePath ?? "";
            if (path.Contains("XboxGames", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
                return "Instalado via Xbox - PC (Windows)";
            if (path.Contains("steamapps", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("Steam", StringComparison.OrdinalIgnoreCase))
                return "Instalado via Steam - PC (Windows)";
            if (path.Contains("Epic Games", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("EpicGames", StringComparison.OrdinalIgnoreCase))
                return "Instalado via Epic Games - PC (Windows)";
            return "Local - PC (Windows)";
        }
    }

    public string? EffectiveImagePath =>
        !string.IsNullOrEmpty(CustomImagePath) ? CustomImagePath :
        !string.IsNullOrEmpty(IconPath) ? IconPath : null;

    public bool HasImage   => EffectiveImagePath is not null;
    public bool HasNoImage => EffectiveImagePath is null;

    public string? EffectiveBackgroundPath =>
        !string.IsNullOrEmpty(BackgroundImagePath) ? BackgroundImagePath :
        EffectiveImagePath;

    public bool HasBackground => EffectiveBackgroundPath is not null;

    public string FavoriteIcon  => IsFavorite ? "Star" : "StarOutline";
    public string FavoriteColor => IsFavorite ? "#FFD700" : "#666688";

    public string PlayTimeDisplay
    {
        get
        {
            if (TotalPlayTimeMinutes < 1) return "Nunca jogado";
            if (TotalPlayTimeMinutes < 60) return $"{(int)TotalPlayTimeMinutes} min";
            var hours = TotalPlayTimeMinutes / 60.0;
            return hours < 100
                ? $"{hours:F1} horas"
                : $"{hours:F0} horas";
        }
    }

    public string TagsDisplay => Tags.Count > 0 ? string.Join(", ", Tags) : "—";

    public string LastPlayedText
    {
        get
        {
            if (!LastPlayed.HasValue) return "Nunca jogado";
            var diff = DateTime.Now - LastPlayed.Value;
            if (diff.TotalMinutes < 1)  return "Agora mesmo";
            if (diff.TotalHours   < 1)  return $"Há {(int)diff.TotalMinutes} min";
            if (diff.TotalDays    < 1)  return $"Há {(int)diff.TotalHours} h";
            if (diff.TotalDays    < 7)
            {
                var days = (int)diff.TotalDays;
                return days == 1 ? "Há 1 dia" : $"Há {days} dias";
            }
            if (diff.TotalDays    < 30)
            {
                var weeks = (int)(diff.TotalDays / 7);
                return weeks == 1 ? "Há 1 semana" : $"Há {weeks} semanas";
            }
            return LastPlayed.Value.ToString("dd/MM/yyyy");
        }
    }
}
