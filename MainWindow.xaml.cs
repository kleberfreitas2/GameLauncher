using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        Loaded += (_, _) => CompositionTarget.Rendering += OnFpsRendering;
        Closed += (_, _) =>
        {
            CompositionTarget.Rendering -= OnFpsRendering;
            (DataContext as MainViewModel)?.Dispose();
        };
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
}