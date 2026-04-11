using System.Windows;
using System.Windows.Controls;

namespace GameLauncher.Views;

public partial class HelpDialog : Window
{
    private readonly StackPanel[] _pages;

    public HelpDialog()
    {
        InitializeComponent();
        _pages = [Page0, Page1, Page2, Page3, Page4, Page5, Page6, Page7, Page8, Page9, Page10, Page11];
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList is null || _pages is null) return;

        var idx = NavList.SelectedIndex;
        for (int i = 0; i < _pages.Length; i++)
            _pages[i].Visibility = i == idx ? Visibility.Visible : Visibility.Collapsed;
    }
}
