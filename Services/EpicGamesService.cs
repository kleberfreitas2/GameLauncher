using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GameLauncher.Models;
using Microsoft.Win32;

namespace GameLauncher.Services;

public sealed class EpicGamesService : IDisposable
{
    private string? _installPath;
    private string? _displayName;

    public bool IsConnected => _installPath is not null;
    public string? DisplayName => _displayName;

    public static string? DetectEpicInstallPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Epic Games\EOS");
            var path = key?.GetValue("ModSdkMetadataDir")?.ToString();
            if (!string.IsNullOrEmpty(path))
            {
                var root = Path.GetDirectoryName(Path.GetDirectoryName(path));
                if (root is not null && Directory.Exists(root))
                    return root;
            }
        }
        catch { }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Epic Games\EpicGamesLauncher");
            var path = key?.GetValue("AppDataPath")?.ToString();
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                return path;
        }
        catch { }

        var candidates = new[]
        {
            @"C:\Program Files\Epic Games",
            @"C:\Program Files (x86)\Epic Games",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Epic Games"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpicGamesLauncher")
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    public static string? DetectDisplayName()
    {
        // Primary: scan Unreal Engine game logs for the DisplayName pattern
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var regex = new Regex(@"DisplayName=\[([^\]]+)\]", RegexOptions.Compiled);
            string? bestName = null;
            DateTime bestTime = DateTime.MinValue;

            foreach (var dir in Directory.GetDirectories(localAppData))
            {
                var logsDir = Path.Combine(dir, "Saved", "Logs");
                if (!Directory.Exists(logsDir)) continue;

                try
                {
                    foreach (var logFile in Directory.GetFiles(logsDir, "*.log"))
                    {
                        try
                        {
                            var fileTime = File.GetLastWriteTime(logFile);
                            using var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            using var reader = new StreamReader(stream);
                            string? line;
                            while ((line = reader.ReadLine()) is not null)
                            {
                                var match = regex.Match(line);
                                if (match.Success)
                                {
                                    var name = match.Groups[1].Value;
                                    if (!string.IsNullOrEmpty(name) && fileTime > bestTime)
                                    {
                                        bestName = name;
                                        bestTime = fileTime;
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(bestName))
                return bestName;
        }
        catch { }

        // Fallback: check GameUserSettings.ini (Config\WindowsEditor)
        try
        {
            var iniPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EpicGamesLauncher", "Saved", "Config", "WindowsEditor", "GameUserSettings.ini");

            if (File.Exists(iniPath))
            {
                foreach (var line in File.ReadAllLines(iniPath))
                {
                    if (line.StartsWith("LastLoggedInDisplayName=", StringComparison.OrdinalIgnoreCase))
                    {
                        var val = line[(line.IndexOf('=') + 1)..].Trim();
                        if (!string.IsNullOrEmpty(val))
                            return val;
                    }
                }
            }
        }
        catch { }

        // Fallback: check JSON files in Saved\Data
        try
        {
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EpicGamesLauncher", "Saved", "Data");

            if (Directory.Exists(dataDir))
            {
                foreach (var jsonFile in Directory.GetFiles(dataDir, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(jsonFile);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("displayName", out var dn))
                        {
                            var name = dn.GetString();
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        return null;
    }

    public bool Connect(string? epicPath = null)
    {
        var path = epicPath ?? DetectEpicInstallPath();
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return false;

        _installPath = path;
        _displayName = DetectDisplayName() ?? "Epic Games";
        return true;
    }

    public void Disconnect()
    {
        _installPath = null;
        _displayName = null;
    }

    public EpicProfile? GetProfile()
    {
        if (!IsConnected)
            return null;

        return new EpicProfile
        {
            DisplayName = _displayName ?? "Epic Games",
            InstalledGamesCount = GetInstalledGamesCount()
        };
    }

    public int GetInstalledGamesCount()
    {
        try
        {
            return ScanEpicInstalledGames().Count;
        }
        catch
        {
            return 0;
        }
    }

    public List<Game> ScanEpicInstalledGames()
    {
        var games = new List<Game>();
        var foundPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var manifests = GetInstalledManifests();

        foreach (var manifest in manifests)
        {
            try
            {
                if (string.IsNullOrEmpty(manifest.InstallLocation) ||
                    !Directory.Exists(manifest.InstallLocation))
                    continue;

                var exeFiles = Directory.GetFiles(manifest.InstallLocation, "*.exe",
                    SearchOption.AllDirectories);

                var mainExe = exeFiles
                    .Select(f => new FileInfo(f))
                    .Where(f => f.Length > 100_000)
                    .Where(f => !IsExcludedExe(f.Name))
                    .OrderByDescending(f =>
                    {
                        if (!string.IsNullOrEmpty(manifest.LaunchExecutable))
                        {
                            var expectedName = Path.GetFileName(manifest.LaunchExecutable);
                            if (f.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
                                return long.MaxValue;
                        }
                        return f.Length;
                    })
                    .FirstOrDefault();

                if (mainExe is null || foundPaths.Contains(mainExe.FullName))
                    continue;

                foundPaths.Add(mainExe.FullName);

                var gameName = !string.IsNullOrEmpty(manifest.DisplayName)
                    ? manifest.DisplayName
                    : Path.GetFileName(manifest.InstallLocation);

                games.Add(new Game
                {
                    Name = gameName,
                    ExecutablePath = mainExe.FullName,
                    InstallDirectory = manifest.InstallLocation,
                    IconPath = IconExtractor.ExtractIcon(mainExe.FullName)
                });
            }
            catch { }
        }

        if (games.Count == 0 && _installPath is not null)
        {
            ScanDirectoryForGames(_installPath, games, foundPaths);
        }

        return games;
    }

    private static List<EpicManifest> GetInstalledManifests()
    {
        var manifests = new List<EpicManifest>();

        var manifestDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");

        if (!Directory.Exists(manifestDir))
            return manifests;

        foreach (var file in Directory.GetFiles(manifestDir, "*.item"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var manifest = JsonSerializer.Deserialize<EpicManifest>(json);
                if (manifest is not null && !string.IsNullOrEmpty(manifest.InstallLocation))
                    manifests.Add(manifest);
            }
            catch { }
        }

        return manifests;
    }

    private static void ScanDirectoryForGames(string rootPath, List<Game> games,
        HashSet<string> foundPaths)
    {
        try
        {
            foreach (var gameDir in Directory.GetDirectories(rootPath))
            {
                var dirName = Path.GetFileName(gameDir);
                if (dirName.Equals("Launcher", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("DirectXRedist", StringComparison.OrdinalIgnoreCase) ||
                    dirName.StartsWith(".", StringComparison.Ordinal))
                    continue;

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

                    games.Add(new Game
                    {
                        Name = dirName,
                        ExecutablePath = mainExe.FullName,
                        InstallDirectory = gameDir,
                        IconPath = IconExtractor.ExtractIcon(mainExe.FullName)
                    });
                }
                catch { }
            }
        }
        catch { }
    }

    private static readonly HashSet<string> ExcludedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "uninstall", "unins", "crash", "report", "updater", "patcher",
        "config", "settings", "setup", "installer", "redist",
        "easyanticheat", "battleye", "vc_redist", "dxsetup",
        "epicwebhelper", "epiconlineservices", "eosbootstrapper"
    };

    private static bool IsExcludedExe(string fileName)
    {
        return ExcludedKeywords.Any(k =>
            fileName.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
    }

    private sealed class EpicManifest
    {
        [JsonPropertyName("DisplayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("InstallLocation")]
        public string? InstallLocation { get; set; }

        [JsonPropertyName("LaunchExecutable")]
        public string? LaunchExecutable { get; set; }

        [JsonPropertyName("AppName")]
        public string? AppName { get; set; }

        [JsonPropertyName("CatalogNamespace")]
        public string? CatalogNamespace { get; set; }
    }
}
