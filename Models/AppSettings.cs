namespace GameLauncher.Models;

public class AppSettings
{
    internal const string DefaultSteamGridDbApiKey  = "ff16d3eb3d9146c2d4046915a9fd2b55";
    internal const string DefaultIgdbClientId       = "2uvzi4sq1glaz1uz09w1qo48fg68kf";
    internal const string DefaultIgdbClientSecret   = "lpv5tjr4pdk15xpmxl097pgtqg3x5n";
    internal const string DefaultXboxClientId       = "2faf5e10-92f6-4ea6-838a-f84fb41facc3";
    internal const string DefaultSteamApiKey        = "STUEF87H4RCNKN78";
    internal const string DefaultDiscordClientId      = "1492859640827088896";
    internal const string DefaultDiscordClientSecret  = "A6lzAthuKWdSrGrcDK0RYjbdh2uQkgpL";

    public string SteamGridDbApiKey    { get; set; } = DefaultSteamGridDbApiKey;
    public string IgdbClientId         { get; set; } = DefaultIgdbClientId;
    public string IgdbClientSecret     { get; set; } = DefaultIgdbClientSecret;
    public string BackgroundImagePath  { get; set; } = string.Empty;
    public string AvatarImagePath      { get; set; } = string.Empty;
    public string PlayerName            { get; set; } = "Jogador";
    public string XboxClientId          { get; set; } = DefaultXboxClientId;
    public string SteamApiKey           { get; set; } = DefaultSteamApiKey;
    public string SteamId               { get; set; } = string.Empty;
    public string DiscordClientId       { get; set; } = DefaultDiscordClientId;
    public string DiscordClientSecret   { get; set; } = DefaultDiscordClientSecret;

    public bool SoundEnabled            { get; set; } = true;
    public bool FpsOverlayEnabled       { get; set; } = false;

    public bool RecordingEnabled        { get; set; } = false;
    public string RecordingResolution   { get; set; } = "1080p";
    public string RecordingHotkey       { get; set; } = "F9";
    public string RecordingEncoder      { get; set; } = "auto";
    public string RecordingMode         { get; set; } = "screen_only";
    public string FacecamPosition       { get; set; } = "top_right";
    public string FacecamDevice         { get; set; } = "";
    public string MicrophoneDevice      { get; set; } = "";

    public string DiscordWebhookUrl      { get; set; } = string.Empty;
    public List<string> DiscordQuickMessages { get; set; } =
    [
        "Bora jogar!",
        "Já volto, 5 min",
        "GG!",
        "Tô online no GLauncher",
        "Quem tá aí?",
        "Vou sair, até mais!"
    ];

    public string AccentColor          { get; set; } = "#7C4DFF";
    public string SecondaryAccentColor { get; set; } = "#00E676";
    public string BackgroundColor      { get; set; } = "#0D0D0D";
    public string HeaderColor          { get; set; } = "#16213E";
    public string CardColor            { get; set; } = "#1A1A2E";
    public string CardImageColor       { get; set; } = "#0F0F23";
}
