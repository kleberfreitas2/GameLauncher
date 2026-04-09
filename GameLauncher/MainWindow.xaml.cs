using System.Windows;
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
}