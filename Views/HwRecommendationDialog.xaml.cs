using System.Windows;
using System.Windows.Media;
using GameLauncher.Models;
using GameLauncher.Services;
using MaterialDesignThemes.Wpf;

namespace GameLauncher.Views;

public partial class HwRecommendationDialog : Window
{
    public HwRecommendationDialog(string gameName, string gpuName, string cpuName, string ramTotal)
    {
        InitializeComponent();
        SubtitleText.Text = $"Analisando: {gameName}";
        Loaded += async (_, _) => await AnalyzeAsync(gameName, gpuName, cpuName, ramTotal);
    }

    private async Task AnalyzeAsync(string gameName, string gpuName, string cpuName, string ramTotal)
    {
        // Leitura de hardware em background (WMI pode ser lenta)
        var hw = await Task.Run(() =>
            HardwareProfileService.Get(gpuName, cpuName, ramTotal));

        var rec = await Task.Run(() =>
            GraphicsRecommendationService.Recommend(gameName, hw));

        ApplyHardware(hw);
        ApplyResult(rec);
    }

    private void ApplyHardware(PcHardwareProfile hw)
    {
        // GPU
        GpuNameText.Text  = string.IsNullOrEmpty(hw.GpuName) ? "GPU não detectada" : hw.GpuName;
        GpuVramText.Text  = hw.GpuVramGb > 0 ? $"{hw.GpuVramGb} GB VRAM" : "";
        GpuScoreBar.Value = hw.GpuScore;
        GpuScoreText.Text = $"{hw.GpuScore}/100";

        // CPU
        CpuNameText.Text   = string.IsNullOrEmpty(hw.CpuName) ? "CPU não detectada" : hw.CpuName;
        CpuDetailText.Text = BuildCpuDetail(hw);

        // RAM
        RamText.Text = hw.RamGb > 0 ? hw.RamDisplay : "RAM não detectada";

        // Avisos
        UnknownHwText.Visibility = hw.IsComplete ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ApplyResult(GraphicsRecommendation rec)
    {
        // Ocultar loading, mostrar resultado
        LoadingPanel.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility  = Visibility.Visible;

        SubtitleText.Text = rec.IsGameKnown
            ? $"Resultado para: {rec.GameName}"
            : $"Estimativa geral para: {rec.GameName}";

        // Badge do preset
        var bgColor = (Color)(ColorConverter.ConvertFromString(rec.PresetColor) ?? Colors.Gray);
        PresetBadge.Background = new SolidColorBrush(Color.FromArgb(40, bgColor.R, bgColor.G, bgColor.B));
        PresetBadge.BorderBrush = new SolidColorBrush(bgColor);
        PresetBadge.BorderThickness = new Thickness(1.5);

        PresetLabel.Text      = PresetDisplayName(rec.Preset);
        PresetLabel.Foreground = new SolidColorBrush(bgColor);
        FpsLabel.Text         = rec.EstimatedFps > 0 ? $"~{rec.EstimatedFps} fps" : "";
        FpsLabel.Foreground   = new SolidColorBrush(bgColor);

        // Ícone
        if (Enum.TryParse<PackIconKind>(rec.PresetIcon, true, out var iconKind))
            PresetIcon.Kind = iconKind;
        PresetIcon.Foreground = new SolidColorBrush(bgColor);

        // Mensagem
        MainMessageText.Text       = rec.ShortMessage;
        MainMessageText.Foreground = new SolidColorBrush(bgColor);

        // Detalhes
        DetailsList.ItemsSource = rec.Details;

        // Badge jogo desconhecido
        UnknownGameBadge.Visibility = rec.IsGameKnown ? Visibility.Collapsed : Visibility.Visible;
    }

    private static string BuildCpuDetail(PcHardwareProfile hw)
    {
        var parts = new List<string>();
        if (hw.CpuCores > 0) parts.Add($"{hw.CpuCores} núcleos");
        if (hw.CpuClockGhz > 0) parts.Add($"{hw.CpuClockGhz:F1} GHz");
        return string.Join(" / ", parts);
    }

    private static string PresetDisplayName(GraphicsPreset p) => p switch
    {
        GraphicsPreset.NãoRoda => "NÃO RODA",
        GraphicsPreset.Baixo   => "BAIXO",
        GraphicsPreset.Médio   => "MÉDIO",
        GraphicsPreset.Alto    => "ALTO",
        GraphicsPreset.Ultra   => "ULTRA",
        _                      => "?"
    };

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => DragMove();
}
