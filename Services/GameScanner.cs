using System.IO;
using GameLauncher.Models;

namespace GameLauncher.Services;

public class GameScanner
{
    private readonly HashSet<string> _excludeKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "uninstall", "unins", "crash", "report", "updater", "patcher",
        "config", "settings", "setup", "installer", "redist", "redistributable",
        "easyanticheat", "battleye", "_be_"
    };

    public async Task<List<Game>> ScanFolderAsync(string folderPath)
    {
        return await Task.Run(() =>
        {
            var games = new List<Game>();
            var foundPaths = new HashSet<string>();

            ScanDirectory(folderPath, games, foundPaths, maxDepth: 4);

            return games.OrderBy(g => g.Name).ToList();
        });
    }

    private void ScanDirectory(string path, List<Game> games, HashSet<string> foundPaths, int currentDepth = 0, int maxDepth = 4)
    {
        if (currentDepth > maxDepth) return;

        try
        {
            var exeFiles = Directory.GetFiles(path, "*.exe", SearchOption.TopDirectoryOnly);

            foreach (var exePath in exeFiles)
            {
                if (foundPaths.Contains(exePath)) continue;

                var fileName = Path.GetFileNameWithoutExtension(exePath);
                var fileInfo = new FileInfo(exePath);

                if (fileInfo.Length < 50_000) continue;

                if (_excludeKeywords.Any(k => fileName.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    continue;

                games.Add(new Game
                {
                    Name = fileName,
                    ExecutablePath = exePath,
                    InstallDirectory = path,
                    IconPath = exePath
                });

                foundPaths.Add(exePath);
            }

            foreach (var directory in Directory.GetDirectories(path))
            {
                ScanDirectory(directory, games, foundPaths, currentDepth + 1, maxDepth);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao escanear {path}: {ex.Message}");
        }
    }
}
