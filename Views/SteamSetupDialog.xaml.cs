using System.Windows;

namespace GameLauncher.Views;

public partial class SteamSetupDialog : Window
{
    public string SteamIdOrVanity => SteamIdBox.Text.Trim();

    public SteamSetupDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => SteamIdBox.Focus();
    }

    public SteamSetupDialog(string currentSteamId) : this()
    {
        SteamIdBox.Text = currentSteamId;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SteamIdOrVanity))
        {
            MessageBox.Show("Preencha o Steam ID, nome personalizado ou URL do perfil.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
