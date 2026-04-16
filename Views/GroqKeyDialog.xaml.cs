using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace GameLauncher.Views;

public partial class GroqKeyDialog : Window
{
    public string ApiKey => KeyTextBox.Text.Trim();

    public GroqKeyDialog()
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
        if (!ApiKey.StartsWith("gsk_", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("A chave Groq deve começar com 'gsk_'.\n" +
                "Obtenha sua chave grátis em: console.groq.com/keys",
                "Chave inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
