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
    private readonly bool _viaGamepad;
    private readonly XInputService? _xinput;
    private bool _keyboardVisible;

    public AiAssistantDialog(AiProvider provider, string apiKey,
                             string? gameName = null, bool viaGamepad = false,
                             XInputService? xinput = null)
    {
        InitializeComponent();
        _gameName   = gameName;
        _viaGamepad = viaGamepad;
        _xinput     = xinput;
        _ai = new AiAssistantService(provider, apiKey, gameName);

        TxtGameContext.Text = gameName is not null
            ? $"Jogando: {gameName}"
            : "Assistente Gamer";

        AddAssistantMessage(gameName is not null
            ? $"Ola! Estou pronto para te ajudar com **{gameName}**. Pode perguntar sobre dicas, puzzles, builds, segredos ou qualquer coisa do jogo!"
            : "Ola! Sou o GLauncher AI, seu assistente gamer. Pode perguntar sobre qualquer jogo — dicas, analises, recomendacoes, builds e muito mais!");

        Loaded += (_, _) =>
        {
            InputBox.Focus();
            if (_viaGamepad || xinput?.IsConnected == true)
            {
                ShowGamepadHints(xinput?.ControllerName ?? "");
                if (_viaGamepad)
                {
                    VirtualKeyboardService.Show();
                    _keyboardVisible = true;
                    IconKeyboard.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
                }
            }
            else
            {
                ShowKeyboardHints();
            }
        };

        Closed += (_, _) =>
        {
            _cts.Cancel();
            if (_xinput is not null)
                _xinput.ButtonPressed    -= OnGamepadButton;
            if (_xinput is not null)
                _xinput.ConnectionChanged -= OnConnectionChanged;
            if (_keyboardVisible)
                VirtualKeyboardService.Hide();
        };

        if (xinput is not null)
        {
            xinput.ButtonPressed     += OnGamepadButton;
            xinput.ConnectionChanged += OnConnectionChanged;
        }
    }

    // -- Hints dinâmicos ----------------------------------------------------

    private void OnConnectionChanged(bool connected)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (connected) ShowGamepadHints(_xinput?.ControllerName ?? "");
            else           ShowKeyboardHints();
        });
    }

    private void ShowGamepadHints(string controllerName)
    {
        HintsGamepad.Visibility  = Visibility.Visible;
        HintsKeyboard.Visibility = Visibility.Collapsed;

        bool isPS = controllerName.Contains("DualSense", StringComparison.OrdinalIgnoreCase)
                 || controllerName.Contains("DualShock", StringComparison.OrdinalIgnoreCase);
        HintBadgeA.Text = isPS ? "Cruz"    : "A";
        HintBadgeB.Text = isPS ? "Circulo" : "B";
        HintBadgeX.Text = isPS ? "Quad"    : "X";
        HintBadgeY.Text = isPS ? "Tri"     : "Y";
    }

    private void ShowKeyboardHints()
    {
        HintsGamepad.Visibility  = Visibility.Collapsed;
        HintsKeyboard.Visibility = Visibility.Visible;
    }

    // -- Navegação por controle ---------------------------------------------

    private void OnGamepadButton(GamepadButton btn)
    {
        Dispatcher.BeginInvoke(() =>
        {
            switch (btn)
            {
                case GamepadButton.A:            // Enviar
                    _ = SendMessageAsync();
                    break;
                case GamepadButton.B:            // Fechar
                    Close();
                    break;
                case GamepadButton.X:            // Toggle teclado virtual
                    ToggleVirtualKeyboard();
                    break;
                case GamepadButton.Y:            // Screenshot
                    Screenshot_Click(this, new RoutedEventArgs());
                    break;
                case GamepadButton.DPadUp:       // Scroll para cima
                    MessagesScroll.ScrollToVerticalOffset(
                        MessagesScroll.VerticalOffset - 80);
                    break;
                case GamepadButton.DPadDown:     // Scroll para baixo
                    MessagesScroll.ScrollToVerticalOffset(
                        MessagesScroll.VerticalOffset + 80);
                    break;
            }
        });
    }

    // -- Toggle teclado virtual ---------------------------------------------

    private void ToggleVirtualKeyboard()
    {
        _keyboardVisible = !_keyboardVisible;
        if (_keyboardVisible)
        {
            VirtualKeyboardService.Show();
            IconKeyboard.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
        }
        else
        {
            VirtualKeyboardService.Hide();
            IconKeyboard.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x88, 0x99, 0xBB));
        }
    }

    private void ToggleKeyboard_Click(object sender, RoutedEventArgs e)
        => ToggleVirtualKeyboard();

    // -- UI handlers --------------------------------------------------------

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
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void Send_Click(object sender, RoutedEventArgs e)
        => _ = SendMessageAsync();

    private void Screenshot_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title     = "Selecionar Screenshot",
            Filter    = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
            Multiselect = false
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            _pendingImageBase64 = Convert.ToBase64String(File.ReadAllBytes(dlg.FileName));
            BtnScreenshot.Opacity   = 1.0;
            BtnScreenshot.ToolTip   = $"Screenshot: {System.IO.Path.GetFileName(dlg.FileName)} v";
            InputBox.Text = "Analise essa screenshot e me de dicas.";
            InputBox.Focus();
        }
        catch
        {
            MessageBox.Show("Erro ao carregar a imagem.", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // -- Envio de mensagem --------------------------------------------------

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
        BtnScreenshot.ToolTip = "Screenshot (Y)";

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
            assistantMsg.Text = $"Erro: {ex.Message}";
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
    }

    private void ScrollToBottom()
    {
        Dispatcher.BeginInvoke(() => MessagesScroll.ScrollToEnd(),
            System.Windows.Threading.DispatcherPriority.Background);
    }
}
