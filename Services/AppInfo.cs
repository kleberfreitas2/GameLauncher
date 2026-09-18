using System.Reflection;

namespace GameLauncher.Services;

public static class AppInfo
{
    public const string GitHubUrl = "https://github.com/kleberfreitas2/GameLauncher";
    public const string ReleasesUrl = "https://github.com/kleberfreitas2/GameLauncher/releases";

    public static string Version =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "2.8.0";
}
