using System.Windows;
using GameLauncher.ViewModels;

namespace GameLauncher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}