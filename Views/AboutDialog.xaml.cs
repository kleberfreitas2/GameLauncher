using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class AboutDialog : Window
{
    private readonly UpdateService _updateService = new();

    public AboutDialog()
    {
        InitializeComponent();
        VersionText.Text = $"Vers?o {AppInfo.Version}";
        Closed += (_, _) => _updateService.Dispose();
        Loaded += async (_, _) =>
        {
            var version = await _updateService.GetAvailableVersionAsync();
            if (version is not null)
                UpdateStatus.Text = $"Nova vers?o para baixar: {version}";
            else if (_updateService.LastError is not null)
                UpdateStatus.Text = "N?o foi poss?vel verificar novas vers?es.";
            else
                UpdateStatus.Text = "Voc? est? usando a vers?o mais recente.";
        };
    }

    private void GitHub_Click(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo(AppInfo.GitHubUrl) { UseShellExecute = true });
    }

    private void Update_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(AppInfo.ReleasesUrl)
        {
            UseShellExecute = true
        });
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Close();
}
