using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class AiHintOverlay : Window
{
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
    private const int GWL_EXSTYLE       = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW  = 0x00000080;
    private const int WS_EX_NOACTIVATE  = 0x08000000;

    private const double VisibleSeconds = 7;
    private readonly DispatcherTimer _dismissTimer;
    private readonly XInputService? _xinput;

    public AiHintOverlay(XInputService? xinput = null)
    {
        InitializeComponent();
        _xinput = xinput;

        SourceInitialized += (_, _) => MakePassThrough();

        Loaded += (_, _) =>
        {
            HotkeyBadge.Text = GlobalHotkeyService.AiHotkeyLabel;
            UpdateHints(xinput?.IsConnected ?? false, xinput?.ControllerName ?? "");
            PositionBottomRight();
            PlayFadeIn();
        };

        if (xinput is not null)
            xinput.ConnectionChanged += OnConnectionChanged;

        _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(VisibleSeconds) };
        _dismissTimer.Tick += (_, _) => { _dismissTimer.Stop(); PlayFadeOutAndClose(); };
        _dismissTimer.Start();

        Closed += (_, _) =>
        {
            if (_xinput is not null)
                _xinput.ConnectionChanged -= OnConnectionChanged;
        };
    }

    private void OnConnectionChanged(bool connected)
    {
        Dispatcher.BeginInvoke(() =>
            UpdateHints(connected, _xinput?.ControllerName ?? ""));
    }

    private void UpdateHints(bool gamepadConnected, string controllerName)
    {
        if (gamepadConnected)
        {
            PanelGamepad.Visibility  = Visibility.Visible;
            PanelKeyboard.Visibility = Visibility.Collapsed;

            // Adapta o botao Y conforme o controle (PlayStation usa Triangulo)
            bool isPlayStation = controllerName.Contains("DualSense", StringComparison.OrdinalIgnoreCase)
                              || controllerName.Contains("DualShock", StringComparison.OrdinalIgnoreCase);
            BadgeStart.Text = isPlayStation ? "Options" : "Start";
            BadgeY.Text     = isPlayStation ? "△" : "Y";
        }
        else
        {
            PanelGamepad.Visibility  = Visibility.Collapsed;
            PanelKeyboard.Visibility = Visibility.Visible;
        }

        UpdateLayout();
        PositionBottomRight();
    }

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        UpdateLayout();
        Left = area.Right  - ActualWidth  - 24;
        Top  = area.Bottom - ActualHeight - 24;
    }

    private void MakePassThrough()
    {
        var hwnd  = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE,
            style | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    private void PlayFadeIn()
    {
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        BeginAnimation(OpacityProperty, anim);
    }

    private void PlayFadeOutAndClose()
    {
        var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(600))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
        anim.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, anim);
    }
}
