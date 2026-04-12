namespace GameLauncher.Models;

public class XboxProfile
{
    public string Xuid { get; set; } = string.Empty;
    public string Gamertag { get; set; } = string.Empty;
    public string GameDisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public int Gamerscore { get; set; }
    public string AccountTier { get; set; } = string.Empty;
}
