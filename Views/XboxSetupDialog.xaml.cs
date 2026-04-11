using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace GameLauncher.Views;

public partial class XboxSetupDialog : Window
{
    public string ClientId => ClientIdBox.Text.Trim();

    public XboxSetupDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => ClientIdBox.Focus();
    }

    public XboxSetupDialog(string currentClientId) : this()
    {
        ClientIdBox.Text = currentClientId;
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
            MessageBox.Show("Preencha o Application (Client) ID.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
