using System.IO;
using System.Windows;
using System.Windows.Input;
using GameLauncher.Models;
using GameLauncher.Services;
using Microsoft.Win32;

namespace GameLauncher.Views;

public partial class AiAssistantDialog : Window
{
    private readonly AiAssistantService _ai;
    private readonly string? _gameName;
    private bool _isBusy;
    private string? _pendingImageBase64;
    private CancellationTokenSource _cts = new();

    public AiAssistantDialog(AiProvider provider, string apiKey, string? gameName = null)
    {
        InitializeComponent();
        _gameName = gameName;
        _ai = new AiAssistantService(provider, apiKey, gameName);

        TxtGameContext.Text = gameName is not null
            ? $"Jogando: {gameName}"
            : "Assistente Gamer";

        AddAssistantMessage(gameName is not null
            ? $"Olá! Estou pronto para ajudar com **{gameName}**. Qual é a sua dúvida? 🎮"
            : "Olá! Sou o GLauncher AI. Pergunte qualquer coisa sobre jogos! 🎮");

        Loaded += (_, _) => InputBox.Focus();
        Closed += (_, _) => _cts.Cancel();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => DragMove();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            _ = SendMessageAsync();
        }
    }

    private void Send_Click(object sender, RoutedEventArgs e)
        => _ = SendMessageAsync();

    private void Screenshot_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Selecionar Screenshot",
            Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
            Multiselect = false
        };

        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var bytes = File.ReadAllBytes(dlg.FileName);
            _pendingImageBase64 = Convert.ToBase64String(bytes);
            BtnScreenshot.Opacity = 1.0;
            BtnScreenshot.ToolTip = $"Screenshot: {Path.GetFileName(dlg.FileName)} ✓";
            InputBox.Text = "Analise essa screenshot e me dê dicas.";
            InputBox.Focus();
        }
        catch
        {
            MessageBox.Show("Erro ao carregar a imagem.", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task SendMessageAsync()
    {
        var text = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(text) || _isBusy) return;

        _isBusy = true;
        BtnSend.IsEnabled = false;
        InputBox.Clear();

        var imageBase64 = _pendingImageBase64;
        _pendingImageBase64 = null;
        BtnScreenshot.Opacity = 0.6;
        BtnScreenshot.ToolTip = "Analisar Screenshot";

        AddUserMessage(text);

        TypingIndicator.Visibility = Visibility.Visible;
        ScrollToBottom();

        var assistantMsg = new AiChatMessage { Role = ChatRole.Assistant, Text = "" };
        MessagesList.Items.Add(assistantMsg);

        _cts = new CancellationTokenSource();
        try
        {
            await foreach (var token in _ai.SendAsync(text, imageBase64, _cts.Token))
            {
                assistantMsg.Text += token;
                ScrollToBottom();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            assistantMsg.Text = $"❌ Erro: {ex.Message}";
        }
        finally
        {
            TypingIndicator.Visibility = Visibility.Collapsed;
            _isBusy = false;
            BtnSend.IsEnabled = true;
            InputBox.Focus();
            ScrollToBottom();
        }
    }

    private void AddUserMessage(string text)
    {
        MessagesList.Items.Add(new AiChatMessage { Role = ChatRole.User, Text = text });
        ScrollToBottom();
    }

    private void AddAssistantMessage(string text)
    {
        MessagesList.Items.Add(new AiChatMessage { Role = ChatRole.Assistant, Text = text });
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        Dispatcher.BeginInvoke(() =>
        {
            MessagesScroll.ScrollToEnd();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }
}
