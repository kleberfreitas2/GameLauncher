using System.IO;
using System.Net.Http;
using craftersmine.SteamGridDBNet;

namespace GameLauncher.Services;

public record SteamGridGame(int Id, string Name);
public record SteamGridImage(string Url, string? ThumbnailUrl);

public class SteamGridDbService
{
    private readonly SteamGridDb _client;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public string? LastError { get; private set; }

    public SteamGridDbService(string apiKey)
    {
        _client = new SteamGridDb(apiKey);
    }

    public async Task<List<SteamGridGame>> SearchGamesAsync(string term)
    {
        LastError = null;
        try
        {
            var games = await _client.SearchForGamesAsync(term);
            return games?.Select(g => new SteamGridGame(g.Id, g.Name)).ToList() ?? [];
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return [];
        }
    }

    public async Task<List<SteamGridImage>> GetCoversAsync(int gameId)
    {
        LastError = null;
        try
        {
            var grids = await _client.GetGridsByGameIdAsync(gameId,
                dimensions: SteamGridDbDimensions.W600H900);
            return grids?.Select(g => new SteamGridImage(
                g.FullImageUrl, g.ThumbnailImageUrl)).ToList() ?? [];
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return [];
        }
    }

    public async Task<List<SteamGridImage>> GetIconsAsync(int gameId)
    {
        LastError = null;
        try
        {
            var icons = await _client.GetIconsByGameIdAsync(gameId);
            return icons?.Select(i => new SteamGridImage(
                i.FullImageUrl, i.ThumbnailImageUrl)).ToList() ?? [];
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return [];
        }
    }

    public async Task<List<SteamGridImage>> GetLogosAsync(int gameId)
    {
        LastError = null;
        try
        {
            var logos = await _client.GetLogosByGameIdAsync(gameId);
            return logos?.Select(l => new SteamGridImage(
                l.FullImageUrl, l.ThumbnailImageUrl)).ToList() ?? [];
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return [];
        }
    }

    public async Task<List<SteamGridImage>> GetHeroesAsync(int gameId)
    {
        LastError = null;
        try
        {
            var heroes = await _client.GetHeroesByGameIdAsync(gameId);
            return heroes?.Select(h => new SteamGridImage(
                h.FullImageUrl, h.ThumbnailImageUrl)).ToList() ?? [];
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return [];
        }
    }

    public async Task<List<SteamGridImage>> GetHeroesAsync(int gameId,
        SteamGridDbStyles styles = SteamGridDbStyles.AllHeroes,
        SteamGridDbDimensions dimensions = SteamGridDbDimensions.AllHeroes,
        SteamGridDbTypes types = SteamGridDbTypes.All,
        SteamGridDbFormats formats = SteamGridDbFormats.All,
        bool nsfw = false, bool humorous = false, bool epilepsy = false)
    {
        LastError = null;
        try
        {
            var heroes = await _client.GetHeroesByGameIdAsync(gameId,
                styles: styles, dimensions: dimensions,
                types: types, formats: formats,
                nsfw: nsfw, humorous: humorous, epilepsy: epilepsy);
            return heroes?.Select(h => new SteamGridImage(
                h.FullImageUrl, h.ThumbnailImageUrl)).ToList() ?? [];
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return [];
        }
    }

    public async Task<List<SteamGridImage>> GetGridsWideAsync(int gameId,
        SteamGridDbStyles styles = SteamGridDbStyles.AllGrids,
        SteamGridDbDimensions dimensions = SteamGridDbDimensions.AllGrids,
        SteamGridDbTypes types = SteamGridDbTypes.All,
        SteamGridDbFormats formats = SteamGridDbFormats.All,
        bool nsfw = false, bool humorous = false, bool epilepsy = false)
    {
        LastError = null;
        try
        {
            var grids = await _client.GetGridsByGameIdAsync(gameId,
                styles: styles, dimensions: dimensions,
                types: types, formats: formats,
                nsfw: nsfw, humorous: humorous, epilepsy: epilepsy);
            return grids?.Select(g => new SteamGridImage(
                g.FullImageUrl, g.ThumbnailImageUrl)).ToList() ?? [];
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return [];
        }
    }

    public async Task<string?> DownloadCoverAsync(string url, string gameName)
    {
        return await DownloadImageAsync(url, gameName, "covers");
    }

    public async Task<string?> DownloadIconAsync(string url, string gameName)
    {
        return await DownloadImageAsync(url, gameName, "icons");
    }

    public async Task<string?> DownloadLogoAsync(string url, string gameName)
    {
        return await DownloadImageAsync(url, gameName, "logos");
    }

    public async Task<string?> DownloadHeroAsync(string url, string gameName)
    {
        return await DownloadImageAsync(url, gameName, "heroes");
    }

    private async Task<string?> DownloadImageAsync(string url, string gameName, string subfolder)
    {
        LastError = null;
        try
        {
            var bytes = await _http.GetByteArrayAsync(url);
            var ext   = Path.GetExtension(url.Split('?')[0]);
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            var dir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                           "GameLauncher", subfolder);
            Directory.CreateDirectory(dir);
            var safe = string.Concat(gameName.Split(Path.GetInvalidFileNameChars()));
            var path = Path.Combine(dir, $"{safe}_{DateTime.Now.Ticks}{ext}");
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }
}
