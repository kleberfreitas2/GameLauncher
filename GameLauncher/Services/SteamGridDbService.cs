using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using craftersmine.SteamGridDBNet;

namespace GameLauncher.Services;

public record SteamGridGame(int Id, string Name);
public record SteamGridImage(string Url, string? ThumbnailUrl);

public class SteamGridDbService
{
    private readonly SteamGridDb _client;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public SteamGridDbService(string apiKey)
    {
        _client = new SteamGridDb(apiKey);
    }

    public async Task<List<SteamGridGame>> SearchGamesAsync(string term)
    {
        try
        {
            var games = await _client.SearchForGamesAsync(term);
            return games?.Select(g => new SteamGridGame(g.Id, g.Name)).ToList() ?? [];
        }
        catch { return []; }
    }

    public async Task<List<SteamGridImage>> GetCoversAsync(int gameId)
    {
        try
        {
            var grids = await _client.GetGridsByGameIdAsync(gameId,
                dimensions: SteamGridDbDimensions.W600H900);
            return grids?.Select(g => new SteamGridImage(
                g.FullImageUrl, g.ThumbnailImageUrl)).ToList() ?? [];
        }
        catch { return []; }
    }

    public async Task<string?> DownloadCoverAsync(string url, string gameName)
    {
        try
        {
            var bytes = await _http.GetByteArrayAsync(url);
            var ext   = Path.GetExtension(url.Split('?')[0]);
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            var dir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                           "GameLauncher", "covers");
            Directory.CreateDirectory(dir);
            var safe = string.Concat(gameName.Split(Path.GetInvalidFileNameChars()));
            var path = Path.Combine(dir, $"{safe}_{DateTime.Now.Ticks}{ext}");
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch { return null; }
    }
}
