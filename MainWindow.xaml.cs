using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private readonly GlobalHotkeyService _globalHotkey = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

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

        Loaded += (_, _) =>
        {
            CompositionTarget.Rendering += OnFpsRendering;

            _globalHotkey.RecordHotkeyPressed += () =>
            {
                if (DataContext is MainViewModel vm)
                    Dispatcher.BeginInvoke(() => vm.HandleRecordingHotkey());
            };
            _globalHotkey.Register();
        };
        Closed += (_, _) =>
        {
            CompositionTarget.Rendering -= OnFpsRendering;
            _globalHotkey.Dispose();
            (DataContext as MainViewModel)?.Dispose();
        };
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
