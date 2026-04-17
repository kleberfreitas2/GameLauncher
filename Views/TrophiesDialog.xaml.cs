using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GameLauncher.Models;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class TrophiesDialog : Window
{
    private readonly TrophyService _service;
    private string _currentFilter = "All";

    public TrophiesDialog(TrophyService service)
    {
        InitializeComponent();
        _service = service;
        Loaded += (_, _) => Initialize();
        MouseDown += (_, e) => { if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove(); };
    }

    private void Initialize()
    {
        UpdateSummary();
        ApplyFilter("All");
        AnimateProgressBar();
    }

    private void UpdateSummary()
    {
        SummaryText.Text   = _service.ProgressSummary;
        ProgressLabel.Text = $"{_service.TrophyCount} de {_service.TotalTrophies} troféus desbloqueados";
        var pct = _service.TotalGamerscore > 0
            ? (double)_service.EarnedGamerscore / _service.TotalGamerscore
            : 0;
        PercentLabel.Text  = $"{pct:P0}";
    }

    private void AnimateProgressBar()
    {
        var pct = _service.TotalGamerscore > 0
            ? (double)_service.EarnedGamerscore / _service.TotalGamerscore
            : 0;
        // O track tem a mesma largura do painel (740 - 56 de margens)
        var trackWidth = ActualWidth > 0 ? ActualWidth - 56 : 684;
        var targetWidth = trackWidth * pct;
        var anim = new DoubleAnimation(0, targetWidth,
            new Duration(TimeSpan.FromMilliseconds(900)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        ProgressFill.BeginAnimation(WidthProperty, anim);
    }

    private double TrophiesDialog_Width() => ActualWidth > 0 ? ActualWidth : 740;

    private void ApplyFilter(string filter)
    {
        _currentFilter = filter;
        UpdateFilterButtons();

        TrophyList.Items.Clear();

        var trophies = _service.All.Where(t => filter switch
        {
            "Bronze"   => t.Rarity == TrophyRarity.Bronze,
            "Silver"   => t.Rarity == TrophyRarity.Silver,
            "Gold"     => t.Rarity == TrophyRarity.Gold,
            "Platinum" => t.Rarity == TrophyRarity.Platinum,
            "Unlocked" => t.IsUnlocked,
            _          => true
        }).OrderBy(t => t.Rarity).ThenBy(t => t.IsUnlocked ? 0 : 1);

        foreach (var trophy in trophies)
            TrophyList.Items.Add(BuildTrophyCard(trophy));
    }

    private void UpdateFilterButtons()
    {
        var buttons = new[]
        {
            (FilterAll,      "All"),
            (FilterBronze,   "Bronze"),
            (FilterSilver,   "Silver"),
            (FilterGold,     "Gold"),
            (FilterPlatinum, "Platinum"),
            (FilterUnlocked, "Unlocked"),
        };

        foreach (var (btn, tag) in buttons)
        {
            bool active = tag == _currentFilter;
            btn.Background = active
                ? new SolidColorBrush(Color.FromRgb(0x26, 0x1A, 0x00))
                : new SolidColorBrush(Color.FromRgb(0x1A, 0x22, 0x33));
            btn.Foreground = active
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00))
                : new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA));
        }
    }

    private static Border BuildTrophyCard(Trophy trophy)
    {
        var rarity    = (Color)ColorConverter.ConvertFromString(trophy.RarityColor);
        var rarityGlow = (Color)ColorConverter.ConvertFromString(trophy.RarityGlow);
        bool locked   = !trophy.IsUnlocked;

        var card = new Border
        {
            Width        = 200,
            Height       = 200,
            Margin       = new Thickness(6),
            CornerRadius = new CornerRadius(16),
            ClipToBounds = true,
            Cursor       = System.Windows.Input.Cursors.Hand,
            Background   = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x26)),
            Opacity      = locked ? 0.45 : 1.0,
        };

        if (!locked)
        {
            card.BorderThickness = new Thickness(1.5);
            card.BorderBrush = new LinearGradientBrush(rarity,
                rarityGlow, new Point(0, 0), new Point(1, 1));
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color       = rarity,
                BlurRadius  = 18,
                ShadowDepth = 0,
                Opacity     = 0.4
            };
        }

        var inner = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Margin              = new Thickness(12)
        };

        // Ícone grande
        var iconBorder = new Border
        {
            Width        = 64,
            Height       = 64,
            CornerRadius = new CornerRadius(32),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin       = new Thickness(0, 0, 0, 8),
            Background   = locked
                ? new SolidColorBrush(Color.FromRgb(0x1A, 0x22, 0x33))
                : new RadialGradientBrush(
                    Color.FromArgb(0x55, rarity.R, rarity.G, rarity.B),
                    Color.FromArgb(0x11, rarity.R, rarity.G, rarity.B)),
        };
        if (!locked)
        {
            iconBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = rarity, BlurRadius = 20, ShadowDepth = 0, Opacity = 0.7
            };
        }
        iconBorder.Child = new TextBlock
        {
            Text = locked ? "🔒" : trophy.Icon,
            FontSize = 30,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };
        inner.Children.Add(iconBorder);

        // Badge de raridade
        var rarityBadge = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding      = new Thickness(8, 3, 8, 3),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin       = new Thickness(0, 0, 0, 6),
            Background   = new SolidColorBrush(Color.FromArgb(
                locked ? (byte)0x22 : (byte)0x33, rarity.R, rarity.G, rarity.B))
        };
        rarityBadge.Child = new TextBlock
        {
            Text = trophy.RarityLabel,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = new SolidColorBrush(locked
                ? Color.FromRgb(0x44, 0x55, 0x66)
                : rarity)
        };
        inner.Children.Add(rarityBadge);

        // Nome
        inner.Children.Add(new TextBlock
        {
            Text = locked ? "???" : trophy.Name,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = new SolidColorBrush(locked
                ? Color.FromRgb(0x44, 0x55, 0x66)
                : Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping  = TextWrapping.Wrap,
            MaxWidth      = 176
        });

        // Gamerscore
        inner.Children.Add(new TextBlock
        {
            Text = $"+{trophy.Gamerscore}G",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(locked
                ? Color.FromRgb(0x33, 0x44, 0x55)
                : rarity),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        });

        // Data de desbloqueio (se obtido)
        if (!locked && trophy.UnlockedAt.HasValue)
        {
            inner.Children.Add(new TextBlock
            {
                Text = trophy.UnlockedAt.Value.ToString("dd/MM/yy"),
                FontSize = 9,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x55, 0x66)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

        card.Child = inner;

        // Tooltip com Descrição
        var ttContent = new StackPanel { MaxWidth = 240 };
        ttContent.Children.Add(new TextBlock
        {
            Text = locked ? "Troféu ainda não obtido" : trophy.Name,
            FontSize = 12, FontWeight = FontWeights.Bold,
            Foreground = Brushes.White, FontFamily = new FontFamily("Segoe UI"),
            TextWrapping = TextWrapping.Wrap
        });
        if (!locked)
        {
            ttContent.Children.Add(new TextBlock
            {
                Text = trophy.Description,
                FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA)),
                FontFamily = new FontFamily("Segoe UI"), TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }
        ToolTipService.SetToolTip(card, new ToolTip
        {
            Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x35, 0x50)),
            Content = ttContent,
            HasDropShadow = true
        });

        return card;
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
            ApplyFilter(tag);
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Rola o painel de troféus via analógico direito do controle.</summary>
    public void ScrollBy(double value)
    {
        TrophyScroll.ScrollToVerticalOffset(
            TrophyScroll.VerticalOffset - value * 60);
    }
}
