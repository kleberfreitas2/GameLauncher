using System.Windows;
using GameLauncher.Models;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class EpicProfileDialog : Window
{
    public bool LogoutRequested { get; private set; }
    public bool ImportGamesRequested { get; private set; }

    public EpicProfileDialog(EpicProfile profile, int importedCount = 0, int availableCount = 0)
    {
        InitializeComponent();

        DisplayNameText.Text = profile.DisplayName;
        TotalGamesText.Text = profile.InstalledGamesCount.ToString("N0");
        ImportedCountText.Text = importedCount.ToString("N0");
        AvailableCountText.Text = availableCount.ToString("N0");
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        LogoutRequested = true;
        DialogResult = true;
    }

    private void ImportGames_Click(object sender, RoutedEventArgs e)
    {
        ImportGamesRequested = true;
        DialogResult = true;
    }

    public void HandleGamepadInput(GamepadButton button)
    {
        switch (button)
        {
            case GamepadButton.B:
            case GamepadButton.Back:
                DialogResult = false;
                break;
        }
    }
}
