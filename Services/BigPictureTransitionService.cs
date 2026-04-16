using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace GameLauncher.Services;

/// <summary>
/// Orquestra a animação cinematográfica de entrada/saída do Modo Big Picture.
/// Estrutura modular: cada fase (Flash, Vinheta, Logo, Barras, FadeOut) é
/// um método privado independente para fácil manutenção.
/// </summary>
public sealed class BigPictureTransitionService
{
    // ─── Referências aos elementos nomeados do XAML ──────────────────────────
    private readonly FrameworkElement _overlay;       // BpTransitionOverlay
    private readonly FrameworkElement _flash;         // BpFlash
    private readonly FrameworkElement _vignette;      // BpVignette
    private readonly FrameworkElement _logoContainer; // BpLogoContainer
    private readonly ScaleTransform   _logoScale;     // BpLogoScale
    private readonly DropShadowEffect _logoGlow;      // BpLogoGlow
    private readonly ScaleTransform   _pulseScale;    // BpPulseScale
    private readonly FrameworkElement _barLeft;       // BpBarLeft
    private readonly FrameworkElement _barRight;      // BpBarRight
    private readonly FrameworkElement _fadeOut;       // BpFadeOut

    // ─── Durations ───────────────────────────────────────────────────────────
    private static readonly Duration DFlash    = Duration(0.10);
    private static readonly Duration DVignette = Duration(0.25);
    private static readonly Duration DLogo     = Duration(0.40);
    private static readonly Duration DBars     = Duration(0.22);
    private static readonly Duration DFadeOut  = Duration(0.35);

    public BigPictureTransitionService(MainWindow window)
    {
        _overlay       = Get<FrameworkElement>(window, "BpTransitionOverlay");
        _flash         = Get<FrameworkElement>(window, "BpFlash");
        _vignette      = Get<FrameworkElement>(window, "BpVignette");
        _logoContainer = Get<FrameworkElement>(window, "BpLogoContainer");
        _logoScale     = Get<ScaleTransform>  (window, "BpLogoScale");
        _logoGlow      = Get<DropShadowEffect> (window, "BpLogoGlow");
        _pulseScale    = Get<ScaleTransform>  (window, "BpPulseScale");
        _barLeft       = Get<FrameworkElement>(window, "BpBarLeft");
        _barRight      = Get<FrameworkElement>(window, "BpBarRight");
        _fadeOut       = Get<FrameworkElement>(window, "BpFadeOut");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  API pública
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Executa a animação completa de entrada no Big Picture.</summary>
    public void PlayEnter(Action onComplete)
    {
        SoundService.PlayBigPicture();
        ResetAll();
        ShowOverlay();
        RunEnterSequence(onComplete);
    }

    /// <summary>Executa a animação de saída do Big Picture.</summary>
    public void PlayExit(Action onComplete)
    {
        ResetAll();
        ShowOverlay();
        RunExitSequence(onComplete);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Sequências
    // ═════════════════════════════════════════════════════════════════════════

    private void RunEnterSequence(Action onComplete)
    {
        // Fase 0: Flash branco (0ms)
        AnimateFlash(fadeIn: true, delay: 0, duration: 0.18, then: () =>

        // Fase 1: Flash desaparece + vinheta aparece (140ms)
        AnimateFlash(fadeIn: false, delay: 0.14, duration: 0.30, then: () => { }));
        AnimateVignette(fadeIn: true, delay: 0.10, duration: 0.45);

        // Fase 2: Barras laterais deslizam (220ms)
        AnimateBars(expand: true, delay: 0.22, duration: 0.38);

        // Fase 3: Logo surge com zoom (380ms)
        AnimateLogo(enter: true, delay: 0.38, duration: 0.65);
        AnimatePulse(delay: 0.70);

        // Fase 4: Hold visível + fade para o Big Picture (~2.0s total)
        FadeOut(delay: 1.30, duration: 0.55, then: () =>
        {
            HideOverlay();
            onComplete();
        });
    }

    private void RunExitSequence(Action onComplete)
    {
        // Saída moderada: flash + fade preto
        AnimateFlash(fadeIn: true, delay: 0, duration: 0.14, then: () =>
        AnimateFlash(fadeIn: false, delay: 0.12, duration: 0.32, then: () => { }));

        FadeOut(delay: 0.10, duration: 0.45, then: () =>
        {
            HideOverlay();
            onComplete();
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Fases individuais (modulares)
    // ═════════════════════════════════════════════════════════════════════════

    private void AnimateFlash(bool fadeIn, double delay, double duration, Action then)
    {
        var anim = Ease(new DoubleAnimation
        {
            From           = fadeIn ? 0 : 0.85,
            To             = fadeIn ? 0.85 : 0,
            Duration       = Duration(duration),
            BeginTime      = TimeSpan.FromSeconds(delay),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        anim.Completed += (_, _) => then();
        _flash.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    private void AnimateVignette(bool fadeIn, double delay, double duration)
    {
        _vignette.BeginAnimation(UIElement.OpacityProperty, Ease(new DoubleAnimation
        {
            From      = fadeIn ? 0 : 1,
            To        = fadeIn ? 1 : 0,
            Duration  = Duration(duration),
            BeginTime = TimeSpan.FromSeconds(delay),
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        }));
    }

    private void AnimateLogo(bool enter, double delay, double duration)
    {
        var easing = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 };

        // Opacidade
        _logoContainer.BeginAnimation(UIElement.OpacityProperty, Ease(new DoubleAnimation
        {
            From = enter ? 0 : 1, To = enter ? 1 : 0,
            Duration  = Duration(duration),
            BeginTime = TimeSpan.FromSeconds(delay),
            EasingFunction = enter ? easing : new CubicEase { EasingMode = EasingMode.EaseIn }
        }));

        // Scale X
        _logoScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation
        {
            From = enter ? 0.25 : 1, To = enter ? 1 : 0.25,
            Duration  = Duration(duration),
            BeginTime = TimeSpan.FromSeconds(delay),
            EasingFunction = easing
        });

        // Scale Y
        _logoScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation
        {
            From = enter ? 0.25 : 1, To = enter ? 1 : 0.25,
            Duration  = Duration(duration),
            BeginTime = TimeSpan.FromSeconds(delay),
            EasingFunction = easing
        });

        // Glow do logo
        _logoGlow.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation
        {
            From = enter ? 0 : 0.9, To = enter ? 0.9 : 0,
            Duration  = Duration(duration * 0.7),
            BeginTime = TimeSpan.FromSeconds(delay + 0.10)
        });
        _logoGlow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation
        {
            From = enter ? 80 : 30, To = enter ? 30 : 80,
            Duration  = Duration(duration),
            BeginTime = TimeSpan.FromSeconds(delay)
        });

        // Atualizar cor do glow com o tema atual
        if (ColorConverter.ConvertFromString(SettingsService.Current.AccentColor) is Color c)
            _logoGlow.Color = c;
    }

    private void AnimatePulse(double delay)
    {
        var pulse = new DoubleAnimationUsingKeyFrames
        {
            BeginTime      = TimeSpan.FromSeconds(delay),
            RepeatBehavior = new RepeatBehavior(2)
        };
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1.0,  KeyTime.FromPercent(0)));
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1.12, KeyTime.FromPercent(0.5),
            new SineEase { EasingMode = EasingMode.EaseOut }));
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1.0,  KeyTime.FromPercent(1)));
        pulse.Duration = Duration(0.30);

        _pulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        _pulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
    }

    private void AnimateBars(bool expand, double delay, double duration)
    {
        double targetW = expand ? 6 : 0;
        var easing = new QuarticEase { EasingMode = EasingMode.EaseOut };

        _barLeft.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation
        {
            From = expand ? 0 : 6, To = targetW,
            Duration  = Duration(duration),
            BeginTime = TimeSpan.FromSeconds(delay),
            EasingFunction = easing
        });
        _barLeft.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = expand ? 0 : 0.85, To = expand ? 0.85 : 0,
            Duration  = Duration(duration),
            BeginTime = TimeSpan.FromSeconds(delay)
        });

        _barRight.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation
        {
            From = expand ? 0 : 6, To = targetW,
            Duration  = Duration(duration),
            BeginTime = TimeSpan.FromSeconds(delay),
            EasingFunction = easing
        });
        _barRight.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = expand ? 0 : 0.85, To = expand ? 0.85 : 0,
            Duration  = Duration(duration),
            BeginTime = TimeSpan.FromSeconds(delay)
        });
    }

    private void FadeOut(double delay, double duration, Action then)
    {
        var anim = new DoubleAnimation
        {
            From           = 0, To = 1,
            Duration       = Duration(duration),
            BeginTime      = TimeSpan.FromSeconds(delay),
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseIn }
        };
        anim.Completed += (_, _) => then();
        _fadeOut.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═════════════════════════════════════════════════════════════════════════

    private void ShowOverlay()
    {
        _overlay.Visibility = Visibility.Visible;
        _overlay.IsHitTestVisible = true;
    }

    private void HideOverlay()
    {
        _overlay.Visibility = Visibility.Collapsed;
        _overlay.IsHitTestVisible = false;
    }

    private void ResetAll()
    {
        SetOpacity(_flash,         0);
        SetOpacity(_vignette,      0);
        SetOpacity(_logoContainer, 0);
        SetOpacity(_barLeft,       0);
        SetOpacity(_barRight,      0);
        SetOpacity(_fadeOut,       0);
        _logoScale.ScaleX = 0.25;
        _logoScale.ScaleY = 0.25;
        _logoGlow.Opacity = 0;
        _barLeft.SetCurrentValue(FrameworkElement.WidthProperty, 0d);
        _barRight.SetCurrentValue(FrameworkElement.WidthProperty, 0d);
    }

    private static void SetOpacity(FrameworkElement el, double value) =>
        el.SetCurrentValue(UIElement.OpacityProperty, value);

    private static Duration Duration(double seconds) =>
        new(TimeSpan.FromSeconds(seconds));

    private static DoubleAnimation Ease(DoubleAnimation anim) => anim;

    private static T Get<T>(MainWindow window, string name) where T : class =>
        (T)(window.FindName(name) ?? throw new InvalidOperationException(
            $"Elemento '{name}' não encontrado no MainWindow."));
}
