using System.IO;
using System.Text.Json;
using GameLauncher.Models;

namespace GameLauncher.Services;

/// <summary>Gerencia o sistema de troféus do GLauncher.</summary>
public class TrophyService
{
    private static readonly string SavePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GameLauncher", "trophies.json");

    private readonly List<Trophy> _all;
    private TrophyProgress _progress;

    /// <summary>Disparado quando um troféu é desbloqueado. Arg: troféu conquistado.</summary>
    public event Action<Trophy>? TrophyUnlocked;

    public TrophyService()
    {
        _all      = [.. Trophy.CreateAll()];
        _progress = Load();
        ApplySavedState();
    }

    // ── Acesso ──────────────────────────────────────────────────────────────

    public IReadOnlyList<Trophy>  All      => _all;
    public TrophyProgress         Progress => _progress;

    public int TotalGamerscore    => _all.Sum(t => t.Gamerscore);
    public int EarnedGamerscore   => _all.Where(t => t.IsUnlocked).Sum(t => t.Gamerscore);
    public int TrophyCount        => _all.Count(t => t.IsUnlocked);
    public int TotalTrophies      => _all.Count;

    public string ProgressSummary =>
        $"{EarnedGamerscore}/{TotalGamerscore}G  •  {TrophyCount}/{TotalTrophies} troféus";

    // ── Eventos de trigger (chamados pelo ViewModel) ────────────────────────

    public void OnAppStarted()
    {
        TryUnlock(TrophyId.Welcome);
        Save();
    }

    public void OnGameAdded(int totalGames)
    {
        _progress.GamesAdded = Math.Max(_progress.GamesAdded, totalGames);
        TryUnlock(TrophyId.FirstGame);
        if (_progress.GamesAdded >= 10)  TryUnlock(TrophyId.Collector10);
        if (_progress.GamesAdded >= 25)  TryUnlock(TrophyId.Library25);
        if (_progress.GamesAdded >= 50)  TryUnlock(TrophyId.Completionist50);
        Save();
    }

    public void OnGameDetailOpened()
    {
        _progress.GamesDetailOpened++;
        if (_progress.GamesDetailOpened >= 5) TryUnlock(TrophyId.Explorer);
        Save();
    }

    public void OnAvatarChanged()
    {
        _progress.AvatarChanged = true;
        TryUnlock(TrophyId.CustomAvatar);
        Save();
    }

    public void OnBackgroundChanged()
    {
        _progress.BackgroundChanged = true;
        TryUnlock(TrophyId.Decorator);
        Save();
    }

    public void OnSettingsOpened()
    {
        _progress.SettingsOpened = true;
        TryUnlock(TrophyId.Configurator);
        Save();
    }

    public void OnDiscordConnected()
    {
        _progress.DiscordConnected = true;
        TryUnlock(TrophyId.SocialDiscord);
        CheckAllInOne();
        Save();
    }

    public void OnXboxConnected()
    {
        _progress.XboxConnected = true;
        TryUnlock(TrophyId.XboxLive);
        CheckAllInOne();
        Save();
    }

    public void OnSteamConnected()
    {
        _progress.SteamConnected = true;
        TryUnlock(TrophyId.SteamGamer);
        CheckAllInOne();
        Save();
    }

    public void OnEpicConnected()
    {
        _progress.EpicConnected = true;
        TryUnlock(TrophyId.EpicGamer);
        CheckAllInOne();
        Save();
    }

    public void OnGameRenamed()
    {
        _progress.RenamedGame = true;
        TryUnlock(TrophyId.Organizer);
        Save();
    }

    public void OnFavoriteAdded()
    {
        _progress.FavoritesAdded++;
        if (_progress.FavoritesAdded >= 3) TryUnlock(TrophyId.Favorited3);
        Save();
    }

    public void OnAiUsed()
    {
        _progress.AiUsageCount++;
        TryUnlock(TrophyId.AiAssistant);
        if (_progress.AiUsageCount >= 10) TryUnlock(TrophyId.AiExpert);
        Save();
    }

    public void OnBigPictureUsed()
    {
        _progress.BigPictureCount++;
        TryUnlock(TrophyId.BigPictureBeginner);
        if (_progress.BigPictureCount >= 5) TryUnlock(TrophyId.BigPicturePro);
        Save();
    }

    public void OnEasterEggFound()
    {
        _progress.EasterEggFound = true;
        TryUnlock(TrophyId.EasterEggHunter);
        Save();
    }

    /// <summary>Adiciona minutos de sessão (chamado pelo timer periódico).</summary>
    public void AddLauncherMinutes(double minutes)
    {
        _progress.LauncherMinutes += minutes;
        if (_progress.LauncherMinutes >= 60)    TryUnlock(TrophyId.OneHourLauncher);
        if (_progress.LauncherMinutes >= 300)   TryUnlock(TrophyId.Veteran5h);
        if (_progress.LauncherMinutes >= 1440)  TryUnlock(TrophyId.Legendary24h);
        Save();
    }

    /// <summary>Adiciona minutos jogados (após o jogo fechar).</summary>
    public void AddPlayedMinutes(double minutes)
    {
        _progress.PlayedMinutes += minutes;
        if (_progress.PlayedMinutes >= 180)  TryUnlock(TrophyId.Marathoner3h);
        if (_progress.PlayedMinutes >= 600)  TryUnlock(TrophyId.RealGamer10h);
        Save();
    }

    // ── Internos ────────────────────────────────────────────────────────────

    private void CheckAllInOne()
    {
        if (_progress.XboxConnected && _progress.SteamConnected && _progress.EpicConnected)
            TryUnlock(TrophyId.AllInOne);
    }

    private void TryUnlock(TrophyId id)
    {
        if (_progress.Unlocked.Contains(id)) return;

        var trophy = _all.FirstOrDefault(t => t.Id == id);
        if (trophy is null) return;

        trophy.IsUnlocked = true;
        trophy.UnlockedAt = DateTime.Now;
        _progress.Unlocked.Add(id);
        _progress.UnlockDates[id] = trophy.UnlockedAt.Value;

        TrophyUnlocked?.Invoke(trophy);

        // Verifica se todos (exceto o Master) foram desbloqueados
        var allExceptMaster = _all
            .Where(t => t.Id != TrophyId.GlauncherMaster)
            .All(t => t.IsUnlocked);
        if (allExceptMaster)
            TryUnlock(TrophyId.GlauncherMaster);
    }

    private void ApplySavedState()
    {
        foreach (var t in _all)
        {
            t.IsUnlocked = _progress.Unlocked.Contains(t.Id);
            if (t.IsUnlocked && _progress.UnlockDates.TryGetValue(t.Id, out var dt))
                t.UnlockedAt = dt;
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
            File.WriteAllText(SavePath,
                JsonSerializer.Serialize(_progress, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static TrophyProgress Load()
    {
        try
        {
            if (!File.Exists(SavePath)) return new TrophyProgress();
            var json = File.ReadAllText(SavePath);
            return JsonSerializer.Deserialize<TrophyProgress>(json) ?? new TrophyProgress();
        }
        catch { return new TrophyProgress(); }
    }
}
