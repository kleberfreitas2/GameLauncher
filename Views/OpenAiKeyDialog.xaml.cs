using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace GameLauncher.Views;

public partial class OpenAiKeyDialog : Window
{
    public string ApiKey => KeyTextBox.Text.Trim();

    public OpenAiKeyDialog()
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
        if (!ApiKey.StartsWith("sk-", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("A chave OpenAI deve começar com 'sk-'.\n" +
                "Copie a chave em: platform.openai.com/api-keys",
                "Chave inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
