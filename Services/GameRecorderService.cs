using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Management;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using GameLauncher.Views;
using Microsoft.Win32;
using NAudio.Wave;

namespace GameLauncher.Services;

public enum RecordingResolution
{
    HD_720p,
    FHD_1080p,
    UHD_4K
}

public enum RecordingMode
{
    ScreenOnly,
    Microphone,
    FacecamMic
}

public enum FacecamPosition
{
    TopRight,
    TopLeft,
    BottomRight,
    BottomLeft
}

public sealed class GameRecorderService : IDisposable
{
    private static readonly string FfmpegDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameLauncher", "ffmpeg");

    private static readonly string FfmpegExe = Path.Combine(FfmpegDir, "ffmpeg.exe");

    private static readonly string OutputDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        "GLauncher Videos");

    private Process? _ffmpegProcess;
    private string? _currentOutputFile;
    private RecordingResolution _currentResolution;
    private string _lastFfmpegError = string.Empty;
    private volatile bool _stoppingManually;
    private volatile bool _pendingRetry;
    private static string? _cachedEncoder;
    private static bool? _ddagrabAvailable;
    private static int? _cachedRefreshRate;
    private FacecamOverlayWindow? _facecamWindow;

    // WASAPI loopback capture (game audio)
    private WasapiLoopbackCapture? _loopbackCapture;
    private NamedPipeServerStream? _loopbackPipe;
    private string? _loopbackPipeName;
    private string? _loopbackFfmpegInput;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    public bool IsRecording => _ffmpegProcess is not null && !_ffmpegProcess.HasExited;
    public string? CurrentFile => _currentOutputFile;

    public event Action<bool>? RecordingStateChanged;
    public event Action<string>? StatusMessage;

    public static bool IsFfmpegAvailable() => File.Exists(FfmpegExe);

    public static async Task<bool> EnsureFfmpegAsync(Action<string>? progress = null)
    {
        if (File.Exists(FfmpegExe))
            return true;

        try
        {
            Directory.CreateDirectory(FfmpegDir);

            progress?.Invoke("Baixando FFmpeg...");

            const string url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromMinutes(5);

            var zipPath = Path.Combine(FfmpegDir, "ffmpeg.zip");
            using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var fs = new FileStream(zipPath, FileMode.Create);
                await response.Content.CopyToAsync(fs);
            }

            progress?.Invoke("Extraindo FFmpeg...");

            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var ffmpegEntry = zip.Entries
                    .FirstOrDefault(e => e.FullName.EndsWith("bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase));

                if (ffmpegEntry is null)
                    return false;

                ffmpegEntry.ExtractToFile(FfmpegExe, overwrite: true);
            }

            try { File.Delete(zipPath); } catch { }

            progress?.Invoke("FFmpeg pronto!");
            return File.Exists(FfmpegExe);
        }
        catch (Exception ex)
        {
            progress?.Invoke($"Erro ao baixar FFmpeg: {ex.Message}");
            return false;
        }
    }

    public static List<string> ListVideoDevices()
    {
        var devices = ListDshowDevices("video");
        if (devices.Count == 0)
            devices = ListCamerasViaWmi();
        return devices;
    }

    public static List<string> ListAudioDevices()
    {
        var devices = ListDshowDevices("audio");
        if (devices.Count == 0)
            devices = ListMicrophonesViaRegistry();
        return devices;
    }

    private static List<string> ListDshowDevices(string type)
    {
        if (!File.Exists(FfmpegExe)) return [];
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = FfmpegExe,
                Arguments = "-list_devices true -f dshow -i dummy",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            });
            var stderr = proc?.StandardError.ReadToEnd() ?? "";
            proc?.WaitForExit(5000);

            var devices = new List<string>();
            bool inSection = false;
            foreach (var line in stderr.Split('\n'))
            {
                if (line.Contains($"DirectShow {type} devices"))
                { inSection = true; continue; }
                if (inSection && line.Contains("DirectShow") && !line.Contains($"{type} devices"))
                    break;
                if (inSection)
                {
                    var m = Regex.Match(line, "\"(.+?)\"");
                    if (m.Success && !line.Contains("Alternative name"))
                        devices.Add(m.Groups[1].Value);
                }
            }
            return devices;
        }
        catch { return []; }
    }

    private static List<string> ListCamerasViaWmi()
    {
        var devices = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Caption FROM Win32_PnPEntity WHERE PNPClass = 'Camera' AND Status = 'OK'");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Caption"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                    devices.Add(name);
            }
        }
        catch { }
        return devices;
    }

    private static List<string> ListMicrophonesViaRegistry()
    {
        var devices = new List<string>();
        string? cachedAdapter = null;
        bool adapterResolved = false;
        try
        {
            using var captureKey = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture");
            if (captureKey is null) return devices;

            foreach (var subKeyName in captureKey.GetSubKeyNames())
            {
                try
                {
                    using var deviceKey = captureKey.OpenSubKey(subKeyName);
                    if (deviceKey is null) continue;

                    var state = deviceKey.GetValue("DeviceState");
                    if (state is not int stateVal || stateVal != 1) continue;

                    using var propsKey = deviceKey.OpenSubKey("Properties");
                    if (propsKey is null) continue;

                    // Prefer full device interface friendly name (includes adapter suffix)
                    // e.g., "Microfone (Realtek Audio)" — matches dshow device name format
                    var fullName = propsKey.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6")?.ToString();
                    if (!string.IsNullOrEmpty(fullName) && fullName.Contains('('))
                    {
                        devices.Add(fullName);
                        Debug.WriteLine($"[Devices] Registry mic (full): '{fullName}'");
                        continue;
                    }

                    // Fallback to endpoint friendly name (may be just "Microfone" without adapter)
                    var name = propsKey.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2")?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        // Build full dshow name if endpoint-only (e.g. "Microfone" → "Microfone (Realtek Audio)")
                        if (!name.Contains('('))
                        {
                            // Use per-device adapter from {b3f8fa53...},6 when available
                            // (e.g. USB headsets: fullName = "Jabra EVOLVE 20 MS" without parens)
                            if (!string.IsNullOrEmpty(fullName))
                            {
                                var built = $"{name} ({fullName})";
                                devices.Add(built);
                                Debug.WriteLine($"[Devices] Registry mic (device-specific adapter): '{built}'");
                                continue;
                            }

                            // Global fallback — use WMI physical audio adapter name
                            if (!adapterResolved)
                            {
                                cachedAdapter = GetPhysicalAudioAdapterName();
                                adapterResolved = true;
                            }
                            if (cachedAdapter is not null)
                            {
                                var built = $"{name} ({cachedAdapter})";
                                devices.Add(built);
                                Debug.WriteLine($"[Devices] Registry mic (built): '{built}'");
                                continue;
                            }
                        }
                        devices.Add(name);
                        Debug.WriteLine($"[Devices] Registry mic (endpoint only): '{name}'");
                    }
                }
                catch { continue; }
            }
        }
        catch { }
        return devices;
    }

    private static string? ResolveDshowAudioDevice(string storedName)
    {
        var dshowDevices = ListDshowDevices("audio");
        Debug.WriteLine($"[Recording] ResolveDshowAudio: stored='{storedName}', dshow found={dshowDevices.Count}");

        if (dshowDevices.Count > 0)
        {
            foreach (var d in dshowDevices)
                Debug.WriteLine($"[Recording]   dshow audio: '{d}'");

            // Exact match
            if (dshowDevices.Any(d => d.Equals(storedName, StringComparison.OrdinalIgnoreCase)))
                return storedName;

            // Match ignoring ® / (R) / (TM) symbols (vary between driver versions)
            var cleanStored = CleanRegisteredSymbol(storedName);
            var cleanMatch = dshowDevices.FirstOrDefault(d =>
                CleanRegisteredSymbol(d).Equals(cleanStored, StringComparison.OrdinalIgnoreCase));
            if (cleanMatch is not null) return cleanMatch;

            // Partial match (stored name is part of dshow name or vice versa)
            var partial = dshowDevices.FirstOrDefault(d =>
                d.Contains(storedName, StringComparison.OrdinalIgnoreCase) ||
                storedName.Contains(d, StringComparison.OrdinalIgnoreCase));
            if (partial is not null) return partial;

            return dshowDevices[0];
        }

        // dshow listing failed — test stored name directly with FFmpeg
        Debug.WriteLine($"[Recording] Testing stored audio name: '{storedName}'");
        if (QuickTestDshowAudioDevice(storedName))
        {
            Debug.WriteLine($"[Recording] Stored audio name works: '{storedName}'");
            return storedName;
        }

        // Build candidate names (endpoint + adapter variants) and test each
        var candidates = BuildAudioDeviceCandidates(storedName);
        foreach (var candidate in candidates)
        {
            Debug.WriteLine($"[Recording] Testing audio candidate: '{candidate}'");
            if (QuickTestDshowAudioDevice(candidate))
            {
                Debug.WriteLine($"[Recording] Audio candidate works: '{candidate}'");
                return candidate;
            }
        }

        // No candidate verified — return stored name (retry-without-audio catches failures)
        Debug.WriteLine($"[Recording] No audio device name verified, using stored: '{storedName}'");
        return storedName;
    }

    private static string? ResolveDshowVideoDevice(string storedName)
    {
        var dshowDevices = ListDshowDevices("video");
        Debug.WriteLine($"[Recording] ResolveDshowVideo: stored='{storedName}', dshow found={dshowDevices.Count}");

        if (dshowDevices.Count > 0)
        {
            foreach (var d in dshowDevices)
                Debug.WriteLine($"[Recording]   dshow video: '{d}'");

            if (dshowDevices.Any(d => d.Equals(storedName, StringComparison.OrdinalIgnoreCase)))
                return storedName;

            var partial = dshowDevices.FirstOrDefault(d =>
                d.Contains(storedName, StringComparison.OrdinalIgnoreCase) ||
                storedName.Contains(d, StringComparison.OrdinalIgnoreCase));
            if (partial is not null) return partial;

            return dshowDevices[0];
        }

        // dshow listing failed — camera names from WMI typically match dshow exactly
        Debug.WriteLine($"[Recording] Using stored video name as-is: '{storedName}'");
        return storedName;
    }

    private static string? GetPhysicalAudioAdapterName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_SoundDevice");
            var adapters = new List<string>();
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                    adapters.Add(name);
            }

            Debug.WriteLine($"[Recording] Audio adapters (WMI): [{string.Join(", ", adapters)}]");

            // Filter out HDMI/display/virtual audio — keep physical adapters (Realtek, Conexant, IDT)
            var physical = adapters.Where(a =>
                !a.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) &&
                !a.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                !(a.Contains("Intel", StringComparison.OrdinalIgnoreCase) &&
                  (a.Contains("Display", StringComparison.OrdinalIgnoreCase) ||
                   a.Contains("tela", StringComparison.OrdinalIgnoreCase) ||
                   a.Contains("HDMI", StringComparison.OrdinalIgnoreCase))))
                .ToList();

            return physical.Count > 0 ? physical[0] : (adapters.Count > 0 ? adapters[0] : null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Recording] GetPhysicalAudioAdapterName error: {ex.Message}");
            return null;
        }
    }

    private static List<string> BuildAudioDeviceCandidates(string endpointName)
    {
        var candidates = new List<string>();

        // If name already has adapter suffix, generate clean variants
        if (endpointName.Contains('(') && endpointName.EndsWith(')'))
        {
            candidates.Add(endpointName);
            var cleaned = CleanRegisteredSymbol(endpointName);
            if (cleaned != endpointName)
                candidates.Add(cleaned);
            return candidates;
        }

        var adapter = GetPhysicalAudioAdapterName();
        if (adapter is not null)
        {
            // If stored name IS the adapter name (e.g., "Realtek Audio"),
            // try common endpoint prefixes used by Windows in different languages
            if (endpointName.Equals(adapter, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var prefix in new[] { "Microfone", "Microphone", "Mic", "Mikrofon",
                    "Linha de entrada", "Line In", "Mixagem est\u00e9reo", "Stereo Mix" })
                {
                    candidates.Add($"{prefix} ({adapter})");
                }
                var cleanAdapter = CleanRegisteredSymbol(adapter);
                if (cleanAdapter != adapter)
                {
                    foreach (var prefix in new[] { "Microfone", "Microphone", "Mic" })
                        candidates.Add($"{prefix} ({cleanAdapter})");
                }
            }
            else
            {
                var candidate = $"{endpointName} ({adapter})";
                candidates.Add(candidate);

                var cleanAdapter = CleanRegisteredSymbol(adapter);
                if (cleanAdapter != adapter)
                    candidates.Add($"{endpointName} ({cleanAdapter})");
            }
        }

        return candidates;
    }

    private static bool QuickTestDshowAudioDevice(string deviceName)
    {
        if (!File.Exists(FfmpegExe)) return false;
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FfmpegExe,
                    Arguments = $"-f dshow -i audio=\"{deviceName}\" -t 0.1 -f null - -y -loglevel quiet",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            if (!proc.WaitForExit(4000))
            {
                // Still running after 4s = device is valid (actively capturing audio)
                try { proc.Kill(); } catch { }
                return true;
            }
            Debug.WriteLine($"[Recording] QuickTest audio='{deviceName}' \u2192 exit {proc.ExitCode}");
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    private static string CleanRegisteredSymbol(string name)
    {
        var cleaned = name.Replace("(R)", "").Replace("(r)", "").Replace("(TM)", "")
                          .Replace("\u00ae", "").Replace("\u2122", "");
        return Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
    }

    public static bool HasCameraHardware()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Caption FROM Win32_PnPEntity WHERE PNPClass = 'Camera' AND Status = 'OK'");
            return searcher.Get().Count > 0;
        }
        catch { return false; }
    }

    public static bool HasMicrophoneHardware()
    {
        try
        {
            using var captureKey = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture");
            if (captureKey is null) return false;
            foreach (var subKeyName in captureKey.GetSubKeyNames())
            {
                using var deviceKey = captureKey.OpenSubKey(subKeyName);
                if (deviceKey is null) continue;
                var state = deviceKey.GetValue("DeviceState");
                if (state is int stateVal && stateVal == 1) return true;
            }
            return false;
        }
        catch { return false; }
    }

    public bool StartRecording(RecordingResolution resolution, RecordingMode mode = RecordingMode.ScreenOnly,
        FacecamPosition facecamPos = FacecamPosition.TopRight,
        string? webcamDevice = null, string? micDevice = null, string? gameName = null)
    {
        if (IsRecording) return false;
        if (!File.Exists(FfmpegExe)) return false;

        if (_ffmpegProcess is not null)
        {
            try { _ffmpegProcess.Dispose(); } catch { }
            _ffmpegProcess = null;
        }

        _stoppingManually = false;
        _lastFfmpegError = string.Empty;
        _currentResolution = resolution;
        Directory.CreateDirectory(OutputDir);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var safeName = string.IsNullOrEmpty(gameName) ? "Recording" : SanitizeFileName(gameName);
        // Gravar em MKV (VFR nativo, tolerante a falhas) — remux para MP4 ao parar
        _currentOutputFile = Path.Combine(OutputDir, $"{safeName}_{timestamp}.mkv");

        bool hasMic = mode != RecordingMode.ScreenOnly && !string.IsNullOrEmpty(micDevice);
        bool hasCam = mode == RecordingMode.FacecamMic && !string.IsNullOrEmpty(webcamDevice);

        // Resolve mic device name via dshow before recording
        if (hasMic)
        {
            var resolvedMic = ResolveDshowAudioDevice(micDevice!);
            if (resolvedMic is null)
            {
                StatusMessage?.Invoke("⚠ Microfone não acessível via DirectShow. Verifique as permissões de privacidade do Windows. Gravando apenas a tela.");
                hasMic = false;
            }
            else
            {
                micDevice = resolvedMic;
            }
        }

        // Resolve webcam device name via dshow before starting facecam
        if (hasCam)
        {
            var resolvedCam = ResolveDshowVideoDevice(webcamDevice!);
            if (resolvedCam is null)
            {
                StatusMessage?.Invoke("⚠ Câmera não acessível via DirectShow. Verifique as permissões de privacidade do Windows.");
                hasCam = false;
            }
            else
            {
                webcamDevice = resolvedCam;
            }
        }

        // Capture game audio via WASAPI loopback (captures ALL system audio output —
        // works with USB headsets, HDMI, Bluetooth, onboard — no Stereo Mix needed)
        bool hasLoopback = StartLoopbackCapture();
        if (!hasLoopback)
        {
            Debug.WriteLine("[Recording] WASAPI loopback unavailable — game audio will not be captured");
            StatusMessage?.Invoke("⚠ Não foi possível capturar áudio do jogo (WASAPI loopback falhou).");
        }

        Debug.WriteLine($"[Recording] Config: mode={mode}, hasMic={hasMic}, hasCam={hasCam}, hasLoopback={hasLoopback}");
        var args = BuildFfmpegArgs(resolution, hasMic, micDevice, hasLoopback ? _loopbackFfmpegInput : null);
        Debug.WriteLine($"[Recording] FFmpeg args: {args}");
        _pendingRetry = hasMic || hasLoopback;

        try
        {
            _ffmpegProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FfmpegExe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            _ffmpegProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Debug.WriteLine($"[FFmpeg] {e.Data}");
                    _lastFfmpegError = e.Data;
                }
            };

            _ffmpegProcess.Exited += (_, _) =>
            {
                try
                {
                    // StopRecording already handles cleanup and state notification
                    if (_stoppingManually) return;

                    var proc = _ffmpegProcess;
                    int exitCode = -1;
                    try { exitCode = proc?.ExitCode ?? -1; } catch { }
                    bool crashed = exitCode != 0;

                    // If retry is pending, let the health monitor handle the failure
                    if (crashed && _pendingRetry)
                    {
                        Debug.WriteLine($"[Recording] FFmpeg exited (code {exitCode}) — retry pending");
                        return;
                    }

                    StopFacecamPreview();

                    if (crashed)
                    {
                        var errorDetail = !string.IsNullOrEmpty(_lastFfmpegError)
                            ? _lastFfmpegError
                            : "FFmpeg encerrou inesperadamente";
                        StatusMessage?.Invoke($"❌ Erro na gravação: {errorDetail}");
                    }

                    RecordingStateChanged?.Invoke(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Recording] Exited handler error: {ex}");
                    if (!_pendingRetry && !_stoppingManually)
                        RecordingStateChanged?.Invoke(false);
                }
            };

            // Start facecam overlay before gdigrab so it appears on screen
            if (hasCam)
                StartFacecamPreview(webcamDevice!, facecamPos);

            _ffmpegProcess.Start();
            _ffmpegProcess.BeginErrorReadLine();

            // Monitor de saúde: verifica se o processo caiu e se o arquivo está crescendo
            var hasMicForRetry = hasMic;
            var hasLoopbackForRetry = hasLoopback;
            var usedDdagrab = IsDdagrabAvailable();
            _ = Task.Run(async () =>
            {
                // Fase 1: Verificar se o processo caiu nos primeiros 2 segundos
                await Task.Delay(2000);
                _pendingRetry = false;
                try
                {
                    var proc = _ffmpegProcess;
                    if (proc is null || _stoppingManually) return;

                    if (proc.HasExited)
                    {
                        int exitCode = -1;
                        try { exitCode = proc.ExitCode; } catch { }
                        if (exitCode == 0) return;

                        // Se ddagrab causou a falha, desativar e tentar gdigrab
                        if (usedDdagrab)
                        {
                            _ddagrabAvailable = false;
                            Debug.WriteLine($"[Recording] ddagrab falhou (exit {exitCode}), tentando gdigrab...");
                            StatusMessage?.Invoke("⚠ Captura avançada falhou. Tentando método alternativo...");
                            RetryScreenOnly(withLoopback: hasLoopbackForRetry);
                            return;
                        }

                        if (hasMicForRetry || hasLoopbackForRetry)
                        {
                            Debug.WriteLine($"[Recording] FFmpeg caiu (exit {exitCode}), tentando sem mic (loopback={hasLoopbackForRetry})...");
                            StatusMessage?.Invoke(hasLoopbackForRetry
                                ? "⚠ Dispositivo falhou. Tentando novamente com áudio do jogo..."
                                : "⚠ Dispositivo de áudio falhou. Tentando novamente sem áudio...");
                            RetryScreenOnly(withLoopback: hasLoopbackForRetry);
                            return;
                        }

                        var errorDetail = !string.IsNullOrEmpty(_lastFfmpegError)
                            ? _lastFfmpegError
                            : "FFmpeg não conseguiu iniciar a gravação";
                        StatusMessage?.Invoke($"❌ Falha: {errorDetail}");
                        RecordingStateChanged?.Invoke(false);
                        return;
                    }

                    // Fase 2: Após 5s total, verificar se o arquivo está crescendo
                    await Task.Delay(3000);
                    proc = _ffmpegProcess;
                    if (proc is null || _stoppingManually) return;
                    if (proc.HasExited) return;

                    if (!string.IsNullOrEmpty(_currentOutputFile) && File.Exists(_currentOutputFile))
                    {
                        var fi = new FileInfo(_currentOutputFile);
                        if (fi.Length < 50 * 1024) // Menos de 50KB após 5s = encoder com problema
                        {
                            Debug.WriteLine($"[Recording] Arquivo muito pequeno ({fi.Length} bytes) após 5s — forçando libx264");
                            StatusMessage?.Invoke("⚠ Encoder com problema. Reiniciando com codificação por software...");
                            _cachedEncoder = null;
                            RetryScreenOnly(withLoopback: hasLoopbackForRetry, forceLibx264: true);
                        }
                    }
                }
                catch { }
            });

            RecordingStateChanged?.Invoke(true);
            var features = new List<string> { "tela" };
            if (hasLoopback) features.Add("áudio do jogo");
            if (hasMic) features.Add("microfone");
            if (hasCam) features.Add("facecam");
            StatusMessage?.Invoke($"🔴 Gravando ({string.Join(" + ", features)}): {Path.GetFileName(_currentOutputFile)}");
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke($"Erro ao iniciar gravação: {ex.Message}");
            StopFacecamPreview();
            _ffmpegProcess?.Dispose();
            _ffmpegProcess = null;
            return false;
        }
    }

    private static int GetDisplayRefreshRate()
    {
        if (_cachedRefreshRate.HasValue) return _cachedRefreshRate.Value;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT CurrentRefreshRate FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var rate = obj["CurrentRefreshRate"];
                if (rate is uint r && r > 30)
                {
                    Debug.WriteLine($"[Recording] Display refresh rate: {r}Hz");
                    _cachedRefreshRate = (int)r;
                    return (int)r;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Recording] GetDisplayRefreshRate error: {ex.Message}");
        }
        _cachedRefreshRate = 60;
        return 60;
    }

    /// <summary>
    /// Starts WASAPI loopback capture (captures ALL system audio output, regardless of device type).
    /// Audio is piped to ffmpeg via a named pipe. Returns true if successfully initialized.
    /// </summary>
    private bool StartLoopbackCapture()
    {
        StopLoopbackCapture();

        try
        {
            var capture = new WasapiLoopbackCapture();
            var wf = capture.WaveFormat;

            _loopbackPipeName = $"glauncher_audio_{Environment.ProcessId}";
            _loopbackPipe = new NamedPipeServerStream(_loopbackPipeName,
                PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 4 * 1024 * 1024);

            _loopbackCapture = capture;

            capture.DataAvailable += (_, e) =>
            {
                try
                {
                    if (_loopbackPipe is { IsConnected: true } && e.BytesRecorded > 0)
                        _loopbackPipe.Write(e.Buffer, 0, e.BytesRecorded);
                }
                catch { /* pipe closed or ffmpeg disconnected */ }
            };

            capture.RecordingStopped += (_, args) =>
            {
                if (args.Exception is not null)
                    Debug.WriteLine($"[Recording] WASAPI loopback error: {args.Exception.Message}");
                else
                    Debug.WriteLine("[Recording] WASAPI loopback stopped");
            };

            // Wait for ffmpeg to connect to the pipe, then start WASAPI capture
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_loopbackPipe is null) return;
                    await _loopbackPipe.WaitForConnectionAsync();
                    Debug.WriteLine("[Recording] Loopback pipe connected — starting WASAPI capture");
                    _loopbackCapture?.StartRecording();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Recording] Loopback pipe wait failed: {ex.Message}");
                }
            });

            // Build the ffmpeg input string for this pipe
            string fmt = wf.Encoding == WaveFormatEncoding.IeeeFloat ? "f32le" : $"s{wf.BitsPerSample}le";
            string pipePath = $@"\\.\pipe\{_loopbackPipeName}";
            _loopbackFfmpegInput = $"-f {fmt} -ar {wf.SampleRate} -ac {wf.Channels} -i \"{pipePath}\"";

            Debug.WriteLine($"[Recording] WASAPI loopback initialized: {wf.SampleRate}Hz, {wf.Channels}ch, {wf.BitsPerSample}bit {wf.Encoding}");
            Debug.WriteLine($"[Recording] Loopback ffmpeg input: {_loopbackFfmpegInput}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Recording] WASAPI loopback init failed: {ex.Message}");
            StopLoopbackCapture();
            return false;
        }
    }

    private void StopLoopbackCapture()
    {
        // Dispose pipe FIRST to unblock any Write() stuck in the DataAvailable handler.
        // WasapiLoopbackCapture.StopRecording() waits for the capture thread to finish,
        // but the capture thread may be blocked on pipe.Write() if the buffer is full.
        // Disposing the pipe causes Write() to throw (caught by empty catch), unblocking it.
        var pipe = _loopbackPipe;
        _loopbackPipe = null;
        _loopbackPipeName = null;
        _loopbackFfmpegInput = null;
        try { pipe?.Dispose(); } catch { }

        try { _loopbackCapture?.StopRecording(); } catch { }
        try { _loopbackCapture?.Dispose(); } catch { }
        _loopbackCapture = null;
    }

    private string BuildFfmpegArgs(RecordingResolution resolution, bool hasMic, string? micDevice,
        string? loopbackInput = null, bool forceLibx264 = false)
    {
        // Detectar taxa de atualização nativa do monitor para captura fluida
        // (60Hz, 75Hz, 120Hz, 144Hz — capturar na frequência nativa elimina frame skipping)
        int captureRate = Math.Clamp(GetDisplayRefreshRate(), 30, 120);
        int gopSize = captureRate * 2; // Keyframe a cada 2 segundos

        // Bitrate base a 60fps — escalar proporcionalmente para refresh rates mais altos
        var (baseBr, baseMax) = resolution switch
        {
            RecordingResolution.HD_720p => (8, 12),
            RecordingResolution.FHD_1080p => (12, 18),
            RecordingResolution.UHD_4K => (35, 50),
            _ => (12, 18)
        };

        double rateScale = captureRate / 60.0;
        var bitrate = $"{(int)Math.Ceiling(baseBr * rateScale)}M";
        var maxrate = $"{(int)Math.Ceiling(baseMax * rateScale)}M";

        var hwAccel = forceLibx264 ? "libx264" : DetectHardwareEncoder();

        var encoderArgs = hwAccel switch
        {
            "h264_nvenc" => $"-c:v h264_nvenc -preset p1 -tune ll -b:v {bitrate} -maxrate {maxrate} -bufsize {maxrate} -profile:v high -bf 0 -g {gopSize}",
            "h264_amf" => $"-c:v h264_amf -quality speed -b:v {bitrate} -maxrate {maxrate} -bufsize {maxrate} -profile:v high -bf 0 -g {gopSize}",
            "h264_qsv" => $"-c:v h264_qsv -preset fast -b:v {bitrate} -maxrate {maxrate} -bufsize {maxrate} -bf 0 -g {gopSize}",
            _ => $"-c:v libx264 -preset ultrafast -crf 23 -profile:v high -bf 0 -g {gopSize}"
        };

        // ddagrab captura jogos fullscreen via Desktop Duplication API; gdigrab é fallback
        bool useDdagrab = IsDdagrabAvailable();

        string inputs;
        if (useDdagrab)
        {
            inputs = $"-thread_queue_size 512 -probesize 32 -analyzeduration 0 -f ddagrab -framerate {captureRate} -i 0";
            Debug.WriteLine($"[Recording] Usando ddagrab (Desktop Duplication API) @ {captureRate}fps");
        }
        else
        {
            inputs = $"-f gdigrab -framerate {captureRate} -rtbufsize 512M -i desktop";
            Debug.WriteLine($"[Recording] Usando gdigrab @ {captureRate}fps");
        }

        Debug.WriteLine($"[Recording] Encoder: {hwAccel}, CaptureRate: {captureRate}fps, Bitrate: {bitrate}/{maxrate}, ForceLibx264: {forceLibx264}");

        int nextInput = 1;
        int loopbackIdx = -1, micIdx = -1;
        bool hasLoopback = !string.IsNullOrEmpty(loopbackInput);

        if (hasLoopback)
        {
            inputs += $" -thread_queue_size 2048 -use_wallclock_as_timestamps 1 -probesize 32 -analyzeduration 0 {loopbackInput}";
            loopbackIdx = nextInput++;
        }

        if (hasMic && !string.IsNullOrEmpty(micDevice))
        {
            inputs += $" -thread_queue_size 1024 -use_wallclock_as_timestamps 1 -f dshow -i audio=\"{micDevice}\"";
            micIdx = nextInput++;
        }

        // Filtros e mapeamentos
        var filters = new List<string>();
        var maps = new List<string>();
        string audioArgs;

        // ddagrab produz frames D3D11. Cada encoder precisa de formato específico:
        // NVENC: D3D11 → CUDA via hwmap (NVENC não aceita D3D11 direto)
        // AMF: D3D11 direto (AMF é nativo D3D11, mesmo device da ddagrab)
        // QSV: D3D11 → QSV via hwmap
        // libx264: D3D11 → CPU via hwdownload (software encoder)
        bool ddagrabDirect = false;
        if (useDdagrab)
        {
            if (hwAccel == "h264_nvenc")
            {
                // hwmap converte D3D11→CUDA inteiramente na GPU (zero transferência CPU)
                filters.Add("[0:v]hwmap=derive_device=cuda,format=cuda[vout]");
                maps.Add("-map \"[vout]\"");
                Debug.WriteLine("[Recording] ddagrab → hwmap D3D11→CUDA → NVENC (zero-copy GPU)");
            }
            else if (hwAccel == "h264_amf")
            {
                // AMF aceita D3D11 frames nativamente (mesmo device da captura)
                ddagrabDirect = true;
                Debug.WriteLine("[Recording] ddagrab → AMF direto (D3D11 nativo)");
            }
            else if (hwAccel == "h264_qsv")
            {
                filters.Add("[0:v]hwmap=derive_device=qsv,format=qsv[vout]");
                maps.Add("-map \"[vout]\"");
                Debug.WriteLine("[Recording] ddagrab → hwmap → QSV");
            }
            else
            {
                filters.Add("[0:v]hwdownload,format=bgra[vout]");
                maps.Add("-map \"[vout]\"");
                Debug.WriteLine("[Recording] ddagrab → hwdownload → libx264");
            }
        }

        bool needsExplicitVideoMap = !useDdagrab || ddagrabDirect;

        if (hasLoopback && hasMic)
        {
            filters.Add($"[{loopbackIdx}:a][{micIdx}:a]amix=inputs=2:duration=longest[aout]");
            if (needsExplicitVideoMap) maps.Add("-map 0:v");
            maps.Add("-map \"[aout]\"");
            audioArgs = "-c:a aac -b:a 192k";
        }
        else if (hasLoopback)
        {
            if (needsExplicitVideoMap) maps.Add("-map 0:v");
            maps.Add($"-map {loopbackIdx}:a");
            audioArgs = "-c:a aac -b:a 192k";
        }
        else if (hasMic)
        {
            if (needsExplicitVideoMap) maps.Add("-map 0:v");
            maps.Add($"-map {micIdx}:a");
            audioArgs = "-c:a aac -b:a 128k";
        }
        else
        {
            if (needsExplicitVideoMap) maps.Add("-map 0:v");
            audioArgs = "";
        }

        var filterArg = filters.Count > 0
            ? $"-filter_complex \"{string.Join(";", filters)}\""
            : "";
        var mapsStr = string.Join(" ", maps);

        // pix_fmt só para caminhos CPU (gdigrab, ddagrab+libx264)
        // HW encoders com ddagrab zero-copy lidam com formato internamente
        string pixFmt = (!useDdagrab || hwAccel == "libx264") ? "-pix_fmt yuv420p" : "";

        // CFR (Constant Frame Rate): ddagrab entrega frames com timestamps irregulares do Desktop Duplication API.
        // Sem regularização, os intervalos desiguais causam vídeo acelerado/não suave.
        // -r {captureRate} + -fps_mode:v cfr força timestamps uniformes (ex: 16.67ms a 60fps),
        // garantindo reprodução fluida. MKV suporta CFR nativamente; remux para MP4 ao parar.

        return $"-y {inputs} {filterArg} {mapsStr} {encoderArgs} {pixFmt} {audioArgs} -r {captureRate} -fps_mode:v cfr -max_muxing_queue_size 2048 \"{_currentOutputFile}\"";
    }

    public void WarmupEncoder()
    {
        DetectHardwareEncoder();
        IsDdagrabAvailable();
    }

    private void StartFacecamPreview(string webcamDevice, FacecamPosition position)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => StartFacecamPreview(webcamDevice, position));
            return;
        }

        StopFacecamPreview();

        var posStr = position switch
        {
            FacecamPosition.TopLeft => "top_left",
            FacecamPosition.BottomRight => "bottom_right",
            FacecamPosition.BottomLeft => "bottom_left",
            _ => "top_right"
        };

        _facecamWindow = new FacecamOverlayWindow(webcamDevice, posStr);
        _facecamWindow.CaptureFailed += error =>
        {
            Debug.WriteLine($"[Facecam] Capture failed: {error}");
            StatusMessage?.Invoke($"⚠ Facecam falhou: {error}");
        };
        _facecamWindow.Show();
    }

    public void StopFacecamPreview()
    {
        var window = _facecamWindow;
        _facecamWindow = null;
        if (window is null) return;

        if (window.Dispatcher.CheckAccess())
        {
            window.StopCapture();
            window.Close();
        }
        else
        {
            window.Dispatcher.BeginInvoke(() =>
            {
                window.StopCapture();
                window.Close();
            });
        }
    }

    private void RetryScreenOnly(bool withLoopback = false, bool forceLibx264 = false)
    {
        if (_ffmpegProcess is not null)
        {
            try { if (!_ffmpegProcess.HasExited) _ffmpegProcess.Kill(); } catch { }
            try { _ffmpegProcess.Dispose(); } catch { }
            _ffmpegProcess = null;
        }

        _lastFfmpegError = string.Empty;

        // Reiniciar pipe WASAPI loopback para o novo processo ffmpeg
        StopLoopbackCapture();
        string? loopbackInput = null;
        if (withLoopback)
        {
            if (StartLoopbackCapture())
                loopbackInput = _loopbackFfmpegInput;
        }

        bool hasLoopback = loopbackInput is not null;
        var args = BuildFfmpegArgs(_currentResolution, hasMic: false, micDevice: null, loopbackInput, forceLibx264: forceLibx264);
        Debug.WriteLine($"[Recording] Retry args (loopback={hasLoopback}): {args}");

        try
        {
            _ffmpegProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FfmpegExe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            _ffmpegProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Debug.WriteLine($"[FFmpeg-retry] {e.Data}");
                    _lastFfmpegError = e.Data;
                }
            };

            _ffmpegProcess.Exited += (_, _) =>
            {
                try
                {
                    // StopRecording already handles cleanup and state notification
                    if (_stoppingManually) return;

                    int exitCode = -1;
                    try { exitCode = _ffmpegProcess?.ExitCode ?? -1; } catch { }
                    if (exitCode != 0)
                    {
                        StopLoopbackCapture();
                        StatusMessage?.Invoke($"❌ Erro na gravação: {_lastFfmpegError}");
                    }
                    RecordingStateChanged?.Invoke(false);
                }
                catch
                {
                    if (!_stoppingManually)
                        RecordingStateChanged?.Invoke(false);
                }
            };

            _ffmpegProcess.Start();
            _ffmpegProcess.BeginErrorReadLine();
            var retryFeatures = new List<string> { "tela" };
            if (hasLoopback) retryFeatures.Add("áudio do jogo");
            StatusMessage?.Invoke($"🔴 Gravando ({string.Join(" + ", retryFeatures)}): {Path.GetFileName(_currentOutputFile)}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Recording] Retry failed: {ex}");
            StopLoopbackCapture();
            StatusMessage?.Invoke($"❌ Falha total na gravação: {ex.Message}");
            _ffmpegProcess?.Dispose();
            _ffmpegProcess = null;
            RecordingStateChanged?.Invoke(false);
        }
    }

    public void StopRecording()
    {
        _stoppingManually = true;
        _pendingRetry = false;

        // Stop WASAPI loopback — closes the pipe, signaling EOF to ffmpeg audio input
        StopLoopbackCapture();
        StopFacecamPreview();

        var proc = _ffmpegProcess;
        var outputFile = _currentOutputFile;
        _ffmpegProcess = null; // IsRecording → false immediately

        if (proc is null || proc.HasExited)
        {
            try { proc?.Dispose(); } catch { }
            RecordingStateChanged?.Invoke(false);
            return;
        }

        // Close stdin — most reliable way to stop ffmpeg gracefully
        // (Write("q") may not work when ddagrab is actively capturing)
        try { proc.StandardInput.Close(); } catch { }

        // Notify UI immediately — recording is "stopped" from the user's perspective
        RecordingStateChanged?.Invoke(false);

        // Background: wait for ffmpeg to exit, kill if needed, then remux MKV → MP4
        _ = Task.Run(() => FinalizeRecording(proc, outputFile));
    }

    private void FinalizeRecording(Process proc, string? outputFile)
    {
        try
        {
            if (!proc.WaitForExit(7000))
            {
                Debug.WriteLine("[Recording] FFmpeg não respondeu em 7s — forçando encerramento");
                try { proc.Kill(); } catch { }
                proc.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Recording] FinalizeRecording wait error: {ex.Message}");
            try { proc.Kill(); } catch { }
        }
        finally
        {
            try { proc.Dispose(); } catch { }
        }

        // Remux MKV → MP4 (cópia instantânea sem re-encoding)
        if (!string.IsNullOrEmpty(outputFile) && File.Exists(outputFile) &&
            outputFile.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage?.Invoke("⏳ Finalizando vídeo...");
            var mp4File = RemuxToMp4(outputFile);
            _currentOutputFile = mp4File;
            StatusMessage?.Invoke($"✅ Vídeo salvo: {Path.GetFileName(mp4File)}");
        }
        else
        {
            StatusMessage?.Invoke(
                !string.IsNullOrEmpty(outputFile) && File.Exists(outputFile)
                    ? $"✅ Vídeo salvo: {Path.GetFileName(outputFile)}"
                    : "Gravação finalizada.");
        }
    }

    /// <summary>
    /// Remux MKV → MP4 sem re-encoding (cópia instantânea de streams).
    /// Adiciona faststart para streaming/reprodução rápida.
    /// </summary>
    private string RemuxToMp4(string mkvPath)
    {
        var mp4Path = Path.ChangeExtension(mkvPath, ".mp4");
        try
        {
            Debug.WriteLine($"[Recording] Remuxing: {Path.GetFileName(mkvPath)} → {Path.GetFileName(mp4Path)}");

            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = FfmpegExe,
                Arguments = $"-i \"{mkvPath}\" -c copy -movflags +faststart \"{mp4Path}\" -y",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            });

            if (proc is null)
            {
                Debug.WriteLine("[Recording] Remux: failed to start ffmpeg");
                return mkvPath;
            }

            // Timeout proporcional ao tamanho (remux é cópia — ~30s por GB em SSD, mínimo 60s)
            var mkvSize = new FileInfo(mkvPath).Length;
            int timeoutMs = Math.Max(60000, (int)(mkvSize / (100.0 * 1024 * 1024) * 30000));
            proc.WaitForExit(timeoutMs);
            if (!proc.HasExited)
            {
                try { proc.Kill(); } catch { }
                Debug.WriteLine("[Recording] Remux: timeout");
                return mkvPath;
            }

            if (proc.ExitCode == 0 && File.Exists(mp4Path) && new FileInfo(mp4Path).Length > 1024)
            {
                Debug.WriteLine($"[Recording] Remux OK: {new FileInfo(mp4Path).Length} bytes");
                try { File.Delete(mkvPath); } catch { }
                return mp4Path;
            }

            Debug.WriteLine($"[Recording] Remux failed (exit {proc.ExitCode}), keeping MKV");
            return mkvPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Recording] Remux exception: {ex.Message}");
            return mkvPath;
        }
    }

    public void ToggleRecording(RecordingResolution resolution, RecordingMode mode = RecordingMode.ScreenOnly,
        FacecamPosition facecamPos = FacecamPosition.TopRight,
        string? webcamDevice = null, string? micDevice = null, string? gameName = null)
    {
        if (IsRecording)
            StopRecording();
        else
            StartRecording(resolution, mode, facecamPos, webcamDevice, micDevice, gameName);
    }

    private string DetectHardwareEncoder()
    {
        var gpuName = SettingsService.Current.RecordingEncoder;
        if (!string.IsNullOrEmpty(gpuName) && gpuName != "auto")
        {
            if (TestEncoder(gpuName))
            {
                Debug.WriteLine($"[Encoder] Using user-selected: {gpuName}");
                return gpuName;
            }
            Debug.WriteLine($"[Encoder] User-selected '{gpuName}' failed test, falling back to auto-detect");
        }

        if (_cachedEncoder is not null)
            return _cachedEncoder;

        // Priority: NVENC (NVIDIA) → AMF (AMD) → QSV (Intel) → libx264 (software)
        foreach (var enc in new[] { "h264_nvenc", "h264_amf", "h264_qsv" })
        {
            if (TestEncoder(enc))
            {
                _cachedEncoder = enc;
                Debug.WriteLine($"[Encoder] Auto-detected: {enc}");
                return enc;
            }
        }

        _cachedEncoder = "libx264";
        Debug.WriteLine("[Encoder] No hardware encoder available, using libx264 software fallback");
        return _cachedEncoder;
    }

    /// <summary>
    /// Tests if a hardware encoder is functional by encoding a short clip with explicit bitrate.
    /// Uses VBR + -b:v (the universally compatible mode) instead of QP/CQP which is driver-dependent.
    /// </summary>
    private bool TestEncoder(string encoder)
    {
        try
        {
            var testFile = Path.Combine(Path.GetTempPath(), $"glauncher_test_{encoder}.mp4");
            try { File.Delete(testFile); } catch { }

            // Test with explicit bitrate (VBR) — works on ALL GPU vendors and drivers.
            // testsrc generates varied frames that actually exercise the encoder.
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = FfmpegExe,
                Arguments = $"-f lavfi -i testsrc=duration=0.5:size=320x240:rate=30 -c:v {encoder} -b:v 1M -pix_fmt yuv420p -frames:v 10 \"{testFile}\" -y",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            });

            if (proc is null) return false;
            string stderr = "";
            proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr += e.Data + "\n"; };
            proc.BeginErrorReadLine();
            proc.WaitForExit(10000);
            if (!proc.HasExited) { try { proc.Kill(); } catch { } return false; }

            var success = proc.ExitCode == 0;

            if (success)
            {
                var fi = new FileInfo(testFile);
                if (!fi.Exists || fi.Length < 2048)
                {
                    Debug.WriteLine($"[Encoder] {encoder} exit=0 but output too small: {(fi.Exists ? fi.Length : 0)} bytes");
                    success = false;
                }
            }
            else
            {
                Debug.WriteLine($"[Encoder] {encoder} failed (exit {proc.ExitCode}): {stderr.Trim().Split('\n').LastOrDefault()}");
            }

            Debug.WriteLine($"[Encoder] Test {encoder}: {(success ? "OK" : "FAIL")}");
            try { File.Delete(testFile); } catch { }
            return success;
        }
        catch (Exception ex)
        {
                Debug.WriteLine($"[Encoder] Test {encoder} exception: {ex.Message}");
                    return false;
                }
            }

            private static bool IsDdagrabAvailable()
            {
                if (_ddagrabAvailable.HasValue) return _ddagrabAvailable.Value;
                if (!File.Exists(FfmpegExe)) { _ddagrabAvailable = false; return false; }

                try
                {
                    var testFile = Path.Combine(Path.GetTempPath(), "glauncher_ddagrab_test.mp4");
                    try { File.Delete(testFile); } catch { }

                    using var proc = Process.Start(new ProcessStartInfo
                    {
                        FileName = FfmpegExe,
                        Arguments = $"-f ddagrab -framerate 30 -i 0 -frames:v 3 -vf \"hwdownload,format=bgra\" -c:v libx264 -preset ultrafast -pix_fmt yuv420p \"{testFile}\" -y",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });

                    if (proc is null) { _ddagrabAvailable = false; return false; }
                    proc.WaitForExit(8000);
                    if (!proc.HasExited) { try { proc.Kill(); } catch { } _ddagrabAvailable = false; return false; }

                    var success = proc.ExitCode == 0 && File.Exists(testFile) && new FileInfo(testFile).Length > 1024;
                    _ddagrabAvailable = success;
                    Debug.WriteLine($"[Recording] ddagrab disponível: {success}");
                    try { File.Delete(testFile); } catch { }
                    return success;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Recording] Teste ddagrab falhou: {ex.Message}");
                    _ddagrabAvailable = false;
                    return false;
                }
            }

            private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    public string? RenameRecording(string gameName)
    {
        if (string.IsNullOrEmpty(_currentOutputFile) || !File.Exists(_currentOutputFile))
            return null;

        var dir = Path.GetDirectoryName(_currentOutputFile)!;
        var ext = Path.GetExtension(_currentOutputFile);
        var safeName = SanitizeFileName(gameName);
        var resLabel = _currentResolution switch
        {
            RecordingResolution.HD_720p => "720p",
            RecordingResolution.UHD_4K => "4K",
            _ => "1080p"
        };
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var newPath = Path.Combine(dir, $"{safeName}_{resLabel}_{timestamp}{ext}");

        if (newPath == _currentOutputFile)
            return _currentOutputFile;

        try
        {
            if (File.Exists(newPath))
            {
                var counter = 1;
                string candidate;
                do
                {
                    candidate = Path.Combine(dir, $"{safeName}_{resLabel}_{timestamp}_{counter}{ext}");
                    counter++;
                } while (File.Exists(candidate));
                newPath = candidate;
            }

            File.Move(_currentOutputFile, newPath);
            _currentOutputFile = newPath;
            StatusMessage?.Invoke($"✅ Vídeo salvo: {Path.GetFileName(newPath)}");
            return newPath;
        }
        catch
        {
            return _currentOutputFile;
        }
    }

    public static string GetOutputDirectory() => OutputDir;

    public static string GetResolutionLabel(RecordingResolution res)
    {
        int hz = Math.Clamp(GetDisplayRefreshRate(), 30, 120);
        return res switch
        {
            RecordingResolution.HD_720p => $"720p (1280×720) — {hz} FPS",
            RecordingResolution.FHD_1080p => $"1080p (1920×1080) — {hz} FPS",
            RecordingResolution.UHD_4K => $"4K (3840×2160) — {hz} FPS",
            _ => "1080p"
        };
    }

    public static string GetFacecamPositionLabel(FacecamPosition pos) => pos switch
    {
        FacecamPosition.TopLeft => "Superior Esquerda",
        FacecamPosition.TopRight => "Superior Direita",
        FacecamPosition.BottomLeft => "Inferior Esquerda",
        FacecamPosition.BottomRight => "Inferior Direita",
        _ => "Superior Direita"
    };

    public void Dispose()
    {
        _stoppingManually = true;
        _pendingRetry = false;
        StopLoopbackCapture();
        StopFacecamPreview();

        var proc = _ffmpegProcess;
        _ffmpegProcess = null;
        if (proc is not null)
        {
            if (!proc.HasExited)
            {
                try { proc.StandardInput.Close(); } catch { }
                if (!proc.WaitForExit(3000))
                    try { proc.Kill(); } catch { }
            }
            try { proc.Dispose(); } catch { }
        }
    }
}
