namespace GameLauncher.Models;

public class AppSettings
{
    internal const string DefaultSteamGridDbApiKey  = "REPLACE_WITH_YOUR_STEAMGRIDDB_API_KEY";
    internal const string DefaultIgdbClientId       = "REPLACE_WITH_YOUR_IGDB_CLIENT_ID";
    internal const string DefaultIgdbClientSecret   = "REPLACE_WITH_YOUR_IGDB_CLIENT_SECRET";
    internal const string DefaultXboxClientId       = "2faf5e10-92f6-4ea6-838a-f84fb41facc3";
    internal const string DefaultSteamApiKey        = "STUEF87H4RCNKN78";

    public string SteamGridDbApiKey    { get; set; } = DefaultSteamGridDbApiKey;
    public string IgdbClientId         { get; set; } = DefaultIgdbClientId;
    public string IgdbClientSecret     { get; set; } = DefaultIgdbClientSecret;
    public string BackgroundImagePath  { get; set; } = string.Empty;
    public string AvatarImagePath      { get; set; } = string.Empty;
    public string PlayerName            { get; set; } = "Jogador";
    public string XboxClientId          { get; set; } = DefaultXboxClientId;
    public string SteamApiKey           { get; set; } = DefaultSteamApiKey;
    public string SteamId               { get; set; } = string.Empty;

    public bool SoundEnabled            { get; set; } = true;

    public string AccentColor          { get; set; } = "#7C4DFF";
    public string SecondaryAccentColor { get; set; } = "#00E676";
    public string BackgroundColor      { get; set; } = "#0D0D0D";
    public string HeaderColor          { get; set; } = "#16213E";
    public string CardColor            { get; set; } = "#1A1A2E";
    public string CardImageColor       { get; set; } = "#0F0F23";
}
