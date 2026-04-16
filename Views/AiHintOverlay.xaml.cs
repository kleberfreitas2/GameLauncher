using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace GameLauncher.Views;

/// <summary>
/// Overlay que aparece no canto da tela ao iniciar um jogo,
/// informando o jogador sobre o GLauncher AI e a hotkey.
/// Some automaticamente após alguns segundos.
/// </summary>
public partial class AiHintOverlay : Window
{
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private const int GWL_EXSTYLE      = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW  = 0x00000080;
    private const int WS_EX_NOACTIVATE  = 0x08000000;

    private readonly DispatcherTimer _dismissTimer;

    /// <summary>Tempo em segundos que o overlay fica visível antes de sumir.</summary>
    private const double VisibleSeconds = 6;

    public AiHintOverlay()
    {
        InitializeComponent();

        SourceInitialized += (_, _) => MakePassThrough();

        Loaded += (_, _) =>
        {
            // Exibe a hotkey que foi efetivamente registrada pelo sistema
            HotkeyBadge.Text = GameLauncher.Services.GlobalHotkeyService.AiHotkeyLabel;
            PositionBottomRight();
            PlayFadeIn();
        };

        _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(VisibleSeconds) };
        _dismissTimer.Tick += (_, _) =>
        {
            _dismissTimer.Stop();
            PlayFadeOutAndClose();
        };
        _dismissTimer.Start();
    }

    // Coloca o overlay no canto inferior direito da área de trabalho
    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        UpdateLayout();
        Left = area.Right  - ActualWidth  - 24;
        Top  = area.Bottom - ActualHeight - 24;
    }

    // Torna a janela click-through (não bloqueia cliques no jogo)
    private void MakePassThrough()
    {
        var hwnd  = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE,
            style | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    private void PlayFadeIn()
    {
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, anim);
    }

    private void PlayFadeOutAndClose()
    {
        var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(600))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        anim.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, anim);
    }
}
