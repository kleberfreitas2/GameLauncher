using System.Windows;

namespace GameLauncher.Views;

public partial class ApiKeyDialog : Window
{
    public string ApiKey => KeyTextBox.Text.Trim();

    public ApiKeyDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => KeyTextBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            MessageBox.Show("A chave API não pode estar vazia.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
