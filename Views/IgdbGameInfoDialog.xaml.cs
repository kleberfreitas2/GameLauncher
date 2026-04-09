using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class IgdbGameInfoDialog : Window
{
    private readonly IgdbService _service;
    private IgdbGame? _selected;

    public IgdbGame? SelectedGame        => _selected;

    public IgdbGameInfoDialog(string clientId, string clientSecret, string gameName)
    {
        InitializeComponent();
        _service       = new IgdbService(clientId, clientSecret);
        SearchBox.Text = gameName;
        Loaded += async (_, _) => await DoSearchAsync(gameName);
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) _ = DoSearchAsync(SearchBox.Text.Trim());
    }

    private void Search_Click(object sender, RoutedEventArgs e)
        => _ = DoSearchAsync(SearchBox.Text.Trim());

    private async Task DoSearchAsync(string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return;

        SetLoading(true);
        ResultsList.Visibility  = Visibility.Collapsed;
        ResultsList.ItemsSource = null;
        ClearDetail();

        var games = await _service.SearchGamesAsync(term);

        SetLoading(false);

        if (games.Count == 0)
        {
            StatusText.Text = _service.LastError is not null
                ? $"Erro: {_service.LastError}"
                : $"Nenhum resultado para '{term}'.";
            StatusText.Visibility = Visibility.Visible;
            return;
        }

        StatusText.Visibility   = Visibility.Collapsed;
        ResultsList.ItemsSource = games;
        ResultsList.Visibility  = Visibility.Visible;
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsList.SelectedItem is IgdbGame game)
            ShowDetail(game);
    }

    private void ShowDetail(IgdbGame game)
    {
        _selected = game;

        DetailPanel.Visibility = Visibility.Visible;
        EmptyDetail.Visibility = Visibility.Collapsed;

        GameTitle.Text  = game.Name;
        RatingText.Text = game.RatingText;
        YearText.Text   = game.YearText;
        GenreText.Text  = game.GenreNames;
        SummaryText.Text = !string.IsNullOrWhiteSpace(game.Summary)
            ? game.Summary
            : "Descrição não disponível.";

        ApplyInfoButton.IsEnabled = true;
        FooterText.Text           = $"IGDB ID: {game.Id}";
    }

    private void ClearDetail()
    {
        _selected                  = null;
        DetailPanel.Visibility     = Visibility.Collapsed;
        EmptyDetail.Visibility     = Visibility.Visible;
        ApplyInfoButton.IsEnabled  = false;
        FooterText.Text            = string.Empty;
    }

    private void ApplyInfo_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void SetLoading(bool loading)
    {
        LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        if (loading)
        {
            StatusText.Visibility  = Visibility.Collapsed;
            ResultsList.Visibility = Visibility.Collapsed;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _service.Dispose();
        base.OnClosed(e);
    }
}
