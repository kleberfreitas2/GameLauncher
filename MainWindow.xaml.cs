using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using GameLauncher.Controls;
using GameLauncher.Models;
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
    private BigPictureTransitionService? _bpTransition;
    private GlobalHotkeyService? _hotkeys;

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

            // Serviço de transição Big Picture
            _bpTransition = new BigPictureTransitionService(this);

            // Interceptar o botão Big Picture para substituir pelo toggle animado
            if (DataContext is MainViewModel bpVm)
                bpVm.BigPictureTransitionRequested = PlayBigPictureTransition;

            // Custom drag-and-drop for card reordering
            GameCarousel.PreviewMouseLeftButtonDown += GameCarousel_PreviewMouseLeftButtonDown;
            GameCarousel.PreviewMouseMove += GameCarousel_PreviewMouseMove;
            GameCarousel.PreviewMouseMove += GameCarousel_PreviewMouseMove_Drag;
            GameCarousel.PreviewMouseLeftButtonUp += GameCarousel_PreviewMouseLeftButtonUp;

            // Sincronizar DropShadowEffects nomeados com o tema atual
            SettingsService.ThemeApplied += UpdateNamedGlowEffects;
            UpdateNamedGlowEffects();

            // Hotkey global para abrir GLauncher AI por cima do jogo.
            // Tenta Ctrl+Shift+A → Ctrl+Shift+G → Ctrl+F12 → Alt+F12 em ordem.
            _hotkeys = new GlobalHotkeyService(this);
            _hotkeys.RegisterWithFallback(
            [
                (GlobalHotkeyService.MOD_CTRL | GlobalHotkeyService.MOD_SHIFT, 0x41, "Ctrl+Shift+A"),  // A
                (GlobalHotkeyService.MOD_CTRL | GlobalHotkeyService.MOD_SHIFT, 0x47, "Ctrl+Shift+G"),  // G
                (GlobalHotkeyService.MOD_CTRL,                                  0x7B, "Ctrl+F12"),      // F12
                (GlobalHotkeyService.MOD_ALT,                                   0x7B, "Alt+F12"),       // F12
            ],
            OpenAiOverlay);
        };
        Closed += (_, _) =>
        {
            CompositionTarget.Rendering -= OnFpsRendering;
            SettingsService.ThemeApplied -= UpdateNamedGlowEffects;
            _hotkeys?.Dispose();
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

    /// <summary>
    /// Abre o GLauncher AI como overlay flutuante por cima do jogo (Ctrl+Shift+G).
    /// </summary>
    private void OpenAiOverlay()
    {
        Dispatcher.Invoke(() =>
        {
            if (DataContext is not MainViewModel vm) return;

            var groqKey   = GameLauncher.Services.SettingsService.Current.GroqApiKey;
            var openAiKey = GameLauncher.Services.SettingsService.Current.OpenAiApiKey;

            AiProvider provider;
            string apiKey;

            if (!string.IsNullOrWhiteSpace(groqKey))
            {
                provider = AiProvider.Groq;
                apiKey   = groqKey;
            }
            else if (!string.IsNullOrWhiteSpace(openAiKey))
            {
                provider = AiProvider.OpenAI;
                apiKey   = openAiKey;
            }
            else return;

            // Verifica se já há um overlay aberto — traz para frente e dá foco
            foreach (Window w in Application.Current.Windows)
            {
                if (w is Views.AiAssistantDialog existing)
                {
                    ForceForeground(existing);
                    return;
                }
            }

            var gameName = vm.SelectedGame?.DisplayName ?? vm.RunningGameName;
            var overlay  = new Views.AiAssistantDialog(provider, apiKey, gameName)
            {
                Topmost       = true,
                Owner         = null,
                ShowInTaskbar = true
            };
            overlay.Show();
            ForceForeground(overlay);
        });
    }

    /// <summary>
    /// Força a janela para o primeiro plano e transfere o foco do teclado,
    /// mesmo quando um jogo está com o input capturado.
    /// </summary>
    private static void ForceForeground(Window window)
    {
        var helper = new WindowInteropHelper(window);
        var targetHwnd = helper.Handle;

        // Obtém a thread do processo atual e do processo em foreground
        var foregroundHwnd   = GetForegroundWindow();
        var foregroundThread = GetWindowThreadProcessId(foregroundHwnd, out _);
        var currentThread    = GetCurrentThreadId();

        // Anexa temporariamente as threads para poder roubar o foco
        var attached = foregroundThread != currentThread &&
                       AttachThreadInput(currentThread, foregroundThread, true);

        ShowWindow(targetHwnd, 9 /* SW_RESTORE */);
        SetForegroundWindow(targetHwnd);

        if (attached)
            AttachThreadInput(currentThread, foregroundThread, false);

        window.Topmost = true;
        window.Activate();
        window.Focus();
    }

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

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

    #region Drag-and-Drop Reorder

    private Game? _draggedGame;
    private ListBoxItem? _draggedItem;
    private bool _isDragging;
    private Point _dragStartPoint;
    private const double DragThreshold = 8;

    private Game? _pendingSelectGame;

    private void GameCarousel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(this);
        _isDragging = false;
        _draggedGame = null;
        _draggedItem = null;

        // Identify clicked card but defer selection to MouseUp
        var source = e.OriginalSource as DependencyObject;
        var item = FindAncestor<ListBoxItem>(source);
        _pendingSelectGame = item?.DataContext as Game;

        // Suppress default ListBox selection on mouse down to prevent layout shift
        if (_pendingSelectGame is not null)
            e.Handled = true;
    }

    private void GameCarousel_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _isDragging) return;

        var pos = e.GetPosition(this);
        var diff = pos - _dragStartPoint;
        if (Math.Abs(diff.X) < DragThreshold && Math.Abs(diff.Y) < DragThreshold) return;

        // Resolve the dragged item from click origin
        var source = e.OriginalSource as DependencyObject;
        var item = FindAncestor<ListBoxItem>(source);
        if (item?.DataContext is not Game game) return;

        _draggedGame = game;
        _draggedItem = item;
        _isDragging = true;

        // Disable hover effects on individual cards during drag
        SetCarouselItemsHitTestVisible(false);

        // Setup ghost image from the card's cover
        DragGhost.Visibility = Visibility.Visible;
        DropIndicator.Visibility = Visibility.Visible;

        if (!string.IsNullOrEmpty(game.EffectiveImagePath))
        {
            try { DragGhostImage.Source = new BitmapImage(new Uri(game.EffectiveImagePath)); }
            catch { DragGhostImage.Source = null; }
        }
        else
        {
            DragGhostImage.Source = null;
        }

        // Capture mouse to track movement outside the list
        GameCarousel.CaptureMouse();
        UpdateDragVisuals(pos);
    }

    private void GameCarousel_PreviewMouseMove_Drag(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        var pos = e.GetPosition(this);
        UpdateDragVisuals(pos);
    }

    private void GameCarousel_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            // Verificar se o clique foi sobre o botão JOGAR
            var source = e.OriginalSource as DependencyObject;
            var clickedButton = FindAncestor<Button>(source);
            bool isPlayButton = clickedButton?.Name == "PlayBtn";

            // Aplicar seleção diferida
            if (_pendingSelectGame is not null)
                GameCarousel.SelectedItem = _pendingSelectGame;

            // Se clicou no JOGAR, lançar o jogo
            if (isPlayButton && _pendingSelectGame is not null && DataContext is MainViewModel vm)
            {
                e.Handled = true;
                vm.LaunchGameCommand.Execute(_pendingSelectGame);
            }

            _pendingSelectGame = null;
            EndDrag();
            return;
        }

        _pendingSelectGame = null;
        var pos = e.GetPosition(this);
        var target = GetCardAtPoint(pos);
        if (target is not null && target != _draggedGame && DataContext is MainViewModel vm2)
            vm2.ReorderGame(_draggedGame!, target);

        EndDrag();
    }

    private void UpdateDragVisuals(Point mousePos)
    {
        // Position ghost centered on cursor, offset slightly up-left
        var ghostX = mousePos.X - 77;
        var ghostY = mousePos.Y - 105;
        DragGhost.Margin = new Thickness(ghostX, ghostY, 0, 0);

        // Find target card and show drop indicator beside it
        var target = GetCardAtPoint(mousePos);
        if (target is not null && target != _draggedGame)
        {
            var targetItem = GetListBoxItemForGame(target);
            if (targetItem is not null)
            {
                var itemPos = targetItem.TransformToAncestor(this).Transform(new Point(0, 0));
                var itemH = targetItem.ActualHeight;
                var indicatorX = mousePos.X < itemPos.X + targetItem.ActualWidth / 2
                    ? itemPos.X - 2
                    : itemPos.X + targetItem.ActualWidth - 2;

                DropIndicator.Margin = new Thickness(indicatorX, itemPos.Y, 0, 0);
                DropIndicator.Height = itemH > 0 ? itemH : 210;
                DropIndicator.Visibility = Visibility.Visible;
            }
        }
        else
        {
            DropIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private Game? GetCardAtPoint(Point windowPos)
    {
        // Temporarily re-enable hit testing on items for drop detection
        SetCarouselItemsHitTestVisible(true);
        try
        {
            var listPos = GameCarousel.PointFromScreen(this.PointToScreen(windowPos));
            var hit = VisualTreeHelper.HitTest(GameCarousel, listPos);
            if (hit is null) return null;
            var item = FindAncestor<ListBoxItem>(hit.VisualHit);
            return item?.DataContext as Game;
        }
        finally
        {
            if (_isDragging) SetCarouselItemsHitTestVisible(false);
        }
    }

    private void SetCarouselItemsHitTestVisible(bool visible)
    {
        for (int i = 0; i < GameCarousel.Items.Count; i++)
        {
            if (GameCarousel.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem item)
                item.IsHitTestVisible = visible;
        }
    }

    private ListBoxItem? GetListBoxItemForGame(Game game)
    {
        return GameCarousel.ItemContainerGenerator.ContainerFromItem(game) as ListBoxItem;
    }

    private void EndDrag()
    {
        _isDragging = false;
        _draggedGame = null;
        _draggedItem = null;
        DragGhost.Visibility = Visibility.Collapsed;
        DropIndicator.Visibility = Visibility.Collapsed;
        if (GameCarousel.IsMouseCaptured)
            GameCarousel.ReleaseMouseCapture();

        // Re-enable hover effects after drag ends
        SetCarouselItemsHitTestVisible(true);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
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

    private void UpdateNamedGlowEffects()
    {
        if (ColorConverter.ConvertFromString(SettingsService.Current.AccentColor) is not Color accent)
            return;

        if (WelcomeIconGlow    is DropShadowEffect wg) wg.Color = accent;
        if (DragGhostGlow      is DropShadowEffect dg) dg.Color = accent;
        if (DropIndicatorGlow  is DropShadowEffect di) di.Color = accent;
        if (AccentGradientStop is GradientStop      gs) gs.Color = accent;
    }

    /// <summary>
    /// Chamado pelo ViewModel quando o usuário aciona o Modo Big Picture.
    /// Executa a animação de transição e aplica a mudança de estado ao término.
    /// </summary>
    private bool _wasBigPictureFullscreen;
    private WindowStyle _bpPreviousWindowStyle;
    private WindowState _bpPreviousWindowState;
    private ResizeMode _bpPreviousResizeMode;
    private Rect _bpPreviousBounds;

    private void EnterBigPictureFullscreen()
    {
        _bpPreviousWindowStyle = WindowStyle;
        _bpPreviousWindowState = WindowState;
        _bpPreviousResizeMode = ResizeMode;
        _bpPreviousBounds = WindowState == WindowState.Maximized
            ? RestoreBounds
            : new Rect(Left, Top, Width, Height);

        if (WindowState != WindowState.Normal)
            WindowState = WindowState.Normal;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Left   = 0;
        Top    = 0;
        Width  = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
        _wasBigPictureFullscreen = true;
    }

    private void ExitBigPictureFullscreen()
    {
        if (!_wasBigPictureFullscreen) return;
        _wasBigPictureFullscreen = false;

        WindowStyle = _bpPreviousWindowStyle;
        ResizeMode  = _bpPreviousResizeMode;

        if (_bpPreviousWindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Maximized;
        }
        else
        {
            Left   = _bpPreviousBounds.Left;
            Top    = _bpPreviousBounds.Top;
            Width  = _bpPreviousBounds.Width;
            Height = _bpPreviousBounds.Height;
            WindowState = _bpPreviousWindowState;
        }
    }

    private void PlayBigPictureTransition(bool entering)
    {
        if (_bpTransition is null) return;

        if (entering)
        {
            EnterBigPictureFullscreen();
            _bpTransition.PlayEnter(onComplete: () => { });
        }
        else
        {
            _bpTransition.PlayExit(onComplete: () =>
            {
                ExitBigPictureFullscreen();
            });
        }
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
