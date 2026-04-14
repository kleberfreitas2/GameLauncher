using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class RecordingSettingsDialog : Window
{
    private static readonly SolidColorBrush SelectedBrush;
    private static readonly SolidColorBrush TransparentBrush;

    static RecordingSettingsDialog()
    {
        SelectedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x17, 0x44));
        SelectedBrush.Freeze();
        TransparentBrush = new SolidColorBrush(Colors.Transparent);
        TransparentBrush.Freeze();
    }

    public RecordingSettingsDialog()
    {
        InitializeComponent();
        LoadSettings();
        LoadDevices();
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

        switch (SettingsService.Current.RecordingMode)
        {
            case "microphone":
                ModeMic.IsChecked = true;
                break;
            case "facecam_mic":
                ModeFacecam.IsChecked = true;
                break;
            default:
                ModeScreen.IsChecked = true;
                break;
        }

        UpdateFacecamPositionVisual(SettingsService.Current.FacecamPosition);
        UpdateSectionsVisibility();
    }

    private void LoadDevices()
    {
        Task.Run(() =>
        {
            var videoDevices = GameRecorderService.ListVideoDevices();
            var audioDevices = GameRecorderService.ListAudioDevices();

            Dispatcher.BeginInvoke(() =>
            {
                WebcamCombo.Items.Clear();
                foreach (var d in videoDevices)
                    WebcamCombo.Items.Add(d);

                var savedCam = SettingsService.Current.FacecamDevice;
                if (!string.IsNullOrEmpty(savedCam) && videoDevices.Contains(savedCam))
                    WebcamCombo.SelectedItem = savedCam;
                else if (videoDevices.Count > 0)
                    WebcamCombo.SelectedIndex = 0;

                MicCombo.Items.Clear();
                foreach (var d in audioDevices)
                    MicCombo.Items.Add(d);

                var savedMic = SettingsService.Current.MicrophoneDevice;
                if (!string.IsNullOrEmpty(savedMic) && audioDevices.Contains(savedMic))
                    MicCombo.SelectedItem = savedMic;
                else if (audioDevices.Count > 0)
                    MicCombo.SelectedIndex = 0;
            });
        });
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
                LoadDevices();
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

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        string mode;
        if (ModeMic.IsChecked == true) mode = "microphone";
        else if (ModeFacecam.IsChecked == true) mode = "facecam_mic";
        else mode = "screen_only";

        SettingsService.Current.RecordingMode = mode;
        SettingsService.Save();
        UpdateSectionsVisibility();
    }

    private void UpdateSectionsVisibility()
    {
        if (FacecamSection is null || DeviceSection is null || WebcamRow is null) return;

        bool isFacecam = ModeFacecam.IsChecked == true;
        bool needsMic = ModeMic.IsChecked == true || isFacecam;

        FacecamSection.Visibility = isFacecam ? Visibility.Visible : Visibility.Collapsed;
        DeviceSection.Visibility = needsMic ? Visibility.Visible : Visibility.Collapsed;
        WebcamRow.Visibility = isFacecam ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FacecamPos_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Border border && border.Tag is string pos)
        {
            SettingsService.Current.FacecamPosition = pos;
            SettingsService.Save();
            UpdateFacecamPositionVisual(pos);
        }
    }

    private void UpdateFacecamPositionVisual(string position)
    {
        PosTopLeft.BorderBrush = TransparentBrush;
        PosTopRight.BorderBrush = TransparentBrush;
        PosBottomLeft.BorderBrush = TransparentBrush;
        PosBottomRight.BorderBrush = TransparentBrush;

        var target = position switch
        {
            "top_left" => PosTopLeft,
            "bottom_left" => PosBottomLeft,
            "bottom_right" => PosBottomRight,
            _ => PosTopRight
        };
        target.BorderBrush = SelectedBrush;
    }

    private void WebcamCombo_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (WebcamCombo.SelectedItem is string device)
        {
            SettingsService.Current.FacecamDevice = device;
            SettingsService.Save();
        }
    }

    private void MicCombo_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (MicCombo.SelectedItem is string device)
        {
            SettingsService.Current.MicrophoneDevice = device;
            SettingsService.Save();
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

    private static SolidColorBrush FindBrush(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
