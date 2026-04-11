using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameLauncher.Services;

public record IgdbCover(
    [property: JsonPropertyName("id")]  int    Id,
    [property: JsonPropertyName("url")] string Url);

public record IgdbGenre(
    [property: JsonPropertyName("id")]   int    Id,
    [property: JsonPropertyName("name")] string Name);

public record IgdbScreenshot(
    [property: JsonPropertyName("id")]  int    Id,
    [property: JsonPropertyName("url")] string Url)
{
    public string FullUrl => Url is { } u
        ? "https:" + u.Replace("t_thumb", "t_1080p")
        : string.Empty;

    public string ThumbUrl => Url is { } u
        ? "https:" + u.Replace("t_thumb", "t_screenshot_med")
        : string.Empty;
}

public record IgdbArtwork(
    [property: JsonPropertyName("id")]  int    Id,
    [property: JsonPropertyName("url")] string Url)
{
    public string FullUrl => Url is { } u
        ? "https:" + u.Replace("t_thumb", "t_1080p")
        : string.Empty;

    public string ThumbUrl => Url is { } u
        ? "https:" + u.Replace("t_thumb", "t_screenshot_med")
        : string.Empty;
}

public record IgdbGame(
    [property: JsonPropertyName("id")]                 int            Id,
    [property: JsonPropertyName("name")]               string         Name,
    [property: JsonPropertyName("summary")]            string?        Summary,
    [property: JsonPropertyName("rating")]             double?        Rating,
    [property: JsonPropertyName("genres")]             List<IgdbGenre>? Genres,
    [property: JsonPropertyName("first_release_date")] long?          FirstReleaseDate,
    [property: JsonPropertyName("cover")]              IgdbCover?     Cover)
{
    public int? ReleaseYear => FirstReleaseDate.HasValue
        ? DateTimeOffset.FromUnixTimeSeconds(FirstReleaseDate.Value).Year
        : (int?)null;

    public string CoverUrl => Cover?.Url is { } url
        ? "https:" + url.Replace("t_thumb", "t_cover_big")
        : string.Empty;

    public string ThumbnailUrl => Cover?.Url is { } url
        ? "https:" + url
        : string.Empty;

    public string GenreNames => Genres is { Count: > 0 }
        ? string.Join(", ", Genres.Select(g => g.Name))
        : "—";

    public string RatingText => Rating.HasValue ? $"{Rating.Value:F0} / 100" : "—";
    public string YearText   => ReleaseYear?.ToString() ?? "—";
}

public class IgdbService : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly string _clientId;
    private readonly string _clientSecret;

    private string?  _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public string? LastError { get; private set; }

    public IgdbService(string clientId, string clientSecret)
    {
        _clientId     = clientId;
        _clientSecret = clientSecret;
    }

    private async Task<bool> EnsureTokenAsync()
    {
        if (_accessToken is not null && DateTime.UtcNow < _tokenExpiry)
            return true;

        try
        {
            var response = await _http.PostAsync(
                $"https://id.twitch.tv/oauth2/token?client_id={_clientId}&client_secret={_clientSecret}&grant_type=client_credentials",
                null);

            if (!response.IsSuccessStatusCode)
            {
                LastError = $"Falha na autenticação Twitch: {(int)response.StatusCode} {response.ReasonPhrase}";
                return false;
            }

            var json       = await response.Content.ReadFromJsonAsync<JsonElement>();
            _accessToken   = json.GetProperty("access_token").GetString();
            var expiresIn  = json.GetProperty("expires_in").GetInt32();
            _tokenExpiry   = DateTime.UtcNow.AddSeconds(expiresIn - 60);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    public async Task<List<IgdbGame>> SearchGamesAsync(string name)
    {
        LastError = null;
        if (!await EnsureTokenAsync()) return [];

        try
        {
            var query = $"search \"{name}\"; fields id,name,summary,rating,genres.name,first_release_date,cover.url; limit 12;";

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games")
            {
                Content = new StringContent(query, Encoding.UTF8, "text/plain")
            };
            request.Headers.Add("Client-ID",     _clientId);
            request.Headers.Add("Authorization", $"Bearer {_accessToken}");

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var games = await response.Content.ReadFromJsonAsync<List<IgdbGame>>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return games ?? [];
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return [];
        }
    }

    public async Task<string?> DownloadCoverAsync(string url, string gameName)
    {
        LastError = null;
        try
        {
            var bytes = await _http.GetByteArrayAsync(url);
            var ext   = Path.GetExtension(url.Split('?')[0]);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";

            var dir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                           "GameLauncher", "covers");
            Directory.CreateDirectory(dir);

            var safe = string.Concat(gameName.Split(Path.GetInvalidFileNameChars()));
            var path = Path.Combine(dir, $"{safe}_igdb_{DateTime.Now.Ticks}{ext}");
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    public async Task<List<IgdbScreenshot>> GetScreenshotsAsync(int gameId)
    {
        LastError = null;
        if (!await EnsureTokenAsync()) return [];

        try
        {
            var query = $"fields url; where game = {gameId}; limit 20;";
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/screenshots")
            {
                Content = new StringContent(query, Encoding.UTF8, "text/plain")
            };
            request.Headers.Add("Client-ID",     _clientId);
            request.Headers.Add("Authorization", $"Bearer {_accessToken}");

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var items = await response.Content.ReadFromJsonAsync<List<IgdbScreenshot>>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return items ?? [];
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return [];
        }
    }

    public async Task<List<IgdbArtwork>> GetArtworksAsync(int gameId)
    {
        LastError = null;
        if (!await EnsureTokenAsync()) return [];

        try
        {
            var query = $"fields url; where game = {gameId}; limit 20;";
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/artworks")
            {
                Content = new StringContent(query, Encoding.UTF8, "text/plain")
            };
            request.Headers.Add("Client-ID",     _clientId);
            request.Headers.Add("Authorization", $"Bearer {_accessToken}");

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var items = await response.Content.ReadFromJsonAsync<List<IgdbArtwork>>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return items ?? [];
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return [];
        }
    }

    public async Task<string?> DownloadBackgroundAsync(string url, string gameName)
    {
        LastError = null;
        try
        {
            var bytes = await _http.GetByteArrayAsync(url);
            var ext   = Path.GetExtension(url.Split('?')[0]);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";

            var dir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                           "GameLauncher", "backgrounds");
            Directory.CreateDirectory(dir);

            var safe = string.Concat(gameName.Split(Path.GetInvalidFileNameChars()));
            var path = Path.Combine(dir, $"{safe}_bg_{DateTime.Now.Ticks}{ext}");
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
