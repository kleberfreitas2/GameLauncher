using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GameLauncher.Services;

/// <summary>
/// Registra hotkeys globais via WinAPI (RegisterHotKey) e mantém um polling
/// de fallback via GetAsyncKeyState para funcionar mesmo em fullscreen exclusivo
/// (jogos DirectX que capturam 100% do input e impedem WM_HOTKEY de chegar).
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    // ── WinAPI ────────────────────────────────────────────────────────────
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);

    private const int WM_HOTKEY = 0x0312;

    // Teclas virtuais usadas no polling
    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT   = 0x10;
    private const int VK_MENU    = 0x12; // Alt

    public const uint MOD_NONE  = 0x0000;
    public const uint MOD_ALT   = 0x0001;
    public const uint MOD_CTRL  = 0x0002;
    public const uint MOD_SHIFT = 0x0004;

    // ── Estado ────────────────────────────────────────────────────────────
    private readonly IntPtr _hwnd;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 9000;

    // Hotkey registrada para polling de fallback em fullscreen
    private uint _pollMods;
    private uint _pollVk;
    private Action? _pollCallback;
    private Thread? _pollThread;
    private volatile bool _pollRunning;

    private const int POLL_INTERVAL_MS = 50;

    /// <summary>Hotkey da IA efetivamente registrada (label legível).</summary>
    public static string AiHotkeyLabel { get; private set; } = "Ctrl+Shift+A";

    public GlobalHotkeyService(Window window)
    {
        _hwnd = new WindowInteropHelper(window).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
    }

    /// <summary>
    /// Tenta registrar a hotkey preferida e, se falhar, percorre alternativas
    /// até encontrar uma livre. Atualiza <see cref="AiHotkeyLabel"/> com a que funcionou.
    /// Inicia automaticamente o polling de fallback para fullscreen exclusivo.
    /// </summary>
    public int RegisterWithFallback(
        (uint mods, uint vk, string label)[] candidates,
        Action callback)
    {
        foreach (var (mods, vk, label) in candidates)
        {
            var id = _nextId++;
            if (RegisterHotKey(_hwnd, id, mods, vk))
            {
                _handlers[id] = callback;
                AiHotkeyLabel = label;
                Debug.WriteLine($"[Hotkey] Registrada: {label}");
                StartFallbackPoll(mods, vk, callback);
                return id;
            }
            Debug.WriteLine($"[Hotkey] Falhou ao registrar: {label}");
        }
        Debug.WriteLine("[Hotkey] Nenhuma combinação disponível.");
        return -1;
    }

    public int Register(uint modifiers, uint virtualKey, Action callback)
    {
        var id = _nextId++;
        if (RegisterHotKey(_hwnd, id, modifiers, virtualKey))
            _handlers[id] = callback;
        return id;
    }

    public void Unregister(int id)
    {
        if (id < 0) return;
        UnregisterHotKey(_hwnd, id);
        _handlers.Remove(id);
    }

    // ── Polling de fallback (funciona em fullscreen exclusivo) ─────────────

    private void StartFallbackPoll(uint mods, uint vk, Action callback)
    {
        _pollMods     = mods;
        _pollVk       = vk;
        _pollCallback = callback;
        _pollRunning  = true;

        _pollThread = new Thread(PollLoop)
        {
            IsBackground = true,
            Name = "GlobalHotkey-FallbackPoll"
        };
        _pollThread.Start();
    }

    private void PollLoop()
    {
        // Janela de cooldown: evita disparar múltiplas vezes enquanto a tecla
        // está pressionada. Só re-arma quando todas as teclas são soltas.
        bool wasFired = false;

        while (_pollRunning)
        {
            Thread.Sleep(POLL_INTERVAL_MS);

            bool ctrl  = (_pollMods & MOD_CTRL)  != 0 && IsDown(VK_CONTROL);
            bool shift = (_pollMods & MOD_SHIFT)  != 0 && IsDown(VK_SHIFT);
            bool alt   = (_pollMods & MOD_ALT)    != 0 && IsDown(VK_MENU);

            bool modsOk =
                ((_pollMods & MOD_CTRL)  == 0 || ctrl)  &&
                ((_pollMods & MOD_SHIFT) == 0 || shift) &&
                ((_pollMods & MOD_ALT)   == 0 || alt);

            bool mainDown = IsDown((int)_pollVk);
            bool comboDown = modsOk && mainDown;

            if (comboDown && !wasFired)
            {
                wasFired = true;
                try { _pollCallback?.Invoke(); }
                catch { }
            }
            else if (!comboDown)
            {
                wasFired = false;
            }
        }
    }

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    // ── WndProc (caminho primário via RegisterHotKey) ──────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _handlers.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _pollRunning = false;
        foreach (var id in _handlers.Keys)
            UnregisterHotKey(_hwnd, id);
        _handlers.Clear();
    }
}
