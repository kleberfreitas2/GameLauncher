using System.IO;
using System.Media;
using GameLauncher.Models;

namespace GameLauncher.Services;

/// <summary>
/// Console-style sound effects generated programmatically (no external files).
/// All tones are synthesized as PCM WAV in memory and cached for instant playback.
/// </summary>
public static class SoundService
{
    private static SoundPlayer? _navigate;
    private static SoundPlayer? _select;
    private static SoundPlayer? _launch;
    private static SoundPlayer? _back;
    private static SoundPlayer? _error;
    private static SoundPlayer? _favorite;
    private static SoundPlayer? _zoneChange;

    private static bool _initialized;

    public static bool IsEnabled => SettingsService.Current.SoundEnabled;

    /// <summary>
    /// Pre-generate and cache all sound effects. Call once at startup.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _navigate   = CreatePlayer(GenerateTone(800, 0.04, 0.25));
        _select     = CreatePlayer(GenerateTone(1200, 0.08, 0.30));
        _launch     = CreatePlayer(GenerateSweep(600, 1100, 0.18, 0.35));
        _back       = CreatePlayer(GenerateSweep(500, 300, 0.08, 0.20));
        _error      = CreatePlayer(GenerateTone(200, 0.12, 0.30));
        _favorite   = CreatePlayer(GenerateSweep(880, 1320, 0.10, 0.25));
        _zoneChange = CreatePlayer(GenerateTone(600, 0.03, 0.15));
    }

    public static void PlayNavigate()   => Play(_navigate);
    public static void PlaySelect()     => Play(_select);
    public static void PlayLaunch()     => Play(_launch);
    public static void PlayBack()       => Play(_back);
    public static void PlayError()      => Play(_error);
    public static void PlayFavorite()   => Play(_favorite);
    public static void PlayZoneChange() => Play(_zoneChange);

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

    // ── PCM WAV generation ──────────────────────────────────────

    private const int SampleRate = 44100;
    private const int BitsPerSample = 16;
    private const int Channels = 1;

    /// <summary>Generate a single-frequency tone with fade-out envelope.</summary>
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

    /// <summary>Generate a frequency sweep (ascending or descending) with fade-out.</summary>
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

    /// <summary>Wrap raw PCM samples into a valid WAV byte array.</summary>
    private static byte[] BuildWav(short[] samples)
    {
        int byteRate = SampleRate * Channels * BitsPerSample / 8;
        int blockAlign = Channels * BitsPerSample / 8;
        int dataSize = samples.Length * blockAlign;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // RIFF header
        bw.Write("RIFF"u8);
        bw.Write(36 + dataSize);
        bw.Write("WAVE"u8);

        // fmt sub-chunk
        bw.Write("fmt "u8);
        bw.Write(16);              // sub-chunk size
        bw.Write((short)1);       // PCM format
        bw.Write((short)Channels);
        bw.Write(SampleRate);
        bw.Write(byteRate);
        bw.Write((short)blockAlign);
        bw.Write((short)BitsPerSample);

        // data sub-chunk
        bw.Write("data"u8);
        bw.Write(dataSize);
        foreach (var s in samples)
            bw.Write(s);

        bw.Flush();
        return ms.ToArray();
    }
}
