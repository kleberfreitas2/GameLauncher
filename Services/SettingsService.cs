using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using GameLauncher.Models;
using MaterialDesignThemes.Wpf;

namespace GameLauncher.Services;

public static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GameLauncher", "settings.json");

    public static AppSettings Current { get; private set; } = new();

    public static void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                Current.GroqApiKey = string.Empty;
                return;
            }
            var json = File.ReadAllText(SettingsPath);
            Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            if (Current.SteamGridDbApiKey.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                Current.SteamGridDbApiKey = AppSettings.DefaultSteamGridDbApiKey;

            // Descriptografa campos sensíveis salvos com DPAPI
            Current.OpenAiApiKey      = SecretsService.Unprotect(Current.OpenAiApiKey);
            Current.GroqApiKey        = SecretsService.Unprotect(Current.GroqApiKey);

            // A chave Groq embutida é apenas um valor legado/ofuscado.
            // Nunca a use como credencial padrão: solicite uma chave configurada pelo usuário.
            if (SecretsService.IsLegacyGroqApiKey(Current.GroqApiKey))
                Current.GroqApiKey = string.Empty;
        }
        catch { Current = new AppSettings(); }

        // A chave Groq embutida não deve ser usada automaticamente, inclusive
        // quando não existe um arquivo de configurações anterior.
        if (SecretsService.IsLegacyGroqApiKey(Current.GroqApiKey))
            Current.GroqApiKey = string.Empty;

        if (IsInvalidKey(Current.SteamGridDbApiKey))
            Current.SteamGridDbApiKey = AppSettings.DefaultSteamGridDbApiKey;
        if (IsInvalidKey(Current.IgdbClientId))
            Current.IgdbClientId = AppSettings.DefaultIgdbClientId;
        if (IsInvalidKey(Current.IgdbClientSecret))
            Current.IgdbClientSecret = AppSettings.DefaultIgdbClientSecret;
        if (IsInvalidKey(Current.XboxClientId))
            Current.XboxClientId = AppSettings.DefaultXboxClientId;
        if (IsInvalidKey(Current.SteamApiKey))
            Current.SteamApiKey = AppSettings.DefaultSteamApiKey;
    }

    private static bool IsInvalidKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Só criptografa com DPAPI se a chave for diferente do padrão embutido.
    /// Chaves padrão ficam em texto plano para funcionar em qualquer máquina.
    /// </summary>
    private static string ProtectIfCustom(string value, string defaultValue) =>
        string.IsNullOrEmpty(value) || value == defaultValue
            ? value
            : SecretsService.Protect(value);

    public static void Save()
    {
        try
        {
            // Cria uma cópia com campos sensíveis protegidos por DPAPI antes de salvar
            var toSave = new AppSettings
            {
                SteamGridDbApiKey    = Current.SteamGridDbApiKey,
                IgdbClientId         = Current.IgdbClientId,
                IgdbClientSecret     = Current.IgdbClientSecret,
                BackgroundImagePath  = Current.BackgroundImagePath,
                AvatarImagePath      = Current.AvatarImagePath,
                PlayerName           = Current.PlayerName,
                XboxClientId         = Current.XboxClientId,
                SteamApiKey          = Current.SteamApiKey,
                SteamId              = Current.SteamId,
                SoundEnabled         = Current.SoundEnabled,
                FpsOverlayEnabled    = Current.FpsOverlayEnabled,
                AccentColor          = Current.AccentColor,
                SecondaryAccentColor = Current.SecondaryAccentColor,
                BackgroundColor      = Current.BackgroundColor,
                HeaderColor          = Current.HeaderColor,
                CardColor            = Current.CardColor,
                CardImageColor       = Current.CardImageColor,

                // Campos sensíveis — protege com DPAPI apenas se o usuário inseriu
                // uma chave customizada (diferente do padrão embutido no app).
                // As chaves padrão ficam em texto plano para funcionar em qualquer máquina.
                OpenAiApiKey         = ProtectIfCustom(Current.OpenAiApiKey,      SecretsService.OpenAiApiKey),
                GroqApiKey           = ProtectIfCustom(Current.GroqApiKey,        SecretsService.GroqApiKey),
            };

            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static event Action? ThemeApplied;

    public static void ApplyTheme()
    {
        SetBrush("AppBackgroundBrush",    Current.BackgroundColor);
        SetBrush("HeaderBackgroundBrush", Current.HeaderColor);
        SetBrush("CardBackgroundBrush",   Current.CardColor);
        SetBrush("CardImageBackgroundBrush", Current.CardImageColor);
        SetBrush("AccentBrush",           Current.AccentColor);
        SetBrush("AccentGreenBrush",      Current.SecondaryAccentColor);

        try
        {
            var palette = new PaletteHelper();
            var theme   = palette.GetTheme();
            if (ColorConverter.ConvertFromString(Current.AccentColor) is Color accent)
                theme.SetPrimaryColor(accent);
            if (ColorConverter.ConvertFromString(Current.SecondaryAccentColor) is Color secondary)
                theme.SetSecondaryColor(secondary);
            palette.SetTheme(theme);
        }
        catch { }

        ThemeApplied?.Invoke();
    }

    private static void SetBrush(string key, string hex)
    {
        if (ColorConverter.ConvertFromString(hex) is Color c)
            Application.Current.Resources[key] = new SolidColorBrush(c);
    }
}

