using GameLauncher.Services;

namespace GameLauncher.Models;

public class AppSettings
{
    // Chaves padrão decodificadas em tempo de execução pelo SecretsService (XOR).
    // Nenhuma chave fica em texto plano no código-fonte ou no binário.
    internal static string DefaultSteamGridDbApiKey  => SecretsService.SteamGridDbApiKey;
    internal static string DefaultIgdbClientId       => SecretsService.IgdbClientId;
    internal static string DefaultIgdbClientSecret   => SecretsService.IgdbClientSecret;
    internal static string DefaultXboxClientId       => SecretsService.XboxClientId;
    internal static string DefaultSteamApiKey        => SecretsService.SteamApiKey;
    internal static string DefaultOpenAiApiKey       => SecretsService.OpenAiApiKey;
    internal static string DefaultGroqApiKey         => SecretsService.GroqApiKey;

    public string SteamGridDbApiKey    { get; set; } = SecretsService.SteamGridDbApiKey;
    public string IgdbClientId         { get; set; } = SecretsService.IgdbClientId;
    public string IgdbClientSecret     { get; set; } = SecretsService.IgdbClientSecret;
    public string BackgroundImagePath  { get; set; } = string.Empty;
    public string AvatarImagePath      { get; set; } = string.Empty;
    public string PlayerName           { get; set; } = "Jogador";
    public string XboxClientId         { get; set; } = SecretsService.XboxClientId;
    public string SteamApiKey          { get; set; } = SecretsService.SteamApiKey;
    public string SteamId              { get; set; } = string.Empty;

    public bool SoundEnabled           { get; set; } = true;
    public bool FpsOverlayEnabled      { get; set; } = false;
    public string OpenAiApiKey         { get; set; } = SecretsService.OpenAiApiKey;
    public string GroqApiKey           { get; set; } = SecretsService.GroqApiKey;

    public string AccentColor          { get; set; } = "#7C4DFF";
    public string SecondaryAccentColor { get; set; } = "#00E676";
    public string BackgroundColor      { get; set; } = "#0D0D0D";
    public string HeaderColor          { get; set; } = "#16213E";
    public string CardColor            { get; set; } = "#1A1A2E";
    public string CardImageColor       { get; set; } = "#0F0F23";

    /// <summary>Quantas vezes o callout "experimente a IA" já foi exibido.</summary>
    public int AiNotificationCount     { get; set; } = 0;
}

