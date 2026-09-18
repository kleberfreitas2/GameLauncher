using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;

namespace GameLauncher.Services;

public sealed class UpdateService
{
    private const string Repository = "kleberfreitas2/GameLauncher";
    private readonly HttpClient _http = new();

    public UpdateService()
    {
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GLauncher", AppInfo.Version));
    }

    public async Task<string?> GetAvailableVersionAsync()
    {
        try
        {
            using var release = await LoadLatestReleaseAsync();
            var tag = GetReleaseVersion(release.RootElement);

            if (Version.TryParse(tag, out var latest) &&
                Version.TryParse(AppInfo.Version, out var current) && latest > current)
                return tag;
        }
        catch
        {
        }

        return null;
    }

    public async Task<bool> UpdateToLatestAsync(Action<string>? status = null)
    {
        try
        {
            status?.Invoke("Verificando a última versão...");
            using var release = await LoadLatestReleaseAsync();
            var tag = GetReleaseVersion(release.RootElement);

            if (Version.TryParse(tag, out var latest) &&
                Version.TryParse(AppInfo.Version, out var current) && latest <= current)
            {
                status?.Invoke("Você já está usando a versão mais recente.");
                return false;
            }

            var asset = release.RootElement.GetProperty("assets")
                .EnumerateArray()
                .Select(item => new
                {
                    Name = item.GetProperty("name").GetString() ?? string.Empty,
                    Url = item.GetProperty("browser_download_url").GetString() ?? string.Empty
                })
                .FirstOrDefault(item => item.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            if (asset is null || string.IsNullOrWhiteSpace(asset.Url))
            {
                status?.Invoke("A release mais recente não possui instalador disponível.");
                return false;
            }

            status?.Invoke("Baixando a atualização...");
            var installerPath = Path.Combine(Path.GetTempPath(), asset.Name);
            await using (var installer = File.Create(installerPath))
            await using (var download = await _http.GetStreamAsync(asset.Url))
                await download.CopyToAsync(installer);

            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });

            Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
            return true;
        }
        catch (Exception ex)
        {
            status?.Invoke($"Não foi possível atualizar: {ex.Message}");
            return false;
        }
    }

    private async Task<JsonDocument> LoadLatestReleaseAsync()
    {
        var latestUrl = $"https://api.github.com/repos/{Repository}/releases/latest";
        using var latestResponse = await _http.GetAsync(latestUrl);

        if (latestResponse.IsSuccessStatusCode)
            return JsonDocument.Parse(await latestResponse.Content.ReadAsStringAsync());

        // Compatibilidade com a release antiga publicada na tag "game".
        using var taggedResponse = await _http.GetAsync(
            $"https://api.github.com/repos/{Repository}/releases/tags/game");
        taggedResponse.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await taggedResponse.Content.ReadAsStringAsync());
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
