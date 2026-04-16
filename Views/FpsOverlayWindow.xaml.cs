using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class FpsOverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private static readonly SolidColorBrush FpsGreen;
    private static readonly SolidColorBrush FpsAmber;
    private static readonly SolidColorBrush FpsRed;

    static FpsOverlayWindow()
    {
        FpsGreen = new SolidColorBrush(Color.FromRgb(0, 230, 118));
        FpsGreen.Freeze();
        FpsAmber = new SolidColorBrush(Color.FromRgb(255, 215, 64));
        FpsAmber.Freeze();
        FpsRed = new SolidColorBrush(Color.FromRgb(255, 82, 82));
        FpsRed.Freeze();
    }

    private readonly DispatcherTimer _timer;
    private int _frameCount;
    private TimeSpan _lastTime;

    public FpsOverlayWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) => MakeClickThrough();

        var screen = SystemParameters.WorkArea;
        Left = screen.Right - Width - 16;
        Top = screen.Top + 16;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;

        Loaded += (_, _) =>
        {
            CompositionTarget.Rendering += OnRendering;
            _timer.Start();
        };

        Closed += (_, _) =>
        {
            CompositionTarget.Rendering -= OnRendering;
            _timer.Stop();
        };
    }

    private void MakeClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        _frameCount++;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var now = TimeSpan.FromTicks(Environment.TickCount64 * TimeSpan.TicksPerMillisecond);

        if (_lastTime == TimeSpan.Zero)
        {
            _lastTime = now;
            _frameCount = 0;
            return;
        }

        var elapsed = (now - _lastTime).TotalSeconds;
        if (elapsed > 0)
        {
            var fps = (int)Math.Round(_frameCount / elapsed);
            FpsText.Text = fps.ToString();
            FpsText.Foreground = fps switch
            {
                >= 50 => FpsGreen,
                >= 30 => FpsAmber,
                _ => FpsRed
            };
        }

        _frameCount = 0;
        _lastTime = now;
    }
}
