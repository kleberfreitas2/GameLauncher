using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace GameLauncher.Views;

public partial class ApiKeyDialog : Window
{
    public string ApiKey => KeyTextBox.Text.Trim();

    public ApiKeyDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => KeyTextBox.Focus();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            MessageBox.Show("A chave API não pode estar vazia.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (ApiKey.All(char.IsDigit) && ApiKey.Length > 10)
        {
            MessageBox.Show("Isso parece um Steam ID, não uma API Key.\n" +
                "A API Key é um código alfanumérico obtido em:\n" +
                "steamgriddb.com/profile/preferences/api",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
