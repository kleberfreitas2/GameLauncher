using System.Diagnostics;
using System.IO;
using System.Windows;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class RecordingSettingsDialog : Window
{
    public RecordingSettingsDialog()
    {
        InitializeComponent();
        LoadSettings();
        CheckFfmpeg();
    }

    private void LoadSettings()
    {
        var res = SettingsService.Current.RecordingResolution;
        switch (res)
        {
            case "720p":
                Rb720.IsChecked = true;
                break;
            case "4K":
                Rb4K.IsChecked = true;
                break;
            default:
                Rb1080.IsChecked = true;
                break;
        }

        EnableToggle.IsChecked = SettingsService.Current.RecordingEnabled;
        HotkeyText.Text = SettingsService.Current.RecordingHotkey;
        OutputPathText.Text = GameRecorderService.GetOutputDirectory();
    }

    private async void CheckFfmpeg()
    {
        if (GameRecorderService.IsFfmpegAvailable())
        {
            FfmpegIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
            FfmpegIcon.Foreground = FindBrush("#00E676");
            FfmpegStatusText.Text = "FFmpeg pronto";
            FfmpegStatusText.Foreground = FindBrush("#00E676");
        }
        else
        {
            FfmpegIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Download;
            FfmpegIcon.Foreground = FindBrush("#FFD740");
            FfmpegStatusText.Text = "Baixando FFmpeg (necessário para gravar)...";
            FfmpegStatusText.Foreground = FindBrush("#FFD740");

            var ok = await GameRecorderService.EnsureFfmpegAsync(msg =>
            {
                Dispatcher.BeginInvoke(() => FfmpegStatusText.Text = msg);
            });

            if (ok)
            {
                FfmpegIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
                FfmpegIcon.Foreground = FindBrush("#00E676");
                FfmpegStatusText.Text = "FFmpeg pronto";
                FfmpegStatusText.Foreground = FindBrush("#00E676");
            }
            else
            {
                FfmpegIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.AlertCircle;
                FfmpegIcon.Foreground = FindBrush("#FF5252");
                FfmpegStatusText.Text = "Erro ao obter FFmpeg — gravação indisponível";
                FfmpegStatusText.Foreground = FindBrush("#FF5252");
            }
        }
    }

    private void Resolution_Changed(object sender, RoutedEventArgs e)
    {
        string res;
        if (Rb720.IsChecked == true) res = "720p";
        else if (Rb4K.IsChecked == true) res = "4K";
        else res = "1080p";

        SettingsService.Current.RecordingResolution = res;
        SettingsService.Save();
    }

    private void EnableToggle_Changed(object sender, RoutedEventArgs e)
    {
        SettingsService.Current.RecordingEnabled = EnableToggle.IsChecked == true;
        SettingsService.Save();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = GameRecorderService.GetOutputDirectory();
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private static System.Windows.Media.SolidColorBrush FindBrush(string hex)
    {
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        var brush = new System.Windows.Media.SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
