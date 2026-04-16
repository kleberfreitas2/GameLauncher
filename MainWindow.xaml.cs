using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using GameLauncher.Controls;
using GameLauncher.Services;
using GameLauncher.ViewModels;

namespace GameLauncher;

public partial class MainWindow : Window
{
    private static readonly SolidColorBrush FpsGreen;
    private static readonly SolidColorBrush FpsAmber;
    private static readonly SolidColorBrush FpsRed;

    static MainWindow()
    {
        FpsGreen = new SolidColorBrush(Color.FromRgb(0, 230, 118));
        FpsGreen.Freeze();
        FpsAmber = new SolidColorBrush(Color.FromRgb(255, 215, 64));
        FpsAmber.Freeze();
        FpsRed = new SolidColorBrush(Color.FromRgb(255, 82, 82));
        FpsRed.Freeze();
    }

    private int _frameCount;
    private TimeSpan _lastFpsTime;

    private List<MenuItem> _menuItems = [];
    private int _menuIndex;

    private bool _isFullscreen;
    private WindowStyle _previousWindowStyle;
    private WindowState _previousWindowState;
    private ResizeMode _previousResizeMode;
    private Rect _previousBounds;

    private System.Windows.Forms.NotifyIcon? _trayIcon;

    public MainWindow(ViewModels.MainViewModel viewModelParam)
    {
        InitializeComponent();
        DataContext = viewModelParam;
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        };

        var dpd = DependencyPropertyDescriptor.FromProperty(
            AnimatedImage.IsLoadingProperty, typeof(AnimatedImage));
        dpd?.AddValueChanged(BackgroundAnim, (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                vm.IsAnimationLoading = BackgroundAnim.IsLoading;
        });

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ContextMenuNavigate = NavigateContextMenu;

            if (BtnGear.ContextMenu is { } ctx)
            {
                ctx.Opened += (_, _) =>
                {
                    viewModel.IsContextMenuOpen = true;
                    _menuItems = CollectMenuItems(ctx);
                    _menuIndex = 0;
                    if (_menuItems.Count > 0)
                        HighlightMenuItem(_menuItems[0]);
                };
                ctx.Closed += (_, _) =>
                {
                    viewModel.IsContextMenuOpen = false;
                    _menuItems.Clear();
                    _menuIndex = 0;
                };
            }
        }

        KeyDown += MainWindow_KeyDown;

        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                EnsureTrayIcon();
                _trayIcon!.Visible = true;
            }
        };

        Loaded += (_, _) =>
        {
            CompositionTarget.Rendering += OnFpsRendering;
        };
        Closed += (_, _) =>
        {
            CompositionTarget.Rendering -= OnFpsRendering;
            _trayIcon?.Dispose();
            _trayIcon = null;
            (DataContext as MainViewModel)?.Dispose();
        };
    }


    #region System Tray

    private void EnsureTrayIcon()
    {
        if (_trayIcon is not null) return;

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "GLauncher",
            Icon = LoadAppIcon()
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Abrir GLauncher", null, (_, _) => RestoreFromTray());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        });
        _trayIcon.ContextMenuStrip = menu;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Maximized;
        Activate();
        if (_trayIcon is not null)
            _trayIcon.Visible = false;
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (exePath is not null)
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon is not null) return icon;
            }
        }
        catch { }
        return System.Drawing.SystemIcons.Application;
    }

    #endregion

    #region Win32 — constrain maximized window to work area (respect taskbar)

    private const int WM_GETMINMAXINFO = 0x0024;

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(monitor, ref mi);
            var work = mi.rcWork;
            var mon = mi.rcMonitor;
            mmi.ptMaxPosition = new POINT { x = work.Left - mon.Left, y = work.Top - mon.Top };
            mmi.ptMaxSize = new POINT { x = work.Right - work.Left, y = work.Bottom - work.Top };
            Marshal.StructureToPtr(mmi, lParam, true);
            handled = true;
        }
        return IntPtr.Zero;
    }

    #endregion

    private void BtnWinMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void BtnWinMaxRestore_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void BtnWinClose_Click(object sender, RoutedEventArgs e) => Close();

    private static List<MenuItem> CollectMenuItems(ContextMenu ctx)
    {
        var items = new List<MenuItem>();
        foreach (var item in ctx.Items)
        {
            if (item is MenuItem mi)
                items.Add(mi);
        }
        return items;
    }

    private void NavigateContextMenu(GamepadButton button)
    {
        if (_menuItems.Count == 0) return;

        switch (button)
        {
            case GamepadButton.DPadUp:
                _menuIndex = (_menuIndex - 1 + _menuItems.Count) % _menuItems.Count;
                HighlightMenuItem(_menuItems[_menuIndex]);
                break;

            case GamepadButton.DPadDown:
                _menuIndex = (_menuIndex + 1) % _menuItems.Count;
                HighlightMenuItem(_menuItems[_menuIndex]);
                break;

            case GamepadButton.A:
                var mi = _menuItems[_menuIndex];
                if (mi.Command is { } cmd && cmd.CanExecute(mi.CommandParameter))
                {
                    BtnGear.ContextMenu!.IsOpen = false;
                    cmd.Execute(mi.CommandParameter);
                }
                break;

            case GamepadButton.B:
            case GamepadButton.Back:
                BtnGear.ContextMenu!.IsOpen = false;
                break;
        }
    }

    private static void HighlightMenuItem(MenuItem target)
    {
        target.Focus();
    }

    private void OnFpsRendering(object? sender, EventArgs e)
    {
        var args = (RenderingEventArgs)e;
        _frameCount++;

        if (_lastFpsTime == TimeSpan.Zero)
        {
            _lastFpsTime = args.RenderingTime;
            return;
        }

        var elapsed = (args.RenderingTime - _lastFpsTime).TotalSeconds;
        if (elapsed >= 1.0)
        {
            var fps = (int)Math.Round(_frameCount / elapsed);
            FpsText.Text = fps.ToString();
            FpsText.Foreground = fps switch
            {
                >= 50 => FpsGreen,
                >= 30 => FpsAmber,
                _     => FpsRed
            };
            _frameCount = 0;
            _lastFpsTime = args.RenderingTime;
        }
    }

    private void BtnGear_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu is not null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void Avatar_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.ChangeAvatarCommand.Execute(null);
    }

    private void GameCarousel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is not null)
        {
            listBox.ScrollIntoView(listBox.SelectedItem);
        }
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _previousWindowStyle = WindowStyle;
            _previousWindowState = WindowState;
            _previousResizeMode = ResizeMode;
            _previousBounds = WindowState == WindowState.Maximized
                ? RestoreBounds
                : new Rect(Left, Top, Width, Height);

            if (WindowState != WindowState.Normal)
                WindowState = WindowState.Normal;

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;

            // Manual full-screen bounds instead of Maximized to avoid
            // DWM fullscreen optimization that suppresses overlays and can disrupt gdigrab
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            _isFullscreen = true;
        }
        else
        {
            WindowStyle = _previousWindowStyle;
            ResizeMode = _previousResizeMode;
            Left = _previousBounds.Left;
            Top = _previousBounds.Top;
            Width = _previousBounds.Width;
            Height = _previousBounds.Height;
            WindowState = _previousWindowState;
            _isFullscreen = false;
        }
    }
}
