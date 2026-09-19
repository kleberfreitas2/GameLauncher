using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Diagnostics;
using System.IO;
using GameLauncher.Models;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class AiAssistantDialog : Window
{
    private readonly AiAssistantService _ai;
    private readonly string? _gameName;
    private bool _isBusy;
    private CancellationTokenSource _cts = new();
    private readonly bool _viaGamepad;
    private readonly XInputService? _xinput;
    private bool _keyboardVisible;
    private readonly IntPtr _previousForegroundWindow;
    private readonly bool _returnToGame;
    private IntPtr _suspendedGameProcess = IntPtr.Zero;
    private readonly List<Button> _keyboardKeys = new();
    private readonly List<int> _keyboardRowLengths = new();
    private int _keyboardIndex;
    private DateTime _lastKeyboardNavigation = DateTime.MinValue;
    private bool _shiftEnabled = true;
    private bool _capitalizeFirstLetter = true;

    private void FocusInputIfInteractive()
    {
        if (_viaGamepad) return;
        InputBox.Focus();
        Keyboard.Focus(InputBox);
    }

    private static IntPtr? FindGameWindow(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            return null;

        try
        {
            var processName = Path.GetFileNameWithoutExtension(executablePath);
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    var processId = (uint)process.Id;
                    IntPtr found = IntPtr.Zero;
                    EnumWindows((hWnd, _) =>
                    {
                        GetWindowThreadProcessId(hWnd, out uint windowProcessId);
                        if (windowProcessId == processId && IsWindowVisible(hWnd))
                        {
                            found = hWnd;
                            return false;
                        }

                        return true;
                    }, IntPtr.Zero);

                    if (found != IntPtr.Zero)
                        return found;
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch { }

        return null;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnableWindow(IntPtr hWnd, bool enable);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr extraData);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int command);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [System.Runtime.InteropServices.DllImport("ntdll.dll")]
    private static extern int NtSuspendProcess(IntPtr processHandle);

    [System.Runtime.InteropServices.DllImport("ntdll.dll")]
    private static extern int NtResumeProcess(IntPtr processHandle);

    private const uint PROCESS_SUSPEND_RESUME = 0x0800;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public AiAssistantDialog(AiProvider provider, string apiKey,
                             string? gameName = null, bool viaGamepad = false,
                             XInputService? xinput = null,
                             IntPtr gameWindowHandle = default,
                             string? gameExecutablePath = null)
    {
        InitializeComponent();
        WindowState = viaGamepad ? WindowState.Normal : WindowState.Maximized;
        Width = 620;
        Height = 640;
        ShowActivated = true;
        _previousForegroundWindow = gameWindowHandle != IntPtr.Zero
            ? gameWindowHandle
            : FindGameWindow(gameExecutablePath) is { } gameWindow
                ? gameWindow
                : GetForegroundWindow();
        _gameName   = gameName;
        _viaGamepad = viaGamepad;
        _xinput     = xinput;
        _returnToGame = !string.IsNullOrWhiteSpace(gameName);
        BuildGameKeyboard();
        _ai = new AiAssistantService(provider, apiKey, gameName);

        TxtGameContext.Text = gameName is not null
            ? $"Jogando: {gameName}"
            : "Assistente Gamer";

        AddAssistantMessage(gameName is not null
            ? $"Ola! Estou pronto para te ajudar com **{gameName}**. Pode perguntar sobre dicas, puzzles, builds, segredos ou qualquer coisa do jogo!"
            : "Ola! Sou o GLauncher AI, seu assistente gamer. Pode perguntar sobre qualquer jogo ? dicas, analises, recomendacoes, builds e muito mais!");

        Loaded += (_, _) =>
        {
            if (_viaGamepad && _returnToGame && _previousForegroundWindow != IntPtr.Zero)
            {
                // N?o desabilitar a janela do jogo. Em jogos fullscreen/borderless
                // isso pode remov?-la da composi??o e impedir o retorno correto.
                SuspendGameProcess();
                PositionOverGameWindow();
                var chatHandle = new WindowInteropHelper(this).Handle;
                SetForegroundWindow(chatHandle);
            }

            Dispatcher.BeginInvoke(() =>
            {
                FocusInputIfInteractive();
            }, System.Windows.Threading.DispatcherPriority.Input);

            if (_viaGamepad || xinput?.IsConnected == true)
            {
                ShowGamepadHints(xinput?.ControllerName ?? "");
                Dispatcher.BeginInvoke(() =>
                {
                    _keyboardVisible = true;
                    GameKeyboardPanel.Visibility = Visibility.Visible;
                    FocusKeyboardKey(0);
                    IconKeyboard.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
                }, System.Windows.Threading.DispatcherPriority.Background);
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
            if (_xinput is not null)
            {
                _xinput.RightStickX -= OnRightStickX;
                _xinput.RightStickY -= OnRightStickY;
            }
            if (_returnToGame && _previousForegroundWindow != IntPtr.Zero)
            {
                ResumeGameProcess();
                Dispatcher.BeginInvoke(() =>
                {
                    ShowWindow(_previousForegroundWindow, 9 /* SW_RESTORE */);
                    SetForegroundWindow(_previousForegroundWindow);
                    _xinput?.Resume();
                },
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            else
            {
                _xinput?.Resume();
            }
        };

        if (xinput is not null)
        {
            xinput.ButtonPressed     += OnGamepadButton;
            xinput.ConnectionChanged += OnConnectionChanged;
            xinput.RightStickX      += OnRightStickX;
            xinput.RightStickY      += OnRightStickY;
        }
    }

    private void SuspendGameProcess()
    {
        if (_suspendedGameProcess != IntPtr.Zero)
            return;

        GetWindowThreadProcessId(_previousForegroundWindow, out uint processId);
        if (processId == 0 || processId == Environment.ProcessId)
            return;

        var handle = OpenProcess(PROCESS_SUSPEND_RESUME, false, processId);
        if (handle == IntPtr.Zero)
            return;

        if (NtSuspendProcess(handle) == 0)
            _suspendedGameProcess = handle;
        else
            CloseHandle(handle);
    }

    private void ResumeGameProcess()
    {
        if (_suspendedGameProcess == IntPtr.Zero)
            return;

        NtResumeProcess(_suspendedGameProcess);
        CloseHandle(_suspendedGameProcess);
        _suspendedGameProcess = IntPtr.Zero;
    }

    private void PositionOverGameWindow()
    {
        if (!GetWindowRect(_previousForegroundWindow, out var rect))
            return;

        WindowState = WindowState.Normal;
        Width = 620;
        Height = 640;
        Left = rect.Left + ((rect.Right - rect.Left) - Width) / 2;
        Top = rect.Top + ((rect.Bottom - rect.Top) - Height) / 2;
    }

    private void OnRightStickX(double value)
    {
        if (Math.Abs(value) < 0.2) return;
    }

    private void OnRightStickY(double value)
    {
        if (Math.Abs(value) < 0.2) return;
        Dispatcher.BeginInvoke(() => MessagesScroll.ScrollToVerticalOffset(
            Math.Clamp(MessagesScroll.VerticalOffset - value * 80,
                0, MessagesScroll.ScrollableHeight)));
    }

    // -- Hints din?micos ----------------------------------------------------

    private void OnConnectionChanged(bool connected)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (connected)
            {
                ShowGamepadHints(_xinput?.ControllerName ?? "");
                _keyboardVisible = true;
                GameKeyboardPanel.Visibility = Visibility.Visible;
                FocusKeyboardKey(_keyboardIndex);
                IconKeyboard.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
            }
            else
            {
                ShowKeyboardHints();
                _keyboardVisible = false;
                GameKeyboardPanel.Visibility = Visibility.Collapsed;
                FocusInputIfInteractive();
            }
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
        HintBadgeY.Text = isPS ? "Tri"     : "Y";
        HintBadgeX.Text = isPS ? "Quad"    : "X";
    }

    private void ShowKeyboardHints()
    {
        HintsGamepad.Visibility  = Visibility.Collapsed;
        HintsKeyboard.Visibility = Visibility.Visible;
    }

    // -- Navega??o por controle ---------------------------------------------

    private void OnGamepadButton(GamepadButton btn)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (btn == GamepadButton.Start)
            {
                ToggleVirtualKeyboard();
                return;
            }

            if (btn == GamepadButton.Y)
            {
                if (_keyboardVisible)
                {
                    InputBox.Text += " ";
                    InputBox.CaretIndex = InputBox.Text.Length;
                    FocusInputIfInteractive();
                }
                return;
            }

            if (btn == GamepadButton.X)
            {
                if (_keyboardVisible)
                    DeleteLastCharacter();
                return;
            }

            if (_keyboardVisible && btn is GamepadButton.DPadUp or GamepadButton.DPadDown
                or GamepadButton.DPadLeft or GamepadButton.DPadRight or GamepadButton.A)
            {
                switch (btn)
                {
                    case GamepadButton.DPadLeft:  MoveKeyboardFocusHorizontal(-1); return;
                    case GamepadButton.DPadRight: MoveKeyboardFocusHorizontal(1); return;
                    case GamepadButton.DPadUp:    MoveKeyboardFocusVertical(-1); return;
                    case GamepadButton.DPadDown:  MoveKeyboardFocusVertical(1); return;
                    case GamepadButton.A:         _keyboardKeys[_keyboardIndex].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return;
                }
            }

            switch (btn)
            {
                case GamepadButton.A:            // Enviar
                    _ = SendMessageAsync();
                    break;
                case GamepadButton.B:            // Fechar
                    Close();
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
            GameKeyboardPanel.Visibility = Visibility.Visible;
            FocusKeyboardKey(_keyboardIndex);
            IconKeyboard.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
        }
        else
        {
            GameKeyboardPanel.Visibility = Visibility.Collapsed;
            IconKeyboard.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x88, 0x99, 0xBB));
            FocusInputIfInteractive();
        }
    }

    private void BuildGameKeyboard()
    {
        string[][] rows =
        [
            ["1|!", "2|@", "3|#", "4|$", "5|%", "6|?", "7|&", "8|*", "9|(", "0|)", "-|_", "=|+"],
            ["q|Q", "w|W", "e|E", "r|R", "t|T", "y|Y", "u|U", "i|I", "o|O", "p|P", "?|`", "[|{", "]|}"],
            ["a|A", "s|S", "d|D", "f|F", "g|G", "h|H", "j|J", "k|K", "l|L", "?|?", "~|^"],
            ["SHIFT", "z|Z", "x|X", "c|C", "v|V", "b|B", "n|N", "m|M", ",|<", ".|>", ";|:"],
            ["/|?", "\\||", "ESPA?O", "ENVIAR"]
        ];

        foreach (var row in rows)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            _keyboardRowLengths.Add(row.Length);
            foreach (var key in row)
            {
                var button = new Button
                {
                    Content = GetKeyboardDisplayText(key),
                    Tag = key,
                    MinWidth = key is "ESPA?O" or "ENVIAR" ? 112 : key == "SHIFT" ? 72 : 34,
                    Height = 30,
                    Margin = new Thickness(2),
                    Padding = new Thickness(4, 0, 4, 0),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = key is "ENVIAR" or "SHIFT" ? System.Windows.Media.Brushes.White :
                        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDD, 0xD8, 0xFF)),
                    Background = key is "ENVIAR" or "SHIFT"
                        ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x24, 0x24, 0x48)),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4B, 0x3B, 0x7A)),
                    BorderThickness = new Thickness(1),
                    Focusable = true
                };
                button.Click += GameKeyboardKey_Click;
                _keyboardKeys.Add(button);
                panel.Children.Add(button);
            }
            KeyboardRows.Children.Add(panel);
        }
    }

    private void GameKeyboardKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string key) return;

        if (key == "SHIFT")
        {
            _shiftEnabled = !_shiftEnabled;
            UpdateKeyboardLabels();
        }
        else if (key == "?")
        {
            DeleteLastCharacter();
        }
        else if (key == "ESPA?O")
            InputBox.Text += " ";
        else if (key == "ENVIAR")
            _ = SendMessageAsync();
        else
        {
            InputBox.Text += GetKeyboardInputText(key);

            var baseKey = key.Split('|', 2)[0];
            if (_capitalizeFirstLetter && baseKey.Length == 1 && char.IsLetter(baseKey[0]))
            {
                _capitalizeFirstLetter = false;
                _shiftEnabled = false;
                UpdateKeyboardLabels();
            }
        }

        InputBox.CaretIndex = InputBox.Text.Length;
    }

    private void UpdateKeyboardLabels()
    {
        foreach (var key in _keyboardKeys)
        {
            if (key.Tag is not string value || value is "SHIFT" or "ESPA?O" or "ENVIAR")
                continue;

            key.Content = GetKeyboardDisplayText(value);
        }

        var shiftKey = _keyboardKeys.FirstOrDefault(key => key.Tag as string == "SHIFT");
        if (shiftKey is not null)
            shiftKey.Content = _shiftEnabled ? "SHIFT" : "shift";

        if (_keyboardKeys.Count > 0)
            FocusKeyboardKey(_keyboardIndex);
    }

    private string GetKeyboardDisplayText(string key)
    {
        if (key is "SHIFT" or "ESPA?O" or "ENVIAR")
            return key;

        var parts = key.Split('|', 2);
        return _shiftEnabled
            ? parts.Length > 1 ? parts[1] : parts[0].ToUpperInvariant()
            : parts[0];
    }

    private string GetKeyboardInputText(string key)
    {
        if (key is "SHIFT" or "ESPA?O" or "ENVIAR")
            return key;

        var parts = key.Split('|', 2);
        return _shiftEnabled && parts.Length > 1 ? parts[1] : parts[0];
    }

    private void DeleteLastCharacter()
    {
        if (InputBox.Text.Length == 0) return;

        InputBox.Text = InputBox.Text[..^1];
        InputBox.CaretIndex = InputBox.Text.Length;
    }

    private bool CanNavigateKeyboard()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastKeyboardNavigation).TotalMilliseconds < 140)
            return false;

        _lastKeyboardNavigation = now;
        return true;
    }

    private void MoveKeyboardFocusHorizontal(int delta)
    {
        if (_keyboardKeys.Count == 0) return;
        if (!CanNavigateKeyboard()) return;

        GetKeyboardPosition(_keyboardIndex, out int row, out int column);
        column = Math.Clamp(column + delta, 0, _keyboardRowLengths[row] - 1);
        _keyboardIndex = GetKeyboardIndex(row, column);
        FocusKeyboardKey(_keyboardIndex);
    }

    private void MoveKeyboardFocusVertical(int delta)
    {
        if (_keyboardKeys.Count == 0) return;
        if (!CanNavigateKeyboard()) return;

        GetKeyboardPosition(_keyboardIndex, out int row, out int column);
        row = Math.Clamp(row + delta, 0, _keyboardRowLengths.Count - 1);
        column = Math.Clamp(column, 0, _keyboardRowLengths[row] - 1);
        _keyboardIndex = GetKeyboardIndex(row, column);
        FocusKeyboardKey(_keyboardIndex);
    }

    private void GetKeyboardPosition(int index, out int row, out int column)
    {
        row = 0;
        while (row < _keyboardRowLengths.Count - 1 && index >= _keyboardRowLengths[row])
        {
            index -= _keyboardRowLengths[row];
            row++;
        }

        column = index;
    }

    private int GetKeyboardIndex(int row, int column)
    {
        int index = 0;
        for (int i = 0; i < row; i++)
            index += _keyboardRowLengths[i];

        return index + column;
    }

    private void FocusKeyboardKey(int index)
    {
        if (_keyboardKeys.Count == 0) return;
        _keyboardIndex = Math.Clamp(index, 0, _keyboardKeys.Count - 1);

        for (int i = 0; i < _keyboardKeys.Count; i++)
        {
            var key = _keyboardKeys[i];
            bool selected = i == _keyboardIndex;
            string keyName = key.Tag as string ?? string.Empty;
            bool isAction = keyName is "ENVIAR" or "SHIFT";
            bool isSend = keyName == "ENVIAR";

            key.Background = selected
                ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                : isAction
                    ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                    : new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x24, 0x24, 0x48));
            key.BorderBrush = selected
                ? System.Windows.Media.Brushes.White
                : new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x4B, 0x3B, 0x7A));
            key.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            key.Effect = selected || (keyName == "SHIFT" && _shiftEnabled)
                ? new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = System.Windows.Media.Color.FromRgb(0x9D, 0x7B, 0xFF),
                    BlurRadius = 18,
                    ShadowDepth = 0,
                    Opacity = 0.95
                }
                : null;
        }

        FocusInputIfInteractive();
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

    // -- Envio de mensagem --------------------------------------------------

    private async Task SendMessageAsync()
    {
        var text = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(text) || _isBusy) return;

        _isBusy = true;
        BtnSend.IsEnabled = false;
        InputBox.Clear();

        AddUserMessage(text);
        TypingIndicator.Visibility = Visibility.Visible;
        ScrollToBottom();

        var assistantMsg = new AiChatMessage { Role = ChatRole.Assistant, Text = "" };
        MessagesList.Items.Add(assistantMsg);

        _cts = new CancellationTokenSource();
        try
        {
            await foreach (var token in _ai.SendAsync(text, cancellationToken: _cts.Token))
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
            FocusInputIfInteractive();
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
