namespace GameLauncher.Models;

public class SteamProfile
{
    public string SteamId { get; set; } = string.Empty;
    public string PersonaName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string ProfileUrl { get; set; } = string.Empty;
    public int OwnedGamesCount { get; set; }
}
