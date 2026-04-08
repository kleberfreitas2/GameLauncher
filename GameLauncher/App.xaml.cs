using System.Windows;
using GameLauncher.Services;

namespace GameLauncher;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        SettingsService.Load();
        SettingsService.ApplyTheme();
    }
}
