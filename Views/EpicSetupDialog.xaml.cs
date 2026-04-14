using System.Windows;
using Microsoft.Win32;

namespace GameLauncher.Views;

public partial class EpicSetupDialog : Window
{
    public string EpicPath => EpicPathBox.Text.Trim();

    public EpicSetupDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => EpicPathBox.Focus();
    }

    public EpicSetupDialog(string currentPath) : this()
    {
        EpicPathBox.Text = currentPath;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Selecione a pasta da Epic Games"
        };

        if (dialog.ShowDialog() == true)
        {
            EpicPathBox.Text = dialog.FolderName;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EpicPath))
        {
            MessageBox.Show("Preencha o caminho da pasta Epic Games.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!System.IO.Directory.Exists(EpicPath))
        {
            MessageBox.Show("A pasta informada não existe.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
