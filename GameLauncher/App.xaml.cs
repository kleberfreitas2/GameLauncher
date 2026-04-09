using System.Windows;
using GameLauncher.Services;

namespace GameLauncher;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "glauncher_crash.txt"),
                args.Exception.ToString());
            MessageBox.Show(args.Exception.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        SettingsService.Load();
        SettingsService.ApplyTheme();
    }
}
