using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class RecordingHintOverlay : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    private readonly DispatcherTimer _dismissTimer;
    private bool _dismissed;

    public RecordingHintOverlay()
    {
        InitializeComponent();

        var hotkey = SettingsService.Current.RecordingHotkey;
        HotkeyText.Text = hotkey;
        TitleText.Text = $"Pressione {hotkey} para gravar";

        var features = new List<string> { "Tela" };
        features.Add("áudio do jogo");
        var mode = SettingsService.Current.RecordingMode;
        if (mode is "microphone" or "facecam_mic")
            features.Add("microfone");
        if (mode == "facecam_mic")
            features.Add("facecam");
        SubtitleText.Text = string.Join("  ·  ", features);

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
            SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
        };

        Loaded += (_, _) =>
        {
            var screen = SystemParameters.WorkArea;
            Left = (screen.Width - ActualWidth) / 2 + screen.Left;
            Top = screen.Top + 30;

            StartEntryAnimation();
            StartTimerBar();
            StartPulse();
        };

        _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _dismissTimer.Tick += (_, _) =>
        {
            _dismissTimer.Stop();
            Dismiss();
        };
    }

    private void StartEntryAnimation()
    {
        var slideDown = new DoubleAnimation
        {
            From = Top - 60,
            To = Top,
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(TopProperty, slideDown);

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(400)
        };
        BeginAnimation(OpacityProperty, fadeIn);

        _dismissTimer.Start();
    }

    private void StartTimerBar()
    {
        var shrink = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromSeconds(10),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        TimerBarScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
    }

    private void StartPulse()
    {
        var pulse = new DoubleAnimation
        {
            From = 1.0,
            To = 0.2,
            Duration = TimeSpan.FromMilliseconds(800),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        HintDot.BeginAnimation(OpacityProperty, pulse);
    }

    public void Dismiss()
    {
        if (_dismissed) return;
        _dismissed = true;
        _dismissTimer.Stop();

        var fadeOut = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) => { try { Close(); } catch { } };
        BeginAnimation(OpacityProperty, fadeOut);

        var slideUp = new DoubleAnimation
        {
            To = Top - 30,
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        BeginAnimation(TopProperty, slideUp);
    }
}
