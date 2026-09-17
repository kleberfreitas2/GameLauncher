using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Globalization;
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

public record IgdbNamed(
    [property: JsonPropertyName("name")] string Name);

public record IgdbCompanyLink(
    [property: JsonPropertyName("company")] IgdbNamed? Company,
    [property: JsonPropertyName("developer")] bool Developer,
    [property: JsonPropertyName("publisher")] bool Publisher);

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
    [property: JsonPropertyName("cover")]              IgdbCover?     Cover,
    [property: JsonPropertyName("platforms")]          List<IgdbNamed>? Platforms,
    [property: JsonPropertyName("game_modes")]         List<IgdbNamed>? GameModes,
    [property: JsonPropertyName("player_perspectives")] List<IgdbNamed>? PlayerPerspectives,
    [property: JsonPropertyName("themes")]             List<IgdbNamed>? Themes,
    [property: JsonPropertyName("involved_companies")] List<IgdbCompanyLink>? InvolvedCompanies,
    [property: JsonPropertyName("franchises")]         List<IgdbNamed>? Franchises,
    [property: JsonPropertyName("game_engines")]       List<IgdbNamed>? GameEngines)
{
    public static IgdbGame SelectBestMatch(string gameName, IReadOnlyList<IgdbGame> results)
    {
        if (results.Count == 0)
            throw new ArgumentException("Nenhum resultado IGDB foi encontrado.", nameof(results));

        var target = NormalizeName(gameName);

        return results
            .OrderByDescending(result =>
            {
                var candidate = NormalizeName(result.Name);
                var score = candidate == target ? 1000 : 0;

                if (candidate.Contains(target, StringComparison.Ordinal))
                    score += 500;

                var targetTokens = TokenizeName(gameName);
                var candidateTokens = TokenizeName(result.Name);
                var commonTokens = targetTokens.Count(token => candidateTokens.Contains(token));
                score += commonTokens * 100;
                score -= (targetTokens.Count - commonTokens) * 150;

                if (result.PlatformNames.Contains("Windows", StringComparison.OrdinalIgnoreCase) ||
                    result.PlatformNames.Contains("PC", StringComparison.OrdinalIgnoreCase))
                    score += 300;

                if (result.PlatformNames.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                    result.PlatformNames.Contains("iOS", StringComparison.OrdinalIgnoreCase))
                    score -= 300;

                return score;
            })
            .First();
    }

    private static string NormalizeName(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        return new string(normalized
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToLowerInvariant();
    }

    private static List<string> TokenizeName(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        return normalized
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            .Aggregate(new List<string>(), (tokens, character) =>
            {
                if (char.IsLetterOrDigit(character))
                {
                    if (tokens.Count == 0) tokens.Add(string.Empty);
                    tokens[^1] += char.ToLowerInvariant(character);
                }
                else if (tokens.Count > 0 && tokens[^1].Length > 0)
                {
                    tokens.Add(string.Empty);
                }

                return tokens;
            })
            .Where(token => token.Length > 0)
            .ToList();
    }

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

    private static string JoinNames(IEnumerable<IgdbNamed>? values) => values is not null
        ? string.Join(", ", values.Select(value => value.Name).Where(name => !string.IsNullOrWhiteSpace(name)))
        : "";

    public string PlatformNames => JoinNames(Platforms?.Where(platform =>
        !platform.Name.Contains("Android", StringComparison.OrdinalIgnoreCase) &&
        !platform.Name.Contains("iOS", StringComparison.OrdinalIgnoreCase) &&
        !platform.Name.Contains("Windows Phone", StringComparison.OrdinalIgnoreCase) &&
        !platform.Name.Contains("BlackBerry", StringComparison.OrdinalIgnoreCase)));
    public string GameModeNames => JoinNames(GameModes);
    public string PerspectiveNames => JoinNames(PlayerPerspectives);
    public string ThemeNames => JoinNames(Themes);
    public string FranchiseNames => JoinNames(Franchises);
    public string EngineNames => JoinNames(GameEngines);
    public string DeveloperNames => InvolvedCompanies is not null
        ? string.Join(", ", InvolvedCompanies.Where(item => item.Developer && item.Company is not null).Select(item => item.Company!.Name).Distinct())
        : "";
    public string PublisherNames => InvolvedCompanies is not null
        ? string.Join(", ", InvolvedCompanies.Where(item => item.Publisher && item.Company is not null).Select(item => item.Company!.Name).Distinct())
        : "";

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

        var escapedName = name.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var exact = await QueryGamesAsync(
            $"fields id,name,summary,rating,genres.name,first_release_date,cover.url,platforms.name,game_modes.name,player_perspectives.name,themes.name,involved_companies.company.name,involved_companies.developer,involved_companies.publisher,franchises.name,game_engines.name; where name = \"{escapedName}\"; limit 20;");
        if (exact.Count > 0)
            return exact;

        try
        {
            return await QueryGamesAsync(
                $"search \"{escapedName}\"; fields id,name,summary,rating,genres.name,first_release_date,cover.url,platforms.name,game_modes.name,player_perspectives.name,themes.name,involved_companies.company.name,involved_companies.developer,involved_companies.publisher,franchises.name,game_engines.name; limit 20;");
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return [];
        }
    }

    private async Task<List<IgdbGame>> QueryGamesAsync(string query)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games")
            {
                Content = new StringContent(query, Encoding.UTF8, "text/plain")
            };
            request.Headers.Add("Client-ID", _clientId);
            request.Headers.Add("Authorization", $"Bearer {_accessToken}");

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<IgdbGame>>(
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
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
