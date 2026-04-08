using System;
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
            if (!File.Exists(SettingsPath)) return;
            var json = File.ReadAllText(SettingsPath);
            Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch { Current = new AppSettings(); }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

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
    }

    private static void SetBrush(string key, string hex)
    {
        if (ColorConverter.ConvertFromString(hex) is Color c)
            Application.Current.Resources[key] = new SolidColorBrush(c);
    }
}
