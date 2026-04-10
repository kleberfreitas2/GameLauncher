using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GameLauncher.Models;

public partial class Game : ObservableObject
{
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

    // ── Metadados IGDB ──────────────────────────────────────
    public int?    IgdbId      { get; set; }
    public string? Summary     { get; set; }
    public double? IgdbRating  { get; set; }
    public string? Genres      { get; set; }
    public int?    ReleaseYear { get; set; }
    public bool    IsSummaryTranslated { get; set; }

    public bool HasIgdbInfo => !string.IsNullOrEmpty(Summary) || IgdbRating.HasValue;

    public string RatingDisplay  => IgdbRating.HasValue ? $"{IgdbRating.Value:F0} / 100" : "—";
    public string YearDisplay    => ReleaseYear?.ToString() ?? "—";
    public string GenresDisplay  => !string.IsNullOrEmpty(Genres) ? Genres : "—";
    public string SummaryDisplay => !string.IsNullOrEmpty(Summary) ? Summary : "Sem descrição disponível.";

    public string InstallSizeDisplay
    {
        get
        {
            if (string.IsNullOrEmpty(InstallDirectory) || !Directory.Exists(InstallDirectory))
                return "—";

            try
            {
                var dir = new DirectoryInfo(InstallDirectory);
                long bytes = dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);

                return bytes switch
                {
                    < 1024L              => $"{bytes} B",
                    < 1024L * 1024       => $"{bytes / 1024.0:F1} KB",
                    < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
                    _                     => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
                };
            }
            catch
            {
                return "—";
            }
        }
    }

    public string DisplayName => Name.Replace(".exe", "").Replace("_", " ");

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

    public string LastPlayedText
    {
        get
        {
            if (!LastPlayed.HasValue) return "Nunca jogado";
            var diff = DateTime.Now - LastPlayed.Value;
            if (diff.TotalMinutes < 1)  return "Agora mesmo";
            if (diff.TotalHours   < 1)  return $"Há {(int)diff.TotalMinutes} min";
            if (diff.TotalDays    < 1)  return $"Há {(int)diff.TotalHours} h";
            if (diff.TotalDays    < 7)  return $"Há {(int)diff.TotalDays} dias";
            if (diff.TotalDays    < 30) return $"Há {(int)(diff.TotalDays / 7)} sem.";
            return LastPlayed.Value.ToString("dd/MM/yyyy");
        }
    }
}
