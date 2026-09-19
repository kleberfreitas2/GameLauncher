using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameLauncher.Services;

public sealed class UpdateService
{
    private const string Repository = "kleberfreitas2/GameLauncher";
    private const string UpdateBranch = "Feature-GameLauncher";
    private readonly HttpClient _http = new();

    public string? LastError { get; private set; }

    public UpdateService()
    {
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GLauncher", AppInfo.Version));
    }

    public async Task<string?> GetAvailableVersionAsync()
    {
        LastError = null;
        try
        {
            using var release = await LoadLatestReleaseAsync();
            var tag = GetReleaseVersion(release.RootElement);

            if (Version.TryParse(tag, out var latest) &&
                Version.TryParse(AppInfo.Version, out var current) && latest > current)
                return tag;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }

        return null;
    }

    private async Task<JsonDocument> LoadLatestReleaseAsync()
    {
        // Os instaladores oficiais ficam versionados diretamente nesta pasta.
        try
        {
            return await LoadReleaseFromOutputFolderAsync();
        }
        catch
        {
            // Mant?m a API de releases como fallback.
        }

        using var response = await _http.GetAsync(
            $"https://api.github.com/repos/{Repository}/releases?per_page=100");
        if (!response.IsSuccessStatusCode)
            return await LoadReleaseFromOutputFolderAsync();

        using var releases = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var newest = releases.RootElement
            .EnumerateArray()
            .Select(release => new
            {
                Release = release,
                Version = GetReleaseVersion(release)
            })
            .Where(item => Version.TryParse(item.Version, out _))
            .OrderByDescending(item => Version.Parse(item.Version!))
            .FirstOrDefault();

        if (newest is null)
            return await LoadReleaseFromOutputFolderAsync();

        // Cria um documento independente antes de liberar a resposta HTTP.
        return JsonDocument.Parse(newest.Release.GetRawText());
    }

    private async Task<JsonDocument> LoadReleaseFromOutputFolderAsync()
    {
        var pageUrl = $"https://github.com/{Repository}/tree/{UpdateBranch}/Installer/Output";
        var html = await _http.GetStringAsync(pageUrl);
        var names = Regex.Matches(
                html,
                @"GLauncher_Setup_v(\d+\.\d+\.\d+)\.exe",
                RegexOptions.IgnoreCase)
            .Select(match => new
            {
                Version = Version.Parse(match.Groups[1].Value),
                Name = $"GLauncher_Setup_v{match.Groups[1].Value}.exe"
            })
            .OrderByDescending(item => item.Version)
            .ToList();

        var newest = names.FirstOrDefault()
            ?? throw new InvalidOperationException("Nenhum instalador versionado foi encontrado na pasta Output.");

        var downloadUrl =
            $"https://raw.githubusercontent.com/{Repository}/{UpdateBranch}/Installer/Output/{Uri.EscapeDataString(newest.Name)}";

        var release = new
        {
            tag_name = UpdateBranch,
            assets = new[]
            {
                new
                {
                    name = newest.Name,
                    browser_download_url = downloadUrl
                }
            }
        };

        return JsonDocument.Parse(JsonSerializer.Serialize(release));
    }

    private static string? GetReleaseVersion(JsonElement release)
    {
        var tag = release.GetProperty("tag_name").GetString()?.TrimStart('v');
        if (Version.TryParse(tag, out _))
            return tag;

        foreach (var asset in release.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? string.Empty;
            var match = Regex.Match(name, @"(?<!\d)(\d+\.\d+\.\d+)(?!\d)");
            if (match.Success)
                return match.Groups[1].Value;
        }

        return null;
    }

    public void Dispose() => _http.Dispose();
}
