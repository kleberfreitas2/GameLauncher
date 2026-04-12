using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace GameLauncher.Views;

public partial class DiscordSetupDialog : Window
{
    public string ClientId => ClientIdBox.Text.Trim();

    public DiscordSetupDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => ClientIdBox.Focus();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            MessageBox.Show("Preencha o Application ID (Client ID).", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
