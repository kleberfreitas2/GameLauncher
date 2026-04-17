using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using GameLauncher.Models;

namespace GameLauncher.Views;

/// <summary>Notificação toast exibida ao desbloquear um troféu.</summary>
public class TrophyToastWindow : Window
{
    private const double DisplaySeconds = 9.5;

    private readonly TranslateTransform _slide = new(380, 0);
    private readonly DropShadowEffect   _outerGlow;
    private readonly Rectangle         _progressBar;
    private readonly double             _cardWidth = 360;
    private readonly Action?            _onClosed;

    public TrophyToastWindow(Trophy trophy, Action? onClosed = null)
    {
        WindowStyle        = WindowStyle.None;
        AllowsTransparency = true;
        Background         = Brushes.Transparent;
        ShowInTaskbar      = false;
        Topmost            = true;
        ResizeMode         = ResizeMode.NoResize;
        Width              = _cardWidth;
        SizeToContent      = SizeToContent.Height;

        var rarity     = (Color)ColorConverter.ConvertFromString(trophy.RarityColor);
        var rarityGlow = (Color)ColorConverter.ConvertFromString(trophy.RarityGlow);

        _outerGlow = new DropShadowEffect
        {
            Color = rarity, BlurRadius = 0, ShadowDepth = 0, Opacity = 0
        };

        // ── Grid raiz ───────────────────────────────────────────────────────
        var root = new Grid();

        // ── Card ────────────────────────────────────────────────────────────
        var card = new Border
        {
            CornerRadius    = new CornerRadius(16),
            BorderThickness = new Thickness(1.5),
            BorderBrush     = new LinearGradientBrush(rarity, rarityGlow, new Point(0, 0), new Point(1, 1)),
            Background      = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17)),
            Effect          = _outerGlow,
            RenderTransform = _slide
        };
        root.Children.Add(card);

        // ── Inner grid: conteúdo + barra de progresso ───────────────────────
        var innerGrid = new Grid();
        innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        innerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });

        // Linha de conteúdo
        var contentGrid = new Grid { Margin = new Thickness(14, 12, 14, 12) };
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(contentGrid, 0);

        // Ícone circular
        var iconBorder = new Border
        {
            Width             = 56,
            Height            = 56,
            CornerRadius      = new CornerRadius(28),
            Margin            = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background        = new RadialGradientBrush(
                Color.FromArgb(0x55, rarity.R, rarity.G, rarity.B),
                Color.FromArgb(0x11, rarity.R, rarity.G, rarity.B)),
            Effect = new DropShadowEffect { Color = rarity, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.9 },
            Child  = new TextBlock
            {
                Text                = trophy.Icon,
                FontSize            = 26,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(iconBorder, 0);
        contentGrid.Children.Add(iconBorder);

        // Textos
        var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        var topRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
        topRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding      = new Thickness(6, 2, 6, 2),
            Margin       = new Thickness(0, 0, 6, 0),
            Background   = new SolidColorBrush(Color.FromArgb(0x33, rarity.R, rarity.G, rarity.B)),
            Child        = new TextBlock
            {
                Text       = trophy.RarityLabel,
                FontSize   = 10,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = new SolidColorBrush(rarity)
            }
        });
        topRow.Children.Add(new TextBlock
        {
            Text              = $"+{trophy.Gamerscore}G",
            FontSize          = 10,
            FontWeight        = FontWeights.Bold,
            Foreground        = new SolidColorBrush(Color.FromRgb(0xAA, 0xBB, 0xCC)),
            FontFamily        = new FontFamily("Segoe UI"),
            VerticalAlignment = VerticalAlignment.Center
        });
        textPanel.Children.Add(topRow);

        textPanel.Children.Add(new TextBlock
        {
            Text       = "Troféu Desbloqueado!",
            FontSize   = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x77, 0x88)),
            FontFamily = new FontFamily("Segoe UI"),
            Margin     = new Thickness(0, 0, 0, 2)
        });
        textPanel.Children.Add(new TextBlock
        {
            Text         = trophy.Name,
            FontSize     = 14,
            FontWeight   = FontWeights.Bold,
            Foreground   = Brushes.White,
            FontFamily   = new FontFamily("Segoe UI"),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        textPanel.Children.Add(new TextBlock
        {
            Text         = trophy.Description,
            FontSize     = 10,
            Foreground   = new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA)),
            FontFamily   = new FontFamily("Segoe UI"),
            TextWrapping = TextWrapping.Wrap,
            MaxHeight    = 32
        });

        Grid.SetColumn(textPanel, 1);
        contentGrid.Children.Add(textPanel);
        innerGrid.Children.Add(contentGrid);

        // Barra de progresso
        _progressBar = new Rectangle
        {
            Height              = 4,
            Width               = _cardWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment   = VerticalAlignment.Bottom,
            RadiusX             = 2,
            RadiusY             = 2,
            Fill                = new LinearGradientBrush(rarity, rarityGlow, 0)
        };
        Grid.SetRow(_progressBar, 1);
        innerGrid.Children.Add(_progressBar);

        card.Child = innerGrid;
        Content    = root;

        _onClosed = onClosed;

        // Posiciona no canto inferior direito após render
        var wa = SystemParameters.WorkArea;
        Loaded += (_, _) =>
        {
            Left = wa.Right  - ActualWidth  - 16;
            Top  = wa.Bottom - ActualHeight - 16;
            BeginEntrance();
        };
    }

    private void BeginEntrance()
    {
        var dur300  = new Duration(TimeSpan.FromMilliseconds(300));
        var dur4500 = new Duration(TimeSpan.FromMilliseconds(DisplaySeconds * 1000));

        var slideIn = new DoubleAnimation(380, 0, dur300)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        _slide.BeginAnimation(TranslateTransform.XProperty, slideIn);

        var glowIn = new DoubleAnimation(0, 0.5, dur300);
        _outerGlow.BeginAnimation(DropShadowEffect.OpacityProperty, glowIn);

        var barAnim = new DoubleAnimation(_cardWidth, 0, dur4500)
        {
            BeginTime = TimeSpan.FromMilliseconds(300)
        };
        barAnim.Completed += (_, _) => BeginExit();
        _progressBar.BeginAnimation(WidthProperty, barAnim);
    }

    private void BeginExit()
    {
        var dur = new Duration(TimeSpan.FromMilliseconds(250));
        var slideOut = new DoubleAnimation(0, 380, dur)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        slideOut.Completed += (_, _) => { Close(); _onClosed?.Invoke(); };
        _slide.BeginAnimation(TranslateTransform.XProperty, slideOut);
    }
}
