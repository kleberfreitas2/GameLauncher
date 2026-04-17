namespace GameLauncher.Models;

/// <summary>Dados de progresso dos troféus — persistidos em JSON.</summary>
public class TrophyProgress
{
    public HashSet<TrophyId> Unlocked        { get; set; } = [];
    public Dictionary<TrophyId, DateTime> UnlockDates { get; set; } = [];

    // Estatísticas rastreadas
    public double LauncherMinutes           { get; set; }   // tempo total com o app aberto
    public double PlayedMinutes             { get; set; }   // tempo jogando via GLauncher
    public int    GamesAdded               { get; set; }
    public int    GamesDetailOpened        { get; set; }
    public int    FavoritesAdded           { get; set; }
    public int    AiUsageCount             { get; set; }
    public int    BigPictureCount          { get; set; }
    public bool   AvatarChanged            { get; set; }
    public bool   BackgroundChanged        { get; set; }
    public bool   SettingsOpened           { get; set; }
    public bool   DiscordConnected         { get; set; }
    public bool   XboxConnected            { get; set; }
    public bool   SteamConnected           { get; set; }
    public bool   EpicConnected            { get; set; }
    public bool   RenamedGame              { get; set; }
    public bool   EasterEggFound           { get; set; }
}
