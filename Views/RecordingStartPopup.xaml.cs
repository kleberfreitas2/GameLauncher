using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GameLauncher.Views;

public partial class RecordingStartPopup : Window
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

    public RecordingStartPopup()
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
            SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
        };

        Loaded += (_, _) =>
        {
            StartEntryAnimation();
            StartPulseAnimation();
        };
    }

    private void StartEntryAnimation()
    {
        // Slide up + fade in
        var slideIn = new DoubleAnimation
        {
            From = Top + 30,
            To = Top,
            Duration = TimeSpan.FromMilliseconds(350),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(TopProperty, slideIn);

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(300)
        };
        BeginAnimation(OpacityProperty, fadeIn);
    }

    private void StartPulseAnimation()
    {
        var pulse = new DoubleAnimation
        {
            From = 1.0,
            To = 0.3,
            Duration = TimeSpan.FromMilliseconds(600),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        PulseDot.BeginAnimation(OpacityProperty, pulse);
    }

    public void Dismiss()
    {
        // Fade out + slide up, then close
        var fadeOut = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fadeOut);

        var slideUp = new DoubleAnimation
        {
            To = Top - 20,
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        BeginAnimation(TopProperty, slideUp);
    }
}
