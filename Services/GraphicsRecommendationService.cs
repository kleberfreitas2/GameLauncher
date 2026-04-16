using GameLauncher.Models;

namespace GameLauncher.Services;

/// <summary>
/// Motor de recomendação gráfica. Contém um banco de dados de ~40 jogos populares com
/// requisitos por preset e gera mensagens como "Seu PC roda Cyberpunk em Alto a ~60fps".
/// </summary>
public static class GraphicsRecommendationService
{
    // ─── Estrutura interna ────────────────────────────────────────────────

    private record GameEntry(
        string[]  Names,         // padrões de nome (busca por Contains, case-insensitive)
        int       MinGpu,        // score mínimo para rodar (Baixo)
        int       RecGpu,        // score para Médio
        int       HighGpu,       // score para Alto
        int       UltraGpu,      // score para Ultra
        double    MinRamGb   = 8,
        double    RecRamGb   = 16,
        int       MinVramGb  = 4,
        int       RecVramGb  = 8,
        string    Category   = "Médio"   // Leve / Médio / Pesado / Muito Pesado
    );

    // ─── Banco de dados de jogos ──────────────────────────────────────────

    private static readonly GameEntry[] GameDb =
    [
        // ── Jogos Leves ────────────────────────────────────────────────────
        new(["MINECRAFT"],
            MinGpu:4,  RecGpu:8,  HighGpu:15, UltraGpu:25,
            MinRamGb:4, RecRamGb:8, MinVramGb:1, RecVramGb:2, Category:"Leve"),

        new(["STARDEW VALLEY", "STARDEW"],
            MinGpu:2, RecGpu:4, HighGpu:8, UltraGpu:12,
            MinRamGb:2, RecRamGb:4, MinVramGb:1, RecVramGb:1, Category:"Leve"),

        new(["AMONG US"],
            MinGpu:2, RecGpu:4, HighGpu:8, UltraGpu:12,
            MinRamGb:2, RecRamGb:4, MinVramGb:1, RecVramGb:1, Category:"Leve"),

        new(["HOLLOW KNIGHT"],
            MinGpu:3, RecGpu:6, HighGpu:10, UltraGpu:16,
            MinRamGb:4, RecRamGb:4, MinVramGb:1, RecVramGb:2, Category:"Leve"),

        new(["TERRARIA"],
            MinGpu:2, RecGpu:4, HighGpu:8, UltraGpu:12,
            MinRamGb:2, RecRamGb:4, MinVramGb:1, RecVramGb:1, Category:"Leve"),

        new(["CELESTE"],
            MinGpu:2, RecGpu:4, HighGpu:8, UltraGpu:12,
            MinRamGb:2, RecRamGb:4, MinVramGb:1, RecVramGb:1, Category:"Leve"),

        new(["HADES"],
            MinGpu:5, RecGpu:10, HighGpu:18, UltraGpu:28,
            MinRamGb:4, RecRamGb:8, MinVramGb:2, RecVramGb:4, Category:"Leve"),

        // ── Jogos Médios ──────────────────────────────────────────────────
        new(["COUNTER-STRIKE 2", "CS2", "CSGO", "CS:GO"],
            MinGpu:10, RecGpu:22, HighGpu:38, UltraGpu:55,
            MinRamGb:8, RecRamGb:16, MinVramGb:2, RecVramGb:4, Category:"Médio"),

        new(["VALORANT"],
            MinGpu:8, RecGpu:18, HighGpu:30, UltraGpu:48,
            MinRamGb:4, RecRamGb:16, MinVramGb:2, RecVramGb:4, Category:"Médio"),

        new(["FORTNITE"],
            MinGpu:12, RecGpu:25, HighGpu:45, UltraGpu:62,
            MinRamGb:8, RecRamGb:16, MinVramGb:2, RecVramGb:6, Category:"Médio"),

        new(["APEX LEGENDS", "APEX"],
            MinGpu:15, RecGpu:28, HighGpu:46, UltraGpu:63,
            MinRamGb:8, RecRamGb:16, MinVramGb:4, RecVramGb:8, Category:"Médio"),

        new(["LEAGUE OF LEGENDS", "LOL"],
            MinGpu:5,  RecGpu:12, HighGpu:20, UltraGpu:32,
            MinRamGb:4, RecRamGb:8, MinVramGb:1, RecVramGb:2, Category:"Médio"),

        new(["DOTA 2", "DOTA2"],
            MinGpu:8,  RecGpu:18, HighGpu:30, UltraGpu:45,
            MinRamGb:4, RecRamGb:8, MinVramGb:2, RecVramGb:4, Category:"Médio"),

        new(["ROCKET LEAGUE"],
            MinGpu:8,  RecGpu:18, HighGpu:30, UltraGpu:48,
            MinRamGb:4, RecRamGb:8, MinVramGb:2, RecVramGb:4, Category:"Médio"),

        new(["WARFRAME"],
            MinGpu:10, RecGpu:20, HighGpu:35, UltraGpu:52,
            MinRamGb:4, RecRamGb:8, MinVramGb:2, RecVramGb:4, Category:"Médio"),

        new(["GTA V", "GTA5", "GRAND THEFT AUTO V"],
            MinGpu:15, RecGpu:28, HighGpu:45, UltraGpu:62,
            MinRamGb:8, RecRamGb:16, MinVramGb:2, RecVramGb:4, Category:"Médio"),

        new(["DOOM ETERNAL", "DOOM ETERNAL"],
            MinGpu:15, RecGpu:28, HighGpu:46, UltraGpu:62,
            MinRamGb:8, RecRamGb:8, MinVramGb:4, RecVramGb:8, Category:"Médio"),

        new(["DARK SOULS 3", "DARK SOULS III"],
            MinGpu:15, RecGpu:28, HighGpu:42, UltraGpu:58,
            MinRamGb:8, RecRamGb:8, MinVramGb:2, RecVramGb:4, Category:"Médio"),

        new(["HALO INFINITE", "HALO"],
            MinGpu:18, RecGpu:32, HighGpu:50, UltraGpu:68,
            MinRamGb:8, RecRamGb:16, MinVramGb:4, RecVramGb:8, Category:"Médio"),

        new(["MONSTER HUNTER WORLD", "MONSTER HUNTER"],
            MinGpu:18, RecGpu:32, HighGpu:48, UltraGpu:65,
            MinRamGb:8, RecRamGb:8, MinVramGb:4, RecVramGb:8, Category:"Médio"),

        new(["CALL OF DUTY", "WARZONE", "MW2", "MW3", "COD"],
            MinGpu:20, RecGpu:38, HighGpu:55, UltraGpu:72,
            MinRamGb:8, RecRamGb:16, MinVramGb:4, RecVramGb:8, Category:"Médio"),

        // ── Jogos Pesados ──────────────────────────────────────────────────
        new(["THE WITCHER 3", "WITCHER 3"],
            MinGpu:18, RecGpu:32, HighGpu:50, UltraGpu:70,
            MinRamGb:6, RecRamGb:12, MinVramGb:2, RecVramGb:6, Category:"Pesado"),

        new(["ELDEN RING"],
            MinGpu:22, RecGpu:38, HighGpu:55, UltraGpu:72,
            MinRamGb:8, RecRamGb:16, MinVramGb:4, RecVramGb:8, Category:"Pesado"),

        new(["RESIDENT EVIL 2", "RESIDENT EVIL 3", "RESIDENT EVIL 4", "RESIDENT EVIL"],
            MinGpu:20, RecGpu:35, HighGpu:52, UltraGpu:68,
            MinRamGb:8, RecRamGb:16, MinVramGb:4, RecVramGb:8, Category:"Pesado"),

        new(["BALDUR'S GATE 3", "BALDURS GATE 3", "BALDUR'S GATE", "BG3"],
            MinGpu:20, RecGpu:36, HighGpu:52, UltraGpu:68,
            MinRamGb:8, RecRamGb:16, MinVramGb:4, RecVramGb:8, Category:"Pesado"),

        new(["DIABLO IV", "DIABLO 4", "DIABLO IV"],
            MinGpu:20, RecGpu:35, HighGpu:52, UltraGpu:68,
            MinRamGb:8, RecRamGb:16, MinVramGb:4, RecVramGb:8, Category:"Pesado"),

        new(["GOD OF WAR"],
            MinGpu:22, RecGpu:38, HighGpu:55, UltraGpu:70,
            MinRamGb:8, RecRamGb:16, MinVramGb:4, RecVramGb:8, Category:"Pesado"),

        new(["DEATH STRANDING"],
            MinGpu:18, RecGpu:32, HighGpu:48, UltraGpu:65,
            MinRamGb:8, RecRamGb:8, MinVramGb:4, RecVramGb:8, Category:"Pesado"),

        new(["FORZA HORIZON 5", "FORZA HORIZON", "FORZA"],
            MinGpu:20, RecGpu:38, HighGpu:55, UltraGpu:72,
            MinRamGb:8, RecRamGb:16, MinVramGb:4, RecVramGb:8, Category:"Pesado"),

        new(["CONTROL"],
            MinGpu:22, RecGpu:38, HighGpu:55, UltraGpu:70,
            MinRamGb:8, RecRamGb:16, MinVramGb:4, RecVramGb:8, Category:"Pesado"),

        new(["BATTLEFIELD 2042", "BATTLEFIELD"],
            MinGpu:22, RecGpu:40, HighGpu:58, UltraGpu:75,
            MinRamGb:8, RecRamGb:16, MinVramGb:4, RecVramGb:8, Category:"Pesado"),

        new(["STARFIELD"],
            MinGpu:25, RecGpu:45, HighGpu:62, UltraGpu:78,
            MinRamGb:16, RecRamGb:16, MinVramGb:8, RecVramGb:12, Category:"Pesado"),

        new(["HOGWARTS LEGACY", "HOGWARTS"],
            MinGpu:25, RecGpu:45, HighGpu:62, UltraGpu:78,
            MinRamGb:8, RecRamGb:16, MinVramGb:6, RecVramGb:8, Category:"Pesado"),

        new(["RED DEAD REDEMPTION 2", "RDR2", "RED DEAD"],
            MinGpu:25, RecGpu:45, HighGpu:62, UltraGpu:80,
            MinRamGb:8, RecRamGb:16, MinVramGb:4, RecVramGb:8, Category:"Pesado"),

        // ── Jogos Muito Pesados ───────────────────────────────────────────
        new(["CYBERPUNK 2077", "CYBERPUNK"],
            MinGpu:28, RecGpu:48, HighGpu:66, UltraGpu:86,
            MinRamGb:8, RecRamGb:16, MinVramGb:6, RecVramGb:12, Category:"Muito Pesado"),

        new(["ALAN WAKE 2", "ALAN WAKE"],
            MinGpu:30, RecGpu:52, HighGpu:68, UltraGpu:82,
            MinRamGb:16, RecRamGb:16, MinVramGb:8, RecVramGb:12, Category:"Muito Pesado"),

        new(["A PLAGUE TALE: REQUIEM", "A PLAGUE TALE", "PLAGUE TALE"],
            MinGpu:30, RecGpu:52, HighGpu:68, UltraGpu:82,
            MinRamGb:16, RecRamGb:16, MinVramGb:8, RecVramGb:12, Category:"Muito Pesado"),

        new(["THE LAST OF US", "LAST OF US"],
            MinGpu:25, RecGpu:48, HighGpu:65, UltraGpu:80,
            MinRamGb:8, RecRamGb:16, MinVramGb:4, RecVramGb:12, Category:"Muito Pesado"),

        new(["STAR WARS JEDI: SURVIVOR", "JEDI SURVIVOR", "STAR WARS JEDI"],
            MinGpu:28, RecGpu:48, HighGpu:65, UltraGpu:80,
            MinRamGb:8, RecRamGb:16, MinVramGb:8, RecVramGb:12, Category:"Muito Pesado"),

        new(["MICROSOFT FLIGHT SIMULATOR", "MSFS", "FLIGHT SIMULATOR"],
            MinGpu:35, RecGpu:55, HighGpu:72, UltraGpu:88,
            MinRamGb:16, RecRamGb:32, MinVramGb:8, RecVramGb:16, Category:"Muito Pesado"),

        new(["BLOODBORNE"],
            MinGpu:40, RecGpu:58, HighGpu:72, UltraGpu:85,
            MinRamGb:8, RecRamGb:16, MinVramGb:6, RecVramGb:12, Category:"Muito Pesado"),   // via emulação PS4
    ];

    // ─── API pública ──────────────────────────────────────────────────────

    /// <summary>
    /// Gera uma recomendação de configuração gráfica para o jogo e o perfil de hardware informados.
    /// </summary>
    public static GraphicsRecommendation Recommend(string gameName, PcHardwareProfile hw)
    {
        var entry = FindEntry(gameName);
        return entry is null
            ? BuildGenericRecommendation(gameName, hw)
            : BuildRecommendation(gameName, hw, entry);
    }

    // ─── Internos ─────────────────────────────────────────────────────────

    private static GameEntry? FindEntry(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName)) return null;
        var upper = gameName.ToUpperInvariant();
        foreach (var entry in GameDb)
            foreach (var pattern in entry.Names)
                if (upper.Contains(pattern))
                    return entry;
        return null;
    }

    private static GraphicsRecommendation BuildRecommendation(string gameName, PcHardwareProfile hw, GameEntry e)
    {
        var score = hw.GpuScore;
        var vram  = hw.GpuVramGb;
        var ram   = hw.RamGb;

        // Determinar preset com base no GPU score
        GraphicsPreset preset;
        int fps;

        if (score < e.MinGpu || (vram > 0 && vram < e.MinVramGb) || ram < e.MinRamGb - 2)
        {
            preset = GraphicsPreset.NãoRoda;
            fps    = 0;
        }
        else if (score < e.RecGpu || (vram > 0 && vram < e.RecVramGb - 2))
        {
            preset = GraphicsPreset.Baixo;
            fps    = EstimateFps(score, e.MinGpu, e.RecGpu, 25, 45);
        }
        else if (score < e.HighGpu)
        {
            preset = GraphicsPreset.Médio;
            fps    = EstimateFps(score, e.RecGpu, e.HighGpu, 45, 65);
        }
        else if (score < e.UltraGpu)
        {
            preset = GraphicsPreset.Alto;
            fps    = EstimateFps(score, e.HighGpu, e.UltraGpu, 55, 75);
        }
        else
        {
            preset = GraphicsPreset.Ultra;
            fps    = score >= e.UltraGpu + 15 ? 144 : (score >= e.UltraGpu + 5 ? 120 : 90);
        }

        // Arredondar fps para valores comuns
        fps = RoundFps(fps);

        var details = BuildDetails(hw, e, preset);
        var (color, icon) = PresetStyle(preset);
        var msg = BuildMessage(gameName, preset, fps, e.Category, hw);

        return new GraphicsRecommendation
        {
            GameName     = gameName,
            Preset       = preset,
            EstimatedFps = fps,
            ShortMessage = msg,
            PresetColor  = color,
            PresetIcon   = icon,
            Details      = details,
            PcProfile    = hw,
            IsGameKnown  = true
        };
    }

    private static GraphicsRecommendation BuildGenericRecommendation(string gameName, PcHardwareProfile hw)
    {
        // Sem dados do jogo — usa tier genérico da GPU
        var score = hw.GpuScore;
        GraphicsPreset preset;
        int fps;

        if (score < 10)      { preset = GraphicsPreset.NãoRoda; fps = 0; }
        else if (score < 22) { preset = GraphicsPreset.Baixo;   fps = 30; }
        else if (score < 40) { preset = GraphicsPreset.Médio;   fps = 45; }
        else if (score < 62) { preset = GraphicsPreset.Alto;    fps = 60; }
        else                 { preset = GraphicsPreset.Ultra;   fps = score >= 80 ? 120 : 90; }

        fps = RoundFps(fps);

        var details = new List<string>
        {
            $"GPU: {hw.GpuDisplay} (score {score}/100)",
            $"RAM: {hw.RamDisplay}",
            hw.GpuVramGb > 0 ? $"VRAM: {hw.GpuVramGb} GB" : "",
            "",
            "⚠️ Jogo não encontrado no banco de dados.",
            "Recomendação baseada apenas no desempenho geral da GPU.",
            "Consulte os requisitos oficiais do jogo para confirmação."
        };
        details.RemoveAll(string.IsNullOrEmpty);

        var (color, icon) = PresetStyle(preset);
        var msg = preset == GraphicsPreset.NãoRoda
            ? $"Seu PC pode ter dificuldades para rodar {gameName} — GPU com score baixo"
            : $"Seu PC deve rodar {gameName} em {PresetLabel(preset)} a ~{fps}fps (estimativa geral)";

        return new GraphicsRecommendation
        {
            GameName     = gameName,
            Preset       = preset,
            EstimatedFps = fps,
            ShortMessage = msg,
            PresetColor  = color,
            PresetIcon   = icon,
            Details      = details,
            PcProfile    = hw,
            IsGameKnown  = false
        };
    }

    private static List<string> BuildDetails(PcHardwareProfile hw, GameEntry e, GraphicsPreset preset)
    {
        var list = new List<string>();

        list.Add($"GPU: {hw.GpuDisplay}  |  Score: {hw.GpuScore}/100");

        if (hw.GpuVramGb > 0)
        {
            var vramOk = hw.GpuVramGb >= e.RecVramGb;
            var vramSuffix = vramOk ? "✓" : $"(recomendado: {e.RecVramGb} GB)";
            list.Add($"VRAM: {hw.GpuVramGb} GB {vramSuffix}");
        }

        var ramOk = hw.RamGb >= e.RecRamGb;
        var ramSuffix = ramOk ? "✓" : $"(recomendado: {e.RecRamGb:F0} GB)";
        list.Add($"RAM: {hw.RamDisplay} {ramSuffix}");

        if (!string.IsNullOrEmpty(hw.CpuDisplay))
            list.Add($"CPU: {hw.CpuDisplay}");

        list.Add("");

        // Dicas específicas por preset
        list.Add(preset switch
        {
            GraphicsPreset.NãoRoda => "❌ Seu PC não atinge os requisitos mínimos para este jogo.",
            GraphicsPreset.Baixo   => "💡 Reduza resolução para 1080p e use texturas Baixas para atingir 30+ fps.",
            GraphicsPreset.Médio   => "💡 Configure resolução 1080p com qualidade Média para ~60fps estável.",
            GraphicsPreset.Alto    => "🎮 Seu PC está preparado para 1080p/1440p em Alto com bom desempenho.",
            GraphicsPreset.Ultra   => "🏆 Seu PC consegue rodar este jogo em Ultra — explore ray tracing e DLSS/FSR!",
            _                      => ""
        });

        // DLSS/FSR hint
        if (preset is GraphicsPreset.Médio or GraphicsPreset.Alto)
        {
            if (hw.GpuName.Contains("RTX", StringComparison.OrdinalIgnoreCase))
                list.Add("💡 Ative DLSS Quality para ganhos de fps sem perda visual.");
            else if (hw.GpuName.Contains("AMD") || hw.GpuName.Contains("RX "))
                list.Add("💡 Ative AMD FSR Quality para ganho de desempenho.");
        }

        // RAM insuficiente
        if (hw.RamGb > 0 && hw.RamGb < e.MinRamGb)
            list.Add($"⚠️ RAM insuficiente ({hw.RamDisplay}) — mínimo é {e.MinRamGb:F0} GB.");
        else if (hw.RamGb > 0 && hw.RamGb < e.RecRamGb)
            list.Add($"💡 Mais RAM ({e.RecRamGb:F0} GB) melhoraria o carregamento de texturas.");

        list.RemoveAll(string.IsNullOrEmpty);
        return list;
    }

    private static string BuildMessage(string gameName, GraphicsPreset preset, int fps, string category, PcHardwareProfile hw)
    {
        var shortName = gameName.Length > 22 ? gameName[..19] + "…" : gameName;

        return preset switch
        {
            GraphicsPreset.NãoRoda => $"Seu PC não consegue rodar {shortName} — GPU abaixo do mínimo",
            GraphicsPreset.Ultra   => fps >= 120
                ? $"Seu PC roda {shortName} em Ultra a ~{fps}fps! 🏆"
                : $"Seu PC roda {shortName} em Ultra a ~{fps}fps",
            _ => $"Seu PC roda {shortName} em {PresetLabel(preset)} a ~{fps}fps"
        };
    }

    private static int EstimateFps(int score, int low, int high, int fpsLow, int fpsHigh)
    {
        if (high <= low) return fpsLow;
        var t = Math.Clamp((double)(score - low) / (high - low), 0, 1);
        return (int)Math.Round(fpsLow + t * (fpsHigh - fpsLow));
    }

    private static int RoundFps(int fps) => fps switch
    {
        0           => 0,
        <= 24       => 24,
        <= 32       => 30,
        <= 42       => 40,
        <= 52       => 45,
        <= 68       => 60,
        <= 80       => 75,
        <= 100      => 90,
        <= 130      => 120,
        _           => 144
    };

    private static string PresetLabel(GraphicsPreset p) => p switch
    {
        GraphicsPreset.Baixo => "Baixo",
        GraphicsPreset.Médio => "Médio",
        GraphicsPreset.Alto  => "Alto",
        GraphicsPreset.Ultra => "Ultra",
        _                    => "?"
    };

    private static (string color, string icon) PresetStyle(GraphicsPreset p) => p switch
    {
        GraphicsPreset.NãoRoda => ("#FF5252", "AlertCircleOutline"),
        GraphicsPreset.Baixo   => ("#FFD740", "SpeedometerSlow"),
        GraphicsPreset.Médio   => ("#4FC3F7", "Speedometer"),
        GraphicsPreset.Alto    => ("#69F0AE", "SpeedometerMedium"),
        GraphicsPreset.Ultra   => ("#FF9800", "RocketLaunchOutline"),
        _                      => ("#AAAAAA", "HelpCircleOutline")
    };
}
