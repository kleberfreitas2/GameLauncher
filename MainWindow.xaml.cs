using System.ComponentModel;
using System.Diagnostics;
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
using GameLauncher.Views;

namespace GameLauncher;

public partial class MainWindow : Window
{
    private void GameCardContextMenu_Opening(object sender, ContextMenuEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Game game
            && DataContext is MainViewModel viewModel)
        {
            viewModel.DetailGame = game;
            if (element.ContextMenu is { } menu)
            {
                _activeContextMenu = menu;
                _menuItems = CollectMenuItems(menu);
                _menuIndex = 0;
                viewModel.IsContextMenuOpen = true;
                if (_menuItems.Count > 0)
                    HighlightMenuItem(_menuItems[0]);
                menu.Opened -= ContextMenu_Opened;
                menu.Opened += ContextMenu_Opened;
                menu.Closed -= ContextMenu_Closed;
                menu.Closed += ContextMenu_Closed;
            }
        }
    }
    private List<MenuItem> _menuItems = [];
    private int _menuIndex;
    private ContextMenu? _activeContextMenu;

    private bool _isFullscreen;
    private WindowStyle _previousWindowStyle;
    private WindowState _previousWindowState;
    private ResizeMode _previousResizeMode;
    private Rect _previousBounds;

    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private BigPictureTransitionService? _bpTransition;
    private GlobalHotkeyService? _hotkeys;

    // Easter Egg
    private int _logoClickCount;
    private System.Windows.Threading.DispatcherTimer? _logoClickTimer;

    private static readonly string[] EggMessages =
    [
        "🎮 Você encontrou o Easter Egg!",
        "🕵️ Curioso(a), hein? Te peguei!",
        "🐣 Boa caçada, gamer!",
        "🔥 Só os lendários chegam aqui!",
        "👾 Nível oculto desbloqueado!",
    ];

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
            viewModel.OpenSelectedGameMenu = OpenSelectedGameMenu;

            // Atualiza o gamerscore de troféus no header
            if (BtnGear.ContextMenu is { } ctx)
            {
                ctx.Opened += (_, _) =>
                {
                     _activeContextMenu = ctx;
                    viewModel.IsContextMenuOpen = true;
                    _menuItems = CollectMenuItems(ctx);
                    _menuIndex = 0;
                    if (_menuItems.Count > 0)
                        HighlightMenuItem(_menuItems[0]);
                };
                ctx.Closed += (_, _) =>
                {
                     _activeContextMenu = null;
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
            Dispatcher.BeginInvoke(
                new Action(SelectFirstGameOnEntry),
                System.Windows.Threading.DispatcherPriority.Loaded);

            // Serviço de transição Big Picture
            _bpTransition = new BigPictureTransitionService(this);

            // Evento para selecionar logotipo


            // Interceptar o botão Big Picture para substituir pelo toggle animado
            if (DataContext is MainViewModel bpVm)
                bpVm.BigPictureTransitionRequested = PlayBigPictureTransition;

            // Custom drag-and-drop for card reordering
            GameCarousel.PreviewMouseLeftButtonDown += GameCarousel_PreviewMouseLeftButtonDown;
            GameCarousel.PreviewMouseMove += GameCarousel_PreviewMouseMove;
            GameCarousel.PreviewMouseMove += GameCarousel_PreviewMouseMove_Drag;
            GameCarousel.PreviewMouseLeftButtonUp += GameCarousel_PreviewMouseLeftButtonUp;
            GameCarousel.SizeChanged += (_, _) =>
            {
                if (DataContext is MainViewModel gridVm)
                    gridVm.SetGamepadGridColumns(
                        Math.Max(1, (int)(GameCarousel.ActualWidth / 175)));
            };

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

            // Aviso inicial da IA desativado intencionalmente.
            // ScheduleAiCallout();
        };
        Closed += (_, _) =>
        {
            SettingsService.ThemeApplied -= UpdateNamedGlowEffects;
            _hotkeys?.Dispose();
            _trayIcon?.Dispose();
            _trayIcon = null;
            (DataContext as MainViewModel)?.Dispose();
        };
    }


    private void SelectFirstGameOnEntry()
    {
        if (GameCarousel.Items.Count == 0)
            return;

        GameCarousel.SelectedIndex = 0;
        GameCarousel.Focus();
        Keyboard.Focus(GameCarousel);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog { Owner = this };
        dialog.ShowDialog();
    }

    private void OpenSelectedGameMenu()
    {
        if (GameCarousel.SelectedItem is not Game game)
            return;

        GameCarousel.ScrollIntoView(game);
        GameCarousel.UpdateLayout();
        var item = GetListBoxItemForGame(game);
        var target = item is null ? null : FindContextMenuTarget(item);
        if (target?.ContextMenu is not ContextMenu menu)
            return;

        // O ContextMenu é exibido em uma janela Popup separada. Ao abri-lo pelo
        // gamepad, o evento Opening/Opened pode ocorrer depois que o evento Y
        // já terminou. Marcar o estado antes de IsOpen evita que o próximo
        // comando do controle volte a ser tratado pelo carrossel.
        if (DataContext is MainViewModel viewModel)
        {
            _activeContextMenu = menu;
            _menuItems = CollectMenuItems(menu);
            _menuIndex = 0;
            viewModel.IsContextMenuOpen = true;
        }

        menu.PlacementTarget = target;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
            return;

        _activeContextMenu = menu;
        _menuItems = CollectMenuItems(menu);
        _menuIndex = 0;
        if (DataContext is MainViewModel viewModel)
            viewModel.IsContextMenuOpen = true;

        // O primeiro item precisa receber foco imediatamente. Deixar esse
        // foco para um callback posterior permite que o ListBox do carrossel
        // o recupere e interprete o D-Pad como navegação entre jogos.
        if (_menuItems.Count > 0)
        {
            menu.Focus();
            HighlightMenuItem(_menuItems[0]);
        }
    }

    private void ContextMenu_Closed(object? sender, RoutedEventArgs e)
    {
        var restoreFocus = sender is ContextMenu closedMenu && closedMenu.PlacementTarget is not null;

        if (sender is ContextMenu menu && ReferenceEquals(_activeContextMenu, menu))
            _activeContextMenu = null;

        _menuItems.Clear();
        _menuIndex = 0;
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.IsContextMenuOpen = false;

            // O Popup do ContextMenu fica fora da árvore visual principal e,
            // ao fechá-lo, o foco pode permanecer no MenuItem. Devolvê-lo ao
            // carrossel garante que o próximo comando continue navegando pelos
            // cards, inclusive após B/○.
            if (restoreFocus)
            {
                Dispatcher.BeginInvoke(RestoreGamepadFocus,
                    System.Windows.Threading.DispatcherPriority.Input);
            }
        }
    }

    private void RestoreGamepadFocus()
    {
        if (DataContext is MainViewModel viewModel)
        {
            // Libera explicitamente o estado do menu antes de devolver o foco.
            // Isso evita que o próximo comando do controle seja descartado
            // enquanto o Popup do ContextMenu ainda está sendo desmontado.
            viewModel.IsContextMenuOpen = false;
            viewModel.ActiveZone = MainViewModel.NavZone.Carousel;
        }

        // Não roubar o foco de diálogos abertos por uma opção do menu.
        if (Application.Current.Windows.OfType<Window>()
            .Any(window => window != this && window.IsVisible && window.IsActive))
            return;

        GameCarousel.Focus();
        Keyboard.Focus(GameCarousel);
    }

    private static FrameworkElement? FindContextMenuTarget(DependencyObject root)
    {
        if (root is FrameworkElement element && element.ContextMenu is not null)
            return element;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var target = FindContextMenuTarget(VisualTreeHelper.GetChild(root, i));
            if (target is not null)
                return target;
        }

        return null;
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
        RestoreLauncherWindow(WindowState.Maximized);
    }

    public void RestoreAfterGame()
    {
        if (_trayIcon is not null)
            _trayIcon.Visible = false;

        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Focus();
    }

    private void RestoreLauncherWindow(WindowState state)
    {
        if (_trayIcon is not null)
            _trayIcon.Visible = false;

        Show();
        WindowState = state;
        Activate();
        Focus();
    }

    /// <summary>
    /// Abre o GLauncher AI como overlay flutuante por cima do jogo (Ctrl+Shift+G).
    /// </summary>
    private void OpenAiOverlay()
    {
        Dispatcher.Invoke(() =>
        {
            if (DataContext is not MainViewModel vm) return;

            var environmentGroqKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
            var groqKey = !string.IsNullOrWhiteSpace(environmentGroqKey)
                ? environmentGroqKey.Trim()
                : !string.IsNullOrWhiteSpace(GameLauncher.Services.SecretsService.GroqApiKey)
                    ? GameLauncher.Services.SecretsService.GroqApiKey
                    : GameLauncher.Services.SettingsService.Current.GroqApiKey;
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
            var openedDuringGame = !string.IsNullOrWhiteSpace(vm.RunningGameName);
            var overlay  = new Views.AiAssistantDialog(
                provider, apiKey, gameName, viaGamepad: openedDuringGame, xinput: vm.XInput)
            {
                Topmost       = true,
                Owner         = null,
                ShowInTaskbar = false,
                WindowState   = WindowState.Normal,
                Width         = 620,
                Height        = 640,
                ShowActivated = true
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
            {
                GameCarousel.SelectedItem = _pendingSelectGame;
                GameCarousel.Focus();
            }

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

    private void GameCarousel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (GameCarousel.Items.Count == 0)
            return;

        if (e.Key == Key.Enter &&
            GameCarousel.SelectedItem is Game selectedGame &&
            DataContext is MainViewModel viewModel)
        {
            viewModel.LaunchGameCommand.Execute(selectedGame);
            e.Handled = true;
            return;
        }

        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down))
            return;

        var currentIndex = GameCarousel.SelectedIndex;
        if (currentIndex < 0)
            currentIndex = 0;

        var targetIndex = e.Key switch
        {
            Key.Left => currentIndex - 1,
            Key.Right => currentIndex + 1,
            _ => FindVerticalGameIndex(currentIndex, e.Key == Key.Down)
        };

        if (targetIndex >= 0 && targetIndex < GameCarousel.Items.Count)
        {
            GameCarousel.SelectedIndex = targetIndex;
            GameCarousel.ScrollIntoView(GameCarousel.SelectedItem);
            e.Handled = true;
        }
    }

    private int FindVerticalGameIndex(int currentIndex, bool down)
    {
        if (GameCarousel.ItemContainerGenerator.ContainerFromIndex(currentIndex) is not ListBoxItem currentItem)
            return currentIndex;

        var currentCenter = currentItem.TranslatePoint(
            new Point(currentItem.ActualWidth / 2, currentItem.ActualHeight / 2), GameCarousel);
        var bestIndex = currentIndex;
        var bestScore = double.MaxValue;

        for (var i = 0; i < GameCarousel.Items.Count; i++)
        {
            if (i == currentIndex ||
                GameCarousel.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem item)
                continue;

            var center = item.TranslatePoint(
                new Point(item.ActualWidth / 2, item.ActualHeight / 2), GameCarousel);
            var verticalDistance = center.Y - currentCenter.Y;

            if ((down && verticalDistance <= 5) || (!down && verticalDistance >= -5))
                continue;

            var score = Math.Abs(verticalDistance) * 10 + Math.Abs(center.X - currentCenter.X);
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
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

    private void BtnYoutube_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.youtube.com/@CarecaRetro",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private void YoutubeFooter_Click(object sender, RoutedEventArgs e)
        => BtnYoutube_Click(sender, e);

    private void Screenshot_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Image image || image.DataContext is not string screenshotUrl)
            return;

        if (DataContext is not MainViewModel vm || vm.DetailGame?.Screenshots is not { Count: > 0 } screenshots)
            return;

        var index = screenshots.IndexOf(screenshotUrl);
        var viewer = new Views.ScreenshotViewerDialog(screenshots, Math.Max(0, index))
        {
            Owner = this
        };
        viewer.ShowDialog();
        e.Handled = true;
    }

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
        if (_activeContextMenu is null || !_activeContextMenu.IsOpen)
            return;

        if (_menuItems.Count == 0)
            _menuItems = CollectMenuItems(_activeContextMenu);

        if (_menuItems.Count == 0)
            return;

        switch (button)
        {
            case GamepadButton.DPadUp:
                _menuIndex = (_menuIndex - 1 + _menuItems.Count) % _menuItems.Count;
                HighlightMenuItem(_menuItems[_menuIndex]);
                break;

            case GamepadButton.DPadLeft:
            case GamepadButton.DPadDown:
                _menuIndex = (_menuIndex + 1) % _menuItems.Count;
                HighlightMenuItem(_menuItems[_menuIndex]);
                break;

            case GamepadButton.DPadRight:
                _menuIndex = (_menuIndex - 1 + _menuItems.Count) % _menuItems.Count;
                HighlightMenuItem(_menuItems[_menuIndex]);
                break;

            case GamepadButton.A:
                var mi = _menuItems[_menuIndex];
                var game = (_activeContextMenu?.PlacementTarget as FrameworkElement)?.DataContext as Game;
                if (game is not null && IsGameMenuItem(mi.Header?.ToString()))
                {
                    if (_activeContextMenu is not null)
                        _activeContextMenu.IsOpen = false;
                    ExecuteGameMenuItem(mi.Header?.ToString(), game);
                    break;
                }

                if (_activeContextMenu is not null)
                    _activeContextMenu.IsOpen = false;
                mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                break;

            case GamepadButton.B:
            case GamepadButton.Back:
                if (_activeContextMenu is not null)
                    _activeContextMenu.IsOpen = false;
                RestoreGamepadFocus();
                break;
        }
    }

    private void ExecuteGameMenuItem(string? header, Game game)
    {
        if (DataContext is not MainViewModel viewModel || string.IsNullOrWhiteSpace(header))
            return;

        switch (header)
        {
            case "Alternar Favorito":
                viewModel.ToggleFavoriteCommand.Execute(game);
                break;
            case "Alterar Imagem":
                viewModel.ChangeImageCommand.Execute(game);
                break;
            case "Buscar Capa Online":
                viewModel.SearchCoverCommand.Execute(game);
                break;
            case "Buscar Fundo (Screenshot/Key Art)":
                viewModel.SearchBackgroundCommand.Execute(game);
                break;
            case "Buscar Info IGDB":
                viewModel.FetchIgdbInfoCommand.Execute(game);
                break;
            case "Re-escanear Tecnologias":
                viewModel.RescanGameTechCommand.Execute(null);
                break;
            case "Recomendação Gráfica (Hardware)":
                viewModel.OpenHwRecommendationCommand.Execute(null);
                break;
            case "Renomear":
                viewModel.RenameGameCommand.Execute(game);
                break;
            case "Remover Jogo":
                viewModel.RemoveGameCommand.Execute(game);
                break;
        }
    }

    private static bool IsGameMenuItem(string? header)
    {
        return header is "Alternar Favorito"
            or "Alterar Imagem"
            or "Buscar Capa Online"
            or "Buscar Fundo (Screenshot/Key Art)"
            or "Buscar Info IGDB"
            or "Re-escanear Tecnologias"
            or "Recomendação Gráfica (Hardware)"
            or "Renomear"
            or "Remover Jogo";
    }

    private void HighlightMenuItem(MenuItem target)
    {
        var selectedBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0xD7, 0x6B));
        foreach (var item in _menuItems)
            item.Background = ReferenceEquals(item, target)
                ? selectedBrush
                : Brushes.Transparent;

        target.Focus();
        Keyboard.Focus(target);
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


    // =========================================================================
    // Callout "Experimente a IA" — todo o visual e animacao em codigo
    // =========================================================================

    private const int    AiCalloutMaxShows    = 5;
    private const double CalloutDisplaySec    = 6.0;

    private System.Windows.Threading.DispatcherTimer? _calloutTimer;

    // Refs ao visual do callout (criadas em BuildCalloutContent)
    private Border?    _calloutRoot;
    private System.Windows.Shapes.Path? _calloutArrow;
    private Border?    _calloutTimerBar;
    private TextBlock? _calloutHotkeyBadge;
    private System.Windows.Media.TranslateTransform? _calloutTranslate;
    private System.Windows.Media.ScaleTransform?     _calloutScale;
    private System.Windows.Media.ScaleTransform?     _iconPulse;
    private System.Windows.Controls.Primitives.Popup?  _aiPopup;

    private void ScheduleAiCallout()
    {
        if (SettingsService.Current.AiNotificationCount >= AiCalloutMaxShows) return;

        var delay = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromSeconds(8) };
        delay.Tick += (_, _) =>
        {
            delay.Stop();
            // O aviso da IA não pode competir com a inicialização da interface
            // nem bloquear o primeiro uso do launcher.
            Dispatcher.BeginInvoke(BuildAndShowCallout,
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        };
        delay.Start();
    }

    private void BuildAndShowCallout()
    {
        // Accent color
        var accent = (System.Windows.Media.Brush?)TryFindResource("AccentBrush")
                     ?? new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0x7C, 0x4D, 0xFF));

        var dark   = new System.Windows.Media.SolidColorBrush(
                         System.Windows.Media.Color.FromArgb(0xFF, 0x1A, 0x15, 0x35));
        var border = new System.Windows.Media.SolidColorBrush(
                         System.Windows.Media.Color.FromArgb(0xFF, 0x3A, 0x2A, 0x6A));
        var textDim = new System.Windows.Media.SolidColorBrush(
                          System.Windows.Media.Color.FromArgb(0xFF, 0x88, 0x77, 0xBB));
        var textMain = System.Windows.Media.Brushes.White;
        var textSub  = new System.Windows.Media.SolidColorBrush(
                           System.Windows.Media.Color.FromArgb(0xFF, 0xCC, 0xBB, 0xEE));

        // --- Seta apontando para cima ---
        _calloutArrow = new System.Windows.Shapes.Path
        {
            Data = System.Windows.Media.Geometry.Parse("M 135,0 L 145,10 L 125,10 Z"),
            Fill = dark,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment   = VerticalAlignment.Top,
            Opacity = 0
        };

        // --- Transforms do corpo ---
        _calloutTranslate = new System.Windows.Media.TranslateTransform { Y = -14 };
        _calloutScale     = new System.Windows.Media.ScaleTransform { ScaleX = 0.90, ScaleY = 0.90,
                                CenterX = 115, CenterY = 0 };
        var tg = new System.Windows.Media.TransformGroup();
        tg.Children.Add(_calloutTranslate);
        tg.Children.Add(_calloutScale);

        // --- Ícone robô com pulso ---
        _iconPulse = new System.Windows.Media.ScaleTransform { ScaleX = 1, ScaleY = 1,
                         CenterX = 10, CenterY = 10 };
        var robotIcon = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind   = MaterialDesignThemes.Wpf.PackIconKind.Robot,
            Width  = 20, Height = 20,
            Foreground = accent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            RenderTransform     = _iconPulse,
            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5)
        };
        var iconBorder = new Border
        {
            Background          = new System.Windows.Media.SolidColorBrush(
                                      System.Windows.Media.Color.FromArgb(0xFF, 0x2A, 0x1A, 0x5A)),
            CornerRadius        = new CornerRadius(10),
            Width = 34, Height  = 34,
            Margin              = new Thickness(0, 0, 10, 0),
            Child               = robotIcon
        };

        // --- Textos ---
        var titleTxt = new TextBlock
        {
            Text = "GLauncher AI", FontSize = 13,
            FontWeight = FontWeights.Bold, Foreground = textMain,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
        };
        var subTxt = new TextBlock
        {
            Text = "Seu assistente gamer 🎮", FontSize = 10,
            Foreground = textDim,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
        };
        var titleStack = new StackPanel();
        titleStack.Children.Add(titleTxt);
        titleStack.Children.Add(subTxt);

        // --- Botão fechar ---
        var closeIcon = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = MaterialDesignThemes.Wpf.PackIconKind.Close,
            Width = 13, Height = 13,
            Foreground = new System.Windows.Media.SolidColorBrush(
                             System.Windows.Media.Color.FromArgb(0xFF, 0x66, 0x55, 0xAA))
        };
        var closeBtn = new Button
        {
            Content = closeIcon, Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(8, -2, -2, 0)
        };
        closeBtn.Click += (_, _) => DismissAiCallout();

        // Grid para empurrar o botão ✕ para a direita corretamente
        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(iconBorder, 0);
        Grid.SetColumn(titleStack, 1);
        Grid.SetColumn(closeBtn,   2);
        closeBtn.Margin = new Thickness(6, -2, 0, 0);
        closeBtn.VerticalAlignment = VerticalAlignment.Center;
        headerRow.Children.Add(iconBorder);
        headerRow.Children.Add(titleStack);
        headerRow.Children.Add(closeBtn);

        // --- Descrição ---
        var desc = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, FontSize = 12,
            Foreground = textSub,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            LineHeight = 18
        };
        desc.Inlines.Add(new System.Windows.Documents.Run("Dicas, segredos e análises com "));
        var boldRun = new System.Windows.Documents.Run("inteligência artificial")
            { FontWeight = FontWeights.Bold, Foreground = accent };
        desc.Inlines.Add(boldRun);
        desc.Inlines.Add(new System.Windows.Documents.Run(" na palma da mão."));

        // --- Badge hotkey ---
        _calloutHotkeyBadge = new TextBlock
        {
            Text = GlobalHotkeyService.AiHotkeyLabel,
            FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = accent,
            FontFamily = new System.Windows.Media.FontFamily("Consolas")
        };
        var badgeBorder = new Border
        {
            Background   = new System.Windows.Media.SolidColorBrush(
                               System.Windows.Media.Color.FromArgb(0xFF, 0x2A, 0x1A, 0x5A)),
            CornerRadius = new CornerRadius(6),
            Padding      = new Thickness(7, 3, 7, 3),
            Margin       = new Thickness(0, 0, 6, 0),
            Child        = _calloutHotkeyBadge
        };
        var hintLabel = new TextBlock
        {
            Text = "ou clique no icone acima", FontSize = 11,
            Foreground = textDim, VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
        };
        var hotKeyRow = new StackPanel { Orientation = Orientation.Horizontal,
                                         Margin = new Thickness(0, 10, 0, 0) };
        hotKeyRow.Children.Add(badgeBorder);
        hotKeyRow.Children.Add(hintLabel);

        // --- Conteudo principal ---
        var content = new StackPanel { Margin = new Thickness(14, 14, 14, 10) };
        content.Children.Add(headerRow);
        content.Children.Add(new Border { Height = 8 });
        content.Children.Add(desc);
        content.Children.Add(hotKeyRow);

        // --- Barra de timer ---
        var timerTrack = new Border
        {
            Height = 4, CornerRadius = new CornerRadius(0, 0, 16, 16),
            Background = new System.Windows.Media.SolidColorBrush(
                             System.Windows.Media.Color.FromArgb(0xFF, 0x2A, 0x1A, 0x5A))
        };
        _calloutTimerBar = new Border
        {
            Height = 4, Width = 290,
            CornerRadius = new CornerRadius(0, 0, 16, 16),
            Background = accent,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        timerTrack.Child = _calloutTimerBar;

        // --- Grid raiz do corpo ---
        var bodyGrid = new Grid();
        bodyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        bodyGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
        Grid.SetRow(content, 0);
        Grid.SetRow(timerTrack, 1);
        bodyGrid.Children.Add(content);
        bodyGrid.Children.Add(timerTrack);

        // --- Border raiz animada ---
        _calloutRoot = new Border
        {
            Width        = 290,
            CornerRadius = new CornerRadius(16),
            Background   = dark,
            BorderBrush  = border,
            BorderThickness = new Thickness(1),
            Margin       = new Thickness(0, 9, 0, 0),
            RenderTransformOrigin = new System.Windows.Point(0.5, 0),
            RenderTransform = tg,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Color.FromRgb(0x7C, 0x4D, 0xFF),
                BlurRadius = 28, ShadowDepth = 0, Opacity = 0.35
            },
            Opacity = 0,
            Child   = bodyGrid
        };

        // --- Container com seta + corpo ---
        var container = new Grid();
        container.Children.Add(_calloutArrow);
        container.Children.Add(_calloutRoot);

        // Criar Popup em codigo (evita problemas de name scope do XAML)
        _aiPopup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = BtnAi,
            Placement       = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            AllowsTransparency = true,
            StaysOpen       = false,
            HorizontalOffset = -(290 - BtnAi.ActualWidth) / 2,
            VerticalOffset   = 6,
            Child            = container
        };
        _aiPopup.Opened += (_, _) =>
        {
            _aiPopup.HorizontalOffset = -(290 - BtnAi.ActualWidth) / 2;
        };
        _aiPopup.StaysOpen = false;

        // Abrir e animar
        _aiPopup!.IsOpen = true;
        PlayCalloutEnterAnimation();

        // Registra exibicao
        SettingsService.Current.AiNotificationCount++;
        SettingsService.Save();
    }

    private void PlayCalloutEnterAnimation()
    {
        var dur  = TimeSpan.FromMilliseconds(420);
        var ease = new System.Windows.Media.Animation.CubicEase
            { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };

        // Fade + slide + scale do corpo
        _calloutRoot!.BeginAnimation(OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0, 1, dur) { EasingFunction = ease });
        _calloutTranslate!.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            new System.Windows.Media.Animation.DoubleAnimation(-14, 0, dur) { EasingFunction = ease });
        _calloutScale!.BeginAnimation(
            System.Windows.Media.ScaleTransform.ScaleXProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0.90, 1.0, dur) { EasingFunction = ease });
        _calloutScale!.BeginAnimation(
            System.Windows.Media.ScaleTransform.ScaleYProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0.90, 1.0, dur) { EasingFunction = ease });

        // Seta
        _calloutArrow!.BeginAnimation(OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0, 1, dur));

        // Pulso do icone robô
        StartIconPulse();

        // Barra de timer: de 230 -> 0
        _calloutTimerBar!.BeginAnimation(WidthProperty,
            new System.Windows.Media.Animation.DoubleAnimation(290, 0,
                TimeSpan.FromSeconds(CalloutDisplaySec)));

        // Auto-fechar
        _calloutTimer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromSeconds(CalloutDisplaySec) };
        _calloutTimer.Tick += (_, _) => { _calloutTimer!.Stop(); DismissAiCallout(); };
        _calloutTimer.Start();
    }

    private void StartIconPulse()
    {
        if (_iconPulse is null) return;

        var pulse = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
            Duration = new Duration(TimeSpan.FromSeconds(2.4))
        };
        var easeBack = new System.Windows.Media.Animation.BackEase
            { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Amplitude = 0.3 };
        pulse.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
            1.0,  System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.Zero)));
        pulse.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
            1.25, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.3)),
            easeBack));
        pulse.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
            1.0,  System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.7))));

        _iconPulse.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, pulse);
        _iconPulse.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, pulse.Clone());
    }

    private void DismissAiCallout()
    {
        _calloutTimer?.Stop();
        if (_calloutRoot is null) return;

        var dur  = TimeSpan.FromMilliseconds(280);
        var ease = new System.Windows.Media.Animation.CubicEase
            { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn };

        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, dur)
            { EasingFunction = ease };
        fadeOut.Completed += (_, _) =>
        {
            _aiPopup!.IsOpen = false;
            _aiPopup!.Child = null;
            _calloutRoot = null;
        };

        _calloutRoot.BeginAnimation(OpacityProperty, fadeOut);
        _calloutTranslate?.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0, -10, dur) { EasingFunction = ease });
        _calloutArrow?.BeginAnimation(OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(1, 0, dur));
    }

    private void BtnAi_Click(object sender, RoutedEventArgs e) => DismissAiCallout();

    // ── Easter Egg: 3 cliques no logo ────────────────────────────────────────
    private void LogoIcon_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _logoClickCount++;

        // Animação de "bounce" no logo a cada clique
        AnimateLogoBounce();

        if (_logoClickCount >= 3)
        {
            _logoClickCount = 0;
            _logoClickTimer?.Stop();
            ShowEasterEgg();
            return;
        }

        // Reset automático se o usuário demorar mais de 1,5s entre cliques
        _logoClickTimer?.Stop();
        _logoClickTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        _logoClickTimer.Tick += (_, _) =>
        {
            _logoClickCount = 0;
            _logoClickTimer?.Stop();
        };
        _logoClickTimer.Start();
    }

    private void AnimateLogoBounce()
    {
        var dur = new System.Windows.Duration(TimeSpan.FromMilliseconds(140));
        var scaleDown = new System.Windows.Media.Animation.DoubleAnimation(0.75, dur);
        var scaleUp   = new System.Windows.Media.Animation.DoubleAnimation(1.0,
            new System.Windows.Duration(TimeSpan.FromMilliseconds(160)));
        scaleUp.BeginTime = TimeSpan.FromMilliseconds(140);

        var group = new System.Windows.Media.Animation.Storyboard();
        System.Windows.Media.Animation.Storyboard.SetTarget(scaleDown, LogoScale);
        System.Windows.Media.Animation.Storyboard.SetTargetProperty(scaleDown,
            new PropertyPath("ScaleX"));
        System.Windows.Media.Animation.Storyboard.SetTarget(scaleUp, LogoScale);
        System.Windows.Media.Animation.Storyboard.SetTargetProperty(scaleUp,
            new PropertyPath("ScaleX"));

        // ScaleY junto
        var scaleDownY = new System.Windows.Media.Animation.DoubleAnimation(0.75, dur);
        var scaleUpY   = new System.Windows.Media.Animation.DoubleAnimation(1.0,
            new System.Windows.Duration(TimeSpan.FromMilliseconds(160)));
        scaleUpY.BeginTime = TimeSpan.FromMilliseconds(140);
        System.Windows.Media.Animation.Storyboard.SetTarget(scaleDownY, LogoScale);
        System.Windows.Media.Animation.Storyboard.SetTargetProperty(scaleDownY,
            new PropertyPath("ScaleY"));
        System.Windows.Media.Animation.Storyboard.SetTarget(scaleUpY, LogoScale);
        System.Windows.Media.Animation.Storyboard.SetTargetProperty(scaleUpY,
            new PropertyPath("ScaleY"));

        group.Children.Add(scaleDown);
        group.Children.Add(scaleUp);
        group.Children.Add(scaleDownY);
        group.Children.Add(scaleUpY);
        group.Begin();
    }

    private void ShowEasterEgg()
    {
        // Notifica o sistema de troféus
        if (DataContext is ViewModels.MainViewModel vm)
            vm.NotifyEasterEggFound();

        // Sorteia mensagem divertida
        var rng = new Random();
        EggFunText.Text = EggMessages[rng.Next(EggMessages.Length)];

        // Abre o popup
        EasterEggPopup.IsOpen = true;

        // Animação de entrada: slide + fade
        EasterEggBorder.Opacity = 0;
        EasterEggBorder.RenderTransform = new TranslateTransform(0, -20);

        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1,
            new System.Windows.Duration(TimeSpan.FromMilliseconds(300)));
        var slideIn = new System.Windows.Media.Animation.DoubleAnimation(-20, 0,
            new System.Windows.Duration(TimeSpan.FromMilliseconds(300)));
        slideIn.EasingFunction = new System.Windows.Media.Animation.QuadraticEase
            { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };

        EasterEggBorder.BeginAnimation(OpacityProperty, fadeIn);
        ((TranslateTransform)EasterEggBorder.RenderTransform).BeginAnimation(
            TranslateTransform.YProperty, slideIn);

        // Gira o logo dentro do popup (360° em 0,8s)
        var spin = new System.Windows.Media.Animation.DoubleAnimation(0, 360,
            new System.Windows.Duration(TimeSpan.FromMilliseconds(800)));
        spin.EasingFunction = new System.Windows.Media.Animation.BackEase
            { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Amplitude = 0.4 };
        EggLogoRotate.BeginAnimation(
            System.Windows.Media.RotateTransform.AngleProperty, spin);
    }

    private void EggCloseBtn_Click(object sender, RoutedEventArgs e)
    {
        // Animação de saída
        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0,
            new System.Windows.Duration(TimeSpan.FromMilliseconds(200)));
        fadeOut.Completed += (_, _) => EasterEggPopup.IsOpen = false;
        EasterEggBorder.BeginAnimation(OpacityProperty, fadeOut);
    }
}