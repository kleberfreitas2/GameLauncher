using System.Windows;
using GameLauncher.Services;
using GameLauncher.ViewModels;
using GameLauncher.Views;

namespace GameLauncher;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(StartupEventArgs e)
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

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var splash = new SplashWindow();
        splash.Show();
        splash.SetStatus("Iniciando o GLauncher...");

        // Let the splash render before heavy work
        await System.Threading.Tasks.Task.Delay(400);

        splash.SetStatus("Carregando biblioteca de jogos...");
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);

        var vm = new MainViewModel();

        splash.SetStatus("Preparando interface...");
        await System.Threading.Tasks.Task.Delay(300);

        var mainWindow = new MainWindow(vm);
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
        splash.Close();
    }
}
