using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using GameLauncher.Services;
using GameLauncher.ViewModels;
using GameLauncher.Views;

namespace GameLauncher;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Local\\GameLauncher_SingleInstance";
    private const string ActivateEventName = "Local\\GameLauncher_Activate";

    private static Mutex? _instanceMutex;
    private static EventWaitHandle? _activateEvent;
    private static CancellationTokenSource? _activateCts;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out var createdNew);
        if (!createdNew)
        {
            SignalFirstInstance();
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "glauncher_crash.txt"),
                args.Exception.ToString());
            MessageBox.Show(args.Exception.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        _activateEvent = new EventWaitHandle(initialState: false, mode: EventResetMode.AutoReset, name: ActivateEventName);
        _activateCts = new CancellationTokenSource();
        _ = Task.Run(() => ActivationLoop(_activateCts.Token));

        SettingsService.Load();
        SettingsService.ApplyTheme();

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var splash = new SplashWindow();
        splash.Show();
        splash.SetStatus("Iniciando o GLauncher...");

        // Let the splash render and animate before starting heavy work
        await System.Threading.Tasks.Task.Delay(400);

        var vm = new MainViewModel();
        await vm.InitializeAsync(status => splash.SetStatus(status));

        splash.SetStatus("Preparando interface...");
        await System.Threading.Tasks.Task.Delay(300);

        var mainWindow = new MainWindow(vm);
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
        splash.Close();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _activateCts?.Cancel(); } catch { }

        try { _activateEvent?.Dispose(); } catch { }
        _activateEvent = null;

        try
        {
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
        }
        catch { }
        _instanceMutex = null;

        base.OnExit(e);
    }

    private static void SignalFirstInstance()
    {
        try
        {
            using var evt = EventWaitHandle.OpenExisting(ActivateEventName);
            evt.Set();
        }
        catch
        {
            // ignore
        }
    }

    private void ActivationLoop(CancellationToken ct)
    {
        var handle = _activateEvent;
        if (handle is null) return;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                handle.WaitOne();
                if (ct.IsCancellationRequested) break;

                Dispatcher.Invoke(ActivateRunningWindow);
            }
            catch
            {
                // ignore
            }
        }
    }

    private void ActivateRunningWindow()
    {
        var window = Current?.MainWindow;
        if (window is null || !window.IsVisible)
            window = Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible);

        if (window is null)
            return;

        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Show();
        window.Activate();
        window.Focus();

        // Ajuda a "trazer para frente" mesmo quando o app está minimizado/atrás de outras janelas
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SW_RESTORE);
                SetForegroundWindow(hwnd);
            }
        }
        catch { }

        // Pequeno truque para garantir foco no topo
        var wasTopmost = window.Topmost;
        window.Topmost = true;
        window.Topmost = wasTopmost;
    }

    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
