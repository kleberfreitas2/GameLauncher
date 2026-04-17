using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace GameLauncher.Services;

/// <summary>
/// Controla o teclado virtual do Windows (TabTip) para uso com controle.
/// </summary>
public static class VirtualKeyboardService
{
    // Caminho do teclado touch do Windows (TabTip)
    private static readonly string TabTipPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "Common Files", "Microsoft Shared", "ink", "TabTip.exe");

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_SYSCOMMAND = 0x0112;
    private const uint SC_CLOSE      = 0xF060;

    public static void Show()
    {
        try
        {
            if (File.Exists(TabTipPath))
                Process.Start(new ProcessStartInfo(TabTipPath) { UseShellExecute = true });
        }
        catch { }
    }

    public static void Hide()
    {
        try
        {
            var hwnd = FindWindow("IPTIP_Main_Window", null);
            if (hwnd != IntPtr.Zero)
                PostMessage(hwnd, WM_SYSCOMMAND, new IntPtr(SC_CLOSE), IntPtr.Zero);
        }
        catch { }
    }
}
