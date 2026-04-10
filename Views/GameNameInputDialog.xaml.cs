using System.Windows;

namespace GameLauncher.Views;

public partial class GameNameInputDialog : Window
{
    public string GameName => NameTextBox.Text.Trim();

    public GameNameInputDialog(string defaultName)
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            NameTextBox.Text = defaultName;
            NameTextBox.SelectAll();
            NameTextBox.Focus();
        };
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(NameTextBox.Text))
            DialogResult = true;
    }

    private void Skip_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
