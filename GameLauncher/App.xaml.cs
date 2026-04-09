using System.Windows;
using GameLauncher.Services;

namespace GameLauncher;

public partial class App : System.Windows.Application
{
    private bool _shutdownHandled;

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

    protected override void OnExit(ExitEventArgs e)
    {
        if (!_shutdownHandled)
        {
            _shutdownHandled = true;

            foreach (Window window in Windows)
            {
                if (window.DataContext is IDisposable disposable)
                {
                    try { disposable.Dispose(); } catch { }
                }
            }

            if (MainWindow?.DataContext is IDisposable mainDisposable)
            {
                try { mainDisposable.Dispose(); } catch { }
            }
        }

        base.OnExit(e);
    }
}
