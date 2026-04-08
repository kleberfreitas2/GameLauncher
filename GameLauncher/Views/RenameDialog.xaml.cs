using System.Windows;

namespace GameLauncher.Views;

public partial class RenameDialog : Window
{
    public string NewName => NameTextBox.Text.Trim();

    public RenameDialog(string currentName)
    {
        InitializeComponent();
        Loaded += (_, _) => { NameTextBox.Text = currentName; NameTextBox.SelectAll(); NameTextBox.Focus(); };
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(NameTextBox.Text))
            DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
