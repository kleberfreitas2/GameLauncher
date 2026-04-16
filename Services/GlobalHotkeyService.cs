using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GameLauncher.Services;

/// <summary>
/// Registra hotkeys globais via WinAPI que funcionam mesmo com o launcher minimizado.
/// Tenta automaticamente combinações alternativas se a preferida já estiver ocupada.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;

    public const uint MOD_NONE  = 0x0000;
    public const uint MOD_ALT   = 0x0001;
    public const uint MOD_CTRL  = 0x0002;
    public const uint MOD_SHIFT = 0x0004;

    private readonly IntPtr _hwnd;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 9000;

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
        foreach (var id in _handlers.Keys)
            UnregisterHotKey(_hwnd, id);
        _handlers.Clear();
    }
}
