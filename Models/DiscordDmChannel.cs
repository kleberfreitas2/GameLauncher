namespace GameLauncher.Models;

public class DiscordDmChannel
{
    public string ChannelId { get; set; } = string.Empty;
    public bool IsGroup { get; set; }
    public string? GroupName { get; set; }
    public List<DiscordDmRecipient> Recipients { get; set; } = [];

    public string DisplayName => IsGroup && !string.IsNullOrEmpty(GroupName)
        ? GroupName
        : IsGroup
            ? string.Join(", ", Recipients.Select(r => r.DisplayName))
            : Recipients.FirstOrDefault()?.DisplayName ?? "Desconhecido";

    public string AvatarUrl => !IsGroup && Recipients.Count == 1
        ? Recipients[0].AvatarUrl
        : string.Empty;

    public string MemberCountText => IsGroup ? $"{Recipients.Count} membros" : string.Empty;
}

public class DiscordDmRecipient
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string GlobalName { get; set; } = string.Empty;
    public string? AvatarHash { get; set; }

    public string DisplayName => !string.IsNullOrEmpty(GlobalName) ? GlobalName : Username;

    public string AvatarUrl => !string.IsNullOrEmpty(AvatarHash)
        ? $"https://cdn.discordapp.com/avatars/{Id}/{AvatarHash}.png?size=64"
        : $"https://cdn.discordapp.com/embed/avatars/{(int.TryParse(Id, out var id) ? (id >> 22) % 6 : 0)}.png";
}
