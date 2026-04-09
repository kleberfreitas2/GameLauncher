using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace GameLauncher.Views;

public partial class IgdbSetupDialog : Window
{
    public string ClientId     => ClientIdBox.Text.Trim();
    public string ClientSecret => ClientSecretBox.Text.Trim();

    public IgdbSetupDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => ClientIdBox.Focus();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret))
        {
            MessageBox.Show("Preencha o Client ID e o Client Secret.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
