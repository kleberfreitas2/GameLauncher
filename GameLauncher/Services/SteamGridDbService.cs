using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameLauncher.Services;

public record SteamGridGame(int Id, string Name);
public record SteamGridImage(string Url);

public class SteamGridDbService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public SteamGridDbService(string apiKey)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<List<SteamGridGame>> SearchGamesAsync(string term)
    {
        try
        {
            var json = await _http.GetStringAsync(
                $"https://www.steamgriddb.com/api/v2/search/autocomplete/{Uri.EscapeDataString(term)}");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.GetProperty("success").GetBoolean()) return [];
            var list = new List<SteamGridGame>();
            foreach (var item in root.GetProperty("data").EnumerateArray())
                list.Add(new SteamGridGame(
                    item.GetProperty("id").GetInt32(),
                    item.GetProperty("name").GetString()!));
            return list;
        }
        catch { return []; }
    }

    public async Task<List<SteamGridImage>> GetCoversAsync(int gameId)
    {
        try
        {
            var json = await _http.GetStringAsync(
                $"https://www.steamgriddb.com/api/v2/grids/game/{gameId}?dimensions=600x900");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.GetProperty("success").GetBoolean()) return [];
            var list = new List<SteamGridImage>();
            foreach (var item in root.GetProperty("data").EnumerateArray())
                list.Add(new SteamGridImage(item.GetProperty("url").GetString()!));
            return list;
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
