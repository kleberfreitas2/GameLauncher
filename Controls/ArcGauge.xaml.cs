using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace GameLauncher.Controls;

public partial class ArcGauge : UserControl
{
    private double _currentValue;
    private AnimationHelper? _currentAnimation;

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(ArcGauge),
            new PropertyMetadata(0.0, OnValueChanged));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ArcGauge),
            new PropertyMetadata("", OnLabelChanged));

    public static readonly DependencyProperty SubLabelProperty =
        DependencyProperty.Register(nameof(SubLabel), typeof(string), typeof(ArcGauge),
            new PropertyMetadata("", OnSubLabelChanged));

    public static readonly DependencyProperty IsTemperatureProperty =
        DependencyProperty.Register(nameof(IsTemperature), typeof(bool), typeof(ArcGauge),
            new PropertyMetadata(false));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string SubLabel
    {
        get => (string)GetValue(SubLabelProperty);
        set => SetValue(SubLabelProperty, value);
    }

    public bool IsTemperature
    {
        get => (bool)GetValue(IsTemperatureProperty);
        set => SetValue(IsTemperatureProperty, value);
    }

    public ArcGauge()
    {
        InitializeComponent();
        UpdateArc(0);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArcGauge gauge)
            gauge.AnimateToValue((double)e.NewValue);
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArcGauge gauge)
            gauge.LabelText.Text = (string)e.NewValue;
    }

    private static void OnSubLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArcGauge gauge)
            gauge.SubText.Text = (string)e.NewValue;
    }

    private void AnimateToValue(double target)
    {
        target = Math.Clamp(target, 0, 100);
        _currentAnimation?.Stop();
        var from = _currentValue;
        _currentValue = target;

        var helper = new AnimationHelper(this, from, target);
        _currentAnimation = helper;
        helper.Start();
    }

    private void UpdateArc(double percent)
    {
        percent = Math.Clamp(percent, 0, 100);

        var suffix = IsTemperature ? "°C" : "%";
        ValueText.Text = $"{percent:F0}{suffix}";

        var brush = GetGradientBrush(percent);
        ValuePath.Stroke = brush;
        GlowPath.Stroke = brush;
        ValueText.Foreground = brush;

        const double startAngle = -220;
        const double totalSweep = 260;
        var sweepAngle = totalSweep * (percent / 100.0);

        const double cx = 40, cy = 40, r = 32;

        double startRad = startAngle * Math.PI / 180.0;
        double endRad = (startAngle + sweepAngle) * Math.PI / 180.0;

        var startPoint = new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad));
        var endPoint = new Point(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad));

        bool isLargeArc = sweepAngle > 180;

        if (sweepAngle < 0.5)
        {
            endPoint = startPoint;
            isLargeArc = false;
        }

        ArcFigure.StartPoint = startPoint;
        ValueArc.Point = endPoint;
        ValueArc.IsLargeArc = isLargeArc;

        GlowFigure.StartPoint = startPoint;
        GlowArc.Point = endPoint;
        GlowArc.IsLargeArc = isLargeArc;
    }

    private SolidColorBrush GetGradientBrush(double percent)
    {
        Color color;
        if (IsTemperature)
        {
            color = percent switch
            {
                <= 45 => Color.FromRgb(0, 200, 255),   // Cool blue
                <= 65 => LerpColor(Color.FromRgb(0, 230, 118), Color.FromRgb(255, 235, 59), (percent - 45) / 20),
                <= 80 => LerpColor(Color.FromRgb(255, 235, 59), Color.FromRgb(255, 100, 0), (percent - 65) / 15),
                _ => LerpColor(Color.FromRgb(255, 100, 0), Color.FromRgb(255, 23, 68), (percent - 80) / 20),
            };
        }
        else
        {
            color = percent switch
            {
                <= 30 => LerpColor(Color.FromRgb(0, 230, 118), Color.FromRgb(0, 230, 118), 0),
                <= 60 => LerpColor(Color.FromRgb(0, 230, 118), Color.FromRgb(255, 235, 59), (percent - 30) / 30),
                <= 85 => LerpColor(Color.FromRgb(255, 235, 59), Color.FromRgb(255, 100, 0), (percent - 60) / 25),
                _ => LerpColor(Color.FromRgb(255, 100, 0), Color.FromRgb(255, 23, 68), (percent - 85) / 15),
            };
        }
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color LerpColor(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private class AnimationHelper
    {
        private readonly ArcGauge _gauge;
        private readonly double _from;
        private readonly double _to;
        private readonly System.Windows.Threading.DispatcherTimer _timer;
        private readonly DateTime _startTime;
        private const double DurationMs = 400;

        public AnimationHelper(ArcGauge gauge, double from, double to)
        {
            _gauge = gauge;
            _from = from;
            _to = to;
            _startTime = DateTime.UtcNow;
            _timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _timer.Tick += OnTick;
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        private void OnTick(object? sender, EventArgs e)
        {
            var elapsed = (DateTime.UtcNow - _startTime).TotalMilliseconds;
            var t = Math.Min(elapsed / DurationMs, 1.0);
            t = 1 - (1 - t) * (1 - t); // ease-out quadratic
            var current = _from + (_to - _from) * t;
            _gauge.UpdateArc(current);

            if (t >= 1.0)
                _timer.Stop();
        }
    }
}
