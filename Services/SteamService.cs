using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using GameLauncher.Models;
using Microsoft.Win32;

namespace GameLauncher.Services;

public sealed partial class SteamService : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private string? _steamId;

    public bool IsConnected => _steamId is not null;
    public string? SteamId => _steamId;

    public SteamService()
    {
    }

    /// <summary>
    /// Detects the SteamID64 of the currently logged-in Steam user from the local installation.
    /// Returns null if Steam is not installed or no user is logged in.
    /// </summary>
    public static string? DetectLocalSteamId()
    {
        try
        {
            // 1. Try ActiveProcess\ActiveUser from the registry (Steam3 ID, 32-bit)
            using var activeKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
            var activeUser = activeKey?.GetValue("ActiveUser");
            if (activeUser is int uid32 && uid32 > 0)
            {
                // Convert Steam3 ID → SteamID64
                return (76561197960265728L + uid32).ToString();
            }

            // 2. Fallback: parse loginusers.vdf from the Steam installation directory
            string? steamPath = null;
            using (var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                steamPath = steamKey?.GetValue("SteamPath")?.ToString()?.Replace('/', '\\');
            }

            if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
            {
                var candidates = new[]
                {
                    @"C:\Program Files (x86)\Steam",
                    @"C:\Program Files\Steam",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Steam")
                };
                steamPath = candidates.FirstOrDefault(Directory.Exists);
            }

            if (steamPath is null) return null;

            var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
            if (!File.Exists(loginUsersPath)) return null;

            var content = File.ReadAllText(loginUsersPath);
            // Find the user with "MostRecent" "1"
            var userBlocks = LoginUserIdRegex().Matches(content);
            foreach (Match block in userBlocks)
            {
                var steamId64 = block.Groups[1].Value;
                // Check if this user block has MostRecent = 1
                var blockStart = block.Index;
                var nextBlock = content.IndexOf("\n\t\"7656", blockStart + 1);
                var blockEnd = nextBlock > 0 ? nextBlock : content.Length;
                var userSection = content[blockStart..blockEnd];

                if (userSection.Contains("\"MostRecent\"", StringComparison.OrdinalIgnoreCase)
                    && userSection.Contains("\"1\""))
                {
                    return steamId64;
                }
            }

            // If no MostRecent found, return the first user
            if (userBlocks.Count > 0)
                return userBlocks[0].Groups[1].Value;
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Connects to Steam using a SteamID64 or vanity URL.
    /// Validates via the public Steam Community profile (no API key needed).
    /// </summary>
    public async Task<bool> ConnectAsync(string steamIdOrVanity)
    {
        try
        {
            string steamId;

            // If it's a full profile URL, extract the identifier
            if (steamIdOrVanity.Contains("steamcommunity.com", StringComparison.OrdinalIgnoreCase))
            {
                var match = ProfileUrlRegex().Match(steamIdOrVanity);
                if (match.Success)
                    steamIdOrVanity = match.Groups[1].Value;
            }

            // If it looks like a 64-bit Steam ID (17 digits starting with 7656)
            if (steamIdOrVanity.Length >= 15 && steamIdOrVanity.All(char.IsDigit))
            {
                steamId = steamIdOrVanity;
            }
            else
            {
                // Try to resolve via public Steam Community XML profile
                var resolved = await ResolveVanityUrlPublicAsync(steamIdOrVanity);
                if (resolved is null) return false;
                steamId = resolved;
            }

            // Validate via public Steam Community XML profile (no API key needed)
            var xml = await FetchPublicProfileXmlAsync(steamId);
            if (xml is null) return false;

            _steamId = steamId;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Disconnect()
    {
        _steamId = null;
    }

    public async Task<SteamProfile?> GetProfileAsync()
    {
        if (!IsConnected || _steamId is null)
            return null;

        try
        {
            // Use public Steam Community XML profile (no API key needed)
            var xml = await FetchPublicProfileXmlAsync(_steamId);
            if (xml is null)
            {
                // Fallback to local data from loginusers.vdf
                return GetProfileFromLocal(_steamId);
            }

            var personaName = xml.Root?.Element("steamID")?.Value ?? "Steam User";
            var avatarFull = xml.Root?.Element("avatarFull")?.Value ?? string.Empty;
            var profileUrl = $"https://steamcommunity.com/profiles/{_steamId}";

            return new SteamProfile
            {
                SteamId = _steamId,
                PersonaName = personaName,
                AvatarUrl = avatarFull,
                ProfileUrl = profileUrl
            };
        }
        catch
        {
            return GetProfileFromLocal(_steamId);
        }
    }

    /// <summary>
    /// Gets the count of installed Steam games from local .acf manifests.
    /// </summary>
    public int GetInstalledGamesCount()
    {
        try
        {
            var count = 0;
            var libraryFolders = GetSteamLibraryFolders();
            foreach (var libPath in libraryFolders)
            {
                var steamApps = Path.Combine(libPath, "steamapps");
                if (!Directory.Exists(steamApps)) continue;
                count += Directory.GetFiles(steamApps, "appmanifest_*.acf").Length;
            }
            return count;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Scans local Steam installation folders for installed games.
    /// </summary>
    public List<Game> ScanSteamInstalledGames()
    {
        var games = new List<Game>();
        var foundPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var libraryFolders = GetSteamLibraryFolders();

        foreach (var libPath in libraryFolders)
        {
            var steamApps = Path.Combine(libPath, "steamapps");
            if (!Directory.Exists(steamApps)) continue;

            var commonDir = Path.Combine(steamApps, "common");
            if (!Directory.Exists(commonDir)) continue;

            // Read .acf manifests to get accurate game names
            var manifests = ParseAcfManifests(steamApps);

            foreach (var gameDir in Directory.GetDirectories(commonDir))
            {
                try
                {
                    var exeFiles = Directory.GetFiles(gameDir, "*.exe", SearchOption.AllDirectories);

                    var mainExe = exeFiles
                        .Select(f => new FileInfo(f))
                        .Where(f => f.Length > 100_000)
                        .Where(f => !IsExcludedExe(f.Name))
                        .OrderByDescending(f => f.Length)
                        .FirstOrDefault();

                    if (mainExe is null || foundPaths.Contains(mainExe.FullName))
                        continue;

                    foundPaths.Add(mainExe.FullName);

                    var folderName = Path.GetFileName(gameDir);
                    var gameName = manifests.GetValueOrDefault(folderName, folderName);

                    games.Add(new Game
                    {
                        Name = gameName,
                        ExecutablePath = mainExe.FullName,
                        InstallDirectory = gameDir,
                        IconPath = IconExtractor.ExtractIcon(mainExe.FullName)
                    });
                }
                catch { }
            }
        }

        return games;
    }

    /// <summary>
    /// Resolves a vanity URL to a SteamID64 using the public Steam Community XML profile.
    /// </summary>
    private async Task<string?> ResolveVanityUrlPublicAsync(string vanityName)
    {
        try
        {
            var url = $"https://steamcommunity.com/id/{Uri.EscapeDataString(vanityName)}/?xml=1";
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(content);
            var steamId64 = doc.Root?.Element("steamID64")?.Value;

            return !string.IsNullOrEmpty(steamId64) ? steamId64 : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Fetches the public Steam Community XML profile for a SteamID64.
    /// </summary>
    private async Task<XDocument?> FetchPublicProfileXmlAsync(string steamId64)
    {
        try
        {
            var url = $"https://steamcommunity.com/profiles/{steamId64}/?xml=1";
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(content);

            // Check if the profile exists (has a steamID element)
            if (doc.Root?.Element("steamID") is null)
                return null;

            return doc;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets profile info from local loginusers.vdf when the public profile is unavailable.
    /// </summary>
    private static SteamProfile? GetProfileFromLocal(string steamId)
    {
        try
        {
            string? steamPath = null;
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                steamPath = key?.GetValue("SteamPath")?.ToString()?.Replace('/', '\\');
            }

            if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
                return null;

            var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
            if (!File.Exists(loginUsersPath))
                return null;

            var content = File.ReadAllText(loginUsersPath);

            // Find the block for this Steam ID and extract PersonaName
            var idIndex = content.IndexOf($"\"{steamId}\"", StringComparison.Ordinal);
            if (idIndex < 0) return null;

            var blockEnd = content.IndexOf("\n\t\"7656", idIndex + 1);
            if (blockEnd < 0) blockEnd = content.Length;
            var block = content[idIndex..blockEnd];

            var personaMatch = Regex.Match(block, @"""PersonaName""\s+""([^""]+)""");
            var personaName = personaMatch.Success ? personaMatch.Groups[1].Value : "Steam User";

            return new SteamProfile
            {
                SteamId = steamId,
                PersonaName = personaName,
                AvatarUrl = string.Empty,
                ProfileUrl = $"https://steamcommunity.com/profiles/{steamId}"
            };
        }
        catch
        {
            return null;
        }
    }

    private static List<string> GetSteamLibraryFolders()
    {
        var folders = new List<string>();

        // Find main Steam path from registry
        string? steamPath = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            steamPath = key?.GetValue("SteamPath")?.ToString()?.Replace('/', '\\');
        }
        catch { }

        // Common fallback paths
        if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
        {
            var candidates = new[]
            {
                @"C:\Program Files (x86)\Steam",
                @"C:\Program Files\Steam",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Steam")
            };
            steamPath = candidates.FirstOrDefault(Directory.Exists);
        }

        if (steamPath is null) return folders;

        folders.Add(steamPath);

        // Parse libraryfolders.vdf for additional library paths
        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdfPath))
        {
            try
            {
                var content = File.ReadAllText(vdfPath);
                var matches = VdfPathRegex().Matches(content);
                foreach (Match m in matches)
                {
                    var path = m.Groups[1].Value.Replace(@"\\", @"\");
                    if (Directory.Exists(path) && !folders.Contains(path, StringComparer.OrdinalIgnoreCase))
                        folders.Add(path);
                }
            }
            catch { }
        }

        return folders;
    }

    private static Dictionary<string, string> ParseAcfManifests(string steamAppsDir)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var acfFile in Directory.GetFiles(steamAppsDir, "*.acf"))
            {
                try
                {
                    var content = File.ReadAllText(acfFile);
                    var nameMatch = AcfNameRegex().Match(content);
                    var dirMatch = AcfInstallDirRegex().Match(content);

                    if (nameMatch.Success && dirMatch.Success)
                    {
                        map[dirMatch.Groups[1].Value] = nameMatch.Groups[1].Value;
                    }
                }
                catch { }
            }
        }
        catch { }

        return map;
    }

    private static readonly HashSet<string> ExcludedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "uninstall", "unins", "crash", "report", "updater", "patcher",
        "config", "settings", "setup", "installer", "redist",
        "easyanticheat", "battleye", "vc_redist", "dxsetup",
        "steamwebhelper", "steamerrorreporter", "gameoverlayui"
    };

    private static bool IsExcludedExe(string fileName)
    {
        return ExcludedKeywords.Any(k =>
            fileName.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    // ── Regex ────────────────────────────────────────────────────

    [GeneratedRegex(@"steamcommunity\.com/(?:id|profiles)/([^/\s]+)")]
    private static partial Regex ProfileUrlRegex();

    [GeneratedRegex(@"""path""\s+""([^""]+)""")]
    private static partial Regex VdfPathRegex();

    [GeneratedRegex(@"""name""\s+""([^""]+)""")]
    private static partial Regex AcfNameRegex();

    [GeneratedRegex(@"""installdir""\s+""([^""]+)""")]
    private static partial Regex AcfInstallDirRegex();

    [GeneratedRegex(@"""(7656\d{13})""")]
    private static partial Regex LoginUserIdRegex();

    }
