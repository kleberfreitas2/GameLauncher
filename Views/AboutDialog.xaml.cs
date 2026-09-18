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
        VersionText.Text = $"Versão {AppInfo.Version}";
        Closed += (_, _) => _updateService.Dispose();
        Loaded += async (_, _) =>
        {
            var version = await _updateService.GetAvailableVersionAsync();
            if (version is not null)
                UpdateStatus.Text = $"Nova versão disponível: {version}";
            else
                UpdateStatus.Text = "Você está usando a versão mais recente.";
        };
    }

    private void GitHub_Click(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo(AppInfo.GitHubUrl) { UseShellExecute = true });
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        await _updateService.UpdateToLatestAsync(status => UpdateStatus.Text = status);
        UpdateButton.IsEnabled = true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Close();
}
