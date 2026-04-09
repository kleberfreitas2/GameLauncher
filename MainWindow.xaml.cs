using System.Windows;
using System.Windows.Input;
using GameLauncher.ViewModels;

namespace GameLauncher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        Closed += (_, _) => (DataContext as MainViewModel)?.Dispose();
    }

    private void DetailBackdrop_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.CloseDetailCommand.Execute(null);
    }
}