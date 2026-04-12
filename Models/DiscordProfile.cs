namespace GameLauncher.Models;

public class DiscordProfile
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string GlobalName { get; set; } = string.Empty;
    public string AvatarHash { get; set; } = string.Empty;
    public string Discriminator { get; set; } = string.Empty;

    public string AvatarUrl => !string.IsNullOrEmpty(AvatarHash)
        ? $"https://cdn.discordapp.com/avatars/{Id}/{AvatarHash}.png?size=256"
        : $"https://cdn.discordapp.com/embed/avatars/{(int.TryParse(Id, out var id) ? (id >> 22) % 6 : 0)}.png";

    public string DisplayName => !string.IsNullOrEmpty(GlobalName) ? GlobalName : Username;
}
