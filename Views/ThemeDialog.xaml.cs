using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GameLauncher.Services;
using Color = System.Windows.Media.Color;

namespace GameLauncher.Views;

public partial class ThemeDialog : Window
{
    private record ThemePreset(
        string Name,
        string Accent, string Secondary,
        string Bg, string Header, string Card, string CardImg);

    private static readonly ThemePreset[] Presets =
    [
        new("Roxo Neon",     "#7C4DFF", "#00E676", "#0D0D0D", "#1A1A3A", "#2A2A4A", "#1E1E3A"),
        new("Azul Elétrico", "#1565C0", "#00BCD4", "#0A0A1E", "#122040", "#1E3255", "#162844"),
        new("Matrix",        "#00C853", "#69F0AE", "#0A160A", "#163016", "#204A20", "#183A18"),
        new("Vermelho",      "#D50000", "#FF6D00", "#1A0808", "#2A1010", "#3A1818", "#2E1212"),
        new("Rosa Cyber",    "#AD1457", "#FF4081", "#160A1C", "#221030", "#341848", "#261030"),
        new("Ártico",        "#0097A7", "#80DEEA", "#080E1E", "#102030", "#183248", "#122438"),
        new("Laranja Lava",  "#E65100", "#FFB74D", "#1A0E04", "#2A1808", "#3D2410", "#301C0C"),
        new("Ouro Imperial", "#FFB300", "#FFE082", "#12100A", "#1E1A0E", "#2E2816", "#241E10"),
        new("Cyberpunk",     "#F50057", "#EEFF41", "#0D0D14", "#1A1028", "#2A1838", "#22142E"),
        new("Steam",         "#1A9FFF", "#66BB6A", "#1B2838", "#171A21", "#2A475E", "#1B2838"),
        new("Xbox",          "#107C10", "#00C853", "#0E0E0E", "#1A1A1A", "#2D2D2D", "#1E1E1E"),
        new("PlayStation",   "#003087", "#00439C", "#0A0A18", "#0E1428", "#162040", "#101832"),
    ];

    private Border? _activeBorder;

    public ThemeDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => BuildPresets();
    }

    private void BuildPresets()
    {
        for (int i = 0; i < Presets.Length; i++)
        {
            var preset      = Presets[i];
            var row         = i / 3;
            var col         = i % 3;
            var accentColor = (Color)ColorConverter.ConvertFromString(preset.Accent);
            var isActive    = SettingsService.Current.AccentColor
                                  .Equals(preset.Accent, System.StringComparison.OrdinalIgnoreCase);

            var outer = new Border
            {
                Margin          = new Thickness(6),
                CornerRadius    = new CornerRadius(10),
                BorderThickness = new Thickness(2),
                BorderBrush     = isActive
                    ? new SolidColorBrush(accentColor)
                    : new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                Background      = new SolidColorBrush(Color.FromRgb(15, 15, 35)),
                Cursor          = Cursors.Hand,
                ClipToBounds    = false
            };

            var inner = new StackPanel
            {
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            inner.Children.Add(new Border
            {
                Width               = 38,
                Height              = 38,
                CornerRadius        = new CornerRadius(19),
                Background          = new SolidColorBrush(accentColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 10)
            });

            inner.Children.Add(new TextBlock
            {
                Text                = preset.Name,
                FontSize            = 13,
                FontWeight          = FontWeights.SemiBold,
                Foreground          = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            outer.Child = inner;

            if (isActive) _activeBorder = outer;

            Grid.SetRow(outer, row);
            Grid.SetColumn(outer, col);

            var captured = preset;
            outer.MouseLeftButtonDown += (_, _) => ApplyPreset(outer, captured, accentColor);

            ThemeGrid.Children.Add(outer);
        }
    }

    private void ApplyPreset(Border border, ThemePreset preset, Color accentColor)
    {
        if (_activeBorder is not null)
            _activeBorder.BorderBrush =
                new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));

        border.BorderBrush = new SolidColorBrush(accentColor);
        _activeBorder      = border;

        var s = SettingsService.Current;
        s.AccentColor          = preset.Accent;
        s.SecondaryAccentColor = preset.Secondary;
        s.BackgroundColor      = preset.Bg;
        s.HeaderColor          = preset.Header;
        s.CardColor            = preset.Card;
        s.CardImageColor       = preset.CardImg;

        SettingsService.Save();
        SettingsService.ApplyTheme();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
