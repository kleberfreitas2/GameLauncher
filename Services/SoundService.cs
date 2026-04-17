using System.IO;
using System.Media;

namespace GameLauncher.Services;

public static class SoundService
{
    private static SoundPlayer? _navigate;
    private static SoundPlayer? _select;
    private static SoundPlayer? _launch;
    private static SoundPlayer? _back;
    private static SoundPlayer? _error;
    private static SoundPlayer? _favorite;
    private static SoundPlayer? _trophy;
    private static SoundPlayer? _zoneChange;
    private static SoundPlayer? _bigPicture;

    private static bool _initialized;

    public static bool IsEnabled => SettingsService.Current.SoundEnabled;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _navigate    = CreatePlayer(GenerateTone(800, 0.04, 0.25));
        _select      = CreatePlayer(GenerateTone(1200, 0.08, 0.30));
        _launch      = CreatePlayer(GenerateSweep(600, 1100, 0.18, 0.35));
        _back        = CreatePlayer(GenerateSweep(500, 300, 0.08, 0.20));
        _error       = CreatePlayer(GenerateTone(200, 0.12, 0.30));
        _favorite    = CreatePlayer(GenerateSweep(880, 1320, 0.10, 0.25));
        _trophy      = CreatePlayer(GenerateTrophySound());
        _zoneChange  = CreatePlayer(GenerateTone(600, 0.03, 0.15));
        _bigPicture  = CreatePlayer(GenerateBigPictureEntry());
    }

    public static void PlayNavigate()      => Play(_navigate);
    public static void PlaySelect()        => Play(_select);
    public static void PlayLaunch()        => Play(_launch);
    public static void PlayBack()          => Play(_back);
    public static void PlayError()         => Play(_error);
    public static void PlayFavorite()      => Play(_favorite);
    public static void PlayTrophy()        => Play(_trophy);
    public static void PlayZoneChange()    => Play(_zoneChange);
    public static void PlayBigPicture()    => Play(_bigPicture);

    private static void Play(SoundPlayer? player)
    {
        if (!IsEnabled || player is null) return;
        try { player.Play(); } catch { /* non-critical */ }
    }

    private static SoundPlayer CreatePlayer(byte[] wav)
    {
        var ms = new MemoryStream(wav);
        var player = new SoundPlayer(ms);
        player.Load();
        return player;
    }


    private const int SampleRate = 44100;
    private const int BitsPerSample = 16;
    private const int Channels = 1;

    private static byte[] GenerateTone(double frequency, double durationSec, double volume)
    {
        int sampleCount = (int)(SampleRate * durationSec);
        var samples = new short[sampleCount];
        double maxAmplitude = short.MaxValue * Math.Clamp(volume, 0, 1);

        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / SampleRate;
            double envelope = 1.0 - ((double)i / sampleCount); // linear fade-out
            envelope *= envelope; // quadratic fade for smoother decay
            double sample = Math.Sin(2.0 * Math.PI * frequency * t) * maxAmplitude * envelope;
            samples[i] = (short)Math.Clamp(sample, short.MinValue, short.MaxValue);
        }

        return BuildWav(samples);
    }

    /// <summary>
    /// Som estilo troféu do Xbox: dois "dings" ascendentes com harmônicos
    /// e decay suave — inconfundível ao desbloquear um achievement.
    /// </summary>
    private static byte[] GenerateTrophySound()
    {
        // Duração total: ~1.1s
        // Nota 1 (Dó): 0.00 – 0.45s  (659 Hz ≈ Mi5)
        // Nota 2 (Mi): 0.28 – 0.75s  (880 Hz = Lá5) — levemente sobreposta
        // Brilho (Sol): 0.55 – 1.10s (1319 Hz ≈ Mi6) — harmônico final
        double totalDuration = 1.15;
        int totalSamples = (int)(SampleRate * totalDuration);
        var mix = new double[totalSamples];

        // Adiciona um "ding" com envelope típico de sino:
        //  - ataque muito rápido (2 ms)
        //  - sustain curto
        //  - decay exponencial longo
        void AddBell(double startSec, double durationSec, double freq, double vol, params double[] harmonics)
        {
            int s0 = (int)(startSec * SampleRate);
            int len = (int)(durationSec * SampleRate);
            int s1 = Math.Min(s0 + len, totalSamples);

            double attackSamples = SampleRate * 0.003; // 3ms de ataque

            for (int i = s0; i < s1; i++)
            {
                double progress = (double)(i - s0) / len;
                // Envelope sino: ataque rápido + decay exponencial
                double env = progress < (attackSamples / len)
                    ? progress / (attackSamples / len)
                    : Math.Exp(-5.0 * (progress - (attackSamples / len)));

                double t = (double)(i - s0) / SampleRate;
                double sample = Math.Sin(2.0 * Math.PI * freq * t) * vol * env;

                // Harmônicos (dão o caráter metálico/cristalino do sino)
                for (int h = 0; h < harmonics.Length; h++)
                {
                    double hVol = vol * (0.35 / (h + 2));
                    sample += Math.Sin(2.0 * Math.PI * freq * harmonics[h] * t) * hVol * env;
                }

                mix[i] += sample;
            }
        }

        // Nota 1 — Mi5 (659 Hz) com harmônicos 2ª, 3ª e 5ª
        AddBell(0.00, 0.50, 659.25, 0.55, 2.0, 3.0, 5.0);

        // Nota 2 — Si5 (987 Hz) — um intervalo de terça-maior acima
        AddBell(0.30, 0.55, 987.77, 0.50, 2.0, 3.0, 5.0);

        // Brilho final — Ré6 (1174 Hz) — completa o acorde de forma etérea
        AddBell(0.55, 0.60, 1174.66, 0.30, 2.0, 4.0);

        // Normalizar
        double maxVal = mix.Max(Math.Abs);
        if (maxVal < 1e-9) maxVal = 1;
        double normalize = short.MaxValue * 0.80 / maxVal;
        var samples = new short[totalSamples];
        for (int i = 0; i < totalSamples; i++)
            samples[i] = (short)Math.Clamp(mix[i] * normalize, short.MinValue, short.MaxValue);

        return BuildWav(samples);
    }

    private static byte[] GenerateBigPictureEntry()
    {
        // Som cinematográfico em 3 camadas:
        // 1) Impacto grave + sweep ascendente (0.00–0.18s)
        // 2) Acorde médio sustentado (0.10–0.40s)
        // 3) Brilho agudo suave (0.30–0.55s)
        double totalDuration = 0.55;
        int totalSamples = (int)(SampleRate * totalDuration);
        var mix = new double[totalSamples];

        void AddSweep(double t0, double t1, double f0, double f1, double vol, bool exp = false)
        {
            int s0 = (int)(t0 * SampleRate);
            int s1 = Math.Min((int)(t1 * SampleRate), totalSamples);
            double duration = t1 - t0;
            double phase = 0;
            for (int i = s0; i < s1; i++)
            {
                double progress = (double)(i - s0) / (s1 - s0);
                double freq = exp
                    ? f0 * Math.Pow(f1 / f0, progress)
                    : f0 + (f1 - f0) * progress;
                double env = Math.Sin(Math.PI * progress); // bell envelope
                phase += 2.0 * Math.PI * freq / SampleRate;
                mix[i] += Math.Sin(phase) * vol * env;
            }
        }

        void AddTone(double t0, double t1, double freq, double vol)
        {
            int s0 = (int)(t0 * SampleRate);
            int s1 = Math.Min((int)(t1 * SampleRate), totalSamples);
            double phase = 0;
            for (int i = s0; i < s1; i++)
            {
                double progress = (double)(i - s0) / (s1 - s0);
                // attack + sustain + decay
                double env = progress < 0.1 ? progress / 0.1
                           : progress > 0.7 ? (1.0 - progress) / 0.3
                           : 1.0;
                env *= env;
                phase += 2.0 * Math.PI * freq / SampleRate;
                mix[i] += Math.Sin(phase) * vol * env;
            }
        }

        // Camada 1 — Impacto grave exponencial ascendente
        AddSweep(0.00, 0.22, 80,  420,  0.50, exp: true);
        // Camada 2 — Sweep médio cinematográfico
        AddSweep(0.05, 0.40, 320, 860,  0.40, exp: false);
        // Camada 3 — Acorde sustentado (quinta justa)
        AddTone (0.12, 0.50, 660, 0.30);
        AddTone (0.12, 0.50, 990, 0.18);
        // Camada 4 — Brilho final suave (harmônico agudo)
        AddSweep(0.32, 0.55, 1200, 1800, 0.20, exp: false);

        // Normalizar e converter
        double max = mix.Max(Math.Abs);
        if (max < 1e-6) max = 1;
        var samples = new short[totalSamples];
        double normalize = short.MaxValue * 0.85 / max;
        for (int i = 0; i < totalSamples; i++)
            samples[i] = (short)Math.Clamp(mix[i] * normalize, short.MinValue, short.MaxValue);

        return BuildWav(samples);
    }

    private static byte[] GenerateSweep(double startFreq, double endFreq, double durationSec, double volume)
    {
        int sampleCount = (int)(SampleRate * durationSec);
        var samples = new short[sampleCount];
        double maxAmplitude = short.MaxValue * Math.Clamp(volume, 0, 1);
        double phase = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            double progress = (double)i / sampleCount;
            double freq = startFreq + (endFreq - startFreq) * progress;
            double envelope = 1.0 - progress;
            envelope *= envelope;
            phase += 2.0 * Math.PI * freq / SampleRate;
            double sample = Math.Sin(phase) * maxAmplitude * envelope;
            samples[i] = (short)Math.Clamp(sample, short.MinValue, short.MaxValue);
        }

        return BuildWav(samples);
    }

    private static byte[] BuildWav(short[] samples)
    {
        int byteRate = SampleRate * Channels * BitsPerSample / 8;
        int blockAlign = Channels * BitsPerSample / 8;
        int dataSize = samples.Length * blockAlign;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write("RIFF"u8);
        bw.Write(36 + dataSize);
        bw.Write("WAVE"u8);

        bw.Write("fmt "u8);
        bw.Write(16);              // sub-chunk size
        bw.Write((short)1);       // PCM format
        bw.Write((short)Channels);
        bw.Write(SampleRate);
        bw.Write(byteRate);
        bw.Write((short)blockAlign);
        bw.Write((short)BitsPerSample);

        bw.Write("data"u8);
        bw.Write(dataSize);
        foreach (var s in samples)
            bw.Write(s);

        bw.Flush();
        return ms.ToArray();
    }
}
