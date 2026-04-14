using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace GameLauncher.Views;

public partial class RecordingOverlayWindow : Window
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

    private readonly DispatcherTimer _timer;
    private DateTime _startTime;

    public RecordingOverlayWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) => MakeClickThrough();

        var screen = SystemParameters.WorkArea;
        Left = screen.Right - Width - 16;
        Top = screen.Top + 50;

        _startTime = DateTime.Now;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            var elapsed = DateTime.Now - _startTime;
            TimerText.Text = elapsed.TotalHours >= 1
                ? elapsed.ToString(@"hh\:mm\:ss")
                : elapsed.ToString(@"mm\:ss");
        };

        Loaded += (_, _) =>
        {
            _timer.Start();
            StartBlinkAnimation();
        };

        Closed += (_, _) => _timer.Stop();
    }

    private void MakeClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
        SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
    }

    private void StartBlinkAnimation()
    {
        var anim = new DoubleAnimation
        {
            From = 1.0,
            To = 0.3,
            Duration = TimeSpan.FromMilliseconds(800),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        RecDot.BeginAnimation(OpacityProperty, anim);
    }
}
