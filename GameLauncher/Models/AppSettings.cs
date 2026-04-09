namespace GameLauncher.Models;

public class AppSettings
{
    public string SteamGridDbApiKey    { get; set; } = "https://www.steamgriddb.com/api/v2";
    public string BackgroundImagePath  { get; set; } = string.Empty;
    public string AccentColor          { get; set; } = "#7C4DFF";
    public string SecondaryAccentColor { get; set; } = "#00E676";
    public string BackgroundColor      { get; set; } = "#0D0D0D";
    public string HeaderColor          { get; set; } = "#16213E";
    public string CardColor            { get; set; } = "#1A1A2E";
    public string CardImageColor       { get; set; } = "#0F0F23";
}
