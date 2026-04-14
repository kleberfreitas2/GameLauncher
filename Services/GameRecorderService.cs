using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

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
    private static readonly string FfplayExe = Path.Combine(FfmpegDir, "ffplay.exe");

    private static readonly string OutputDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        "GLauncher Videos");

    private Process? _ffmpegProcess;
    private Process? _ffplayProcess;
    private string? _currentOutputFile;
    private RecordingResolution _currentResolution;
    private string _lastFfmpegError = string.Empty;
    private string _lastFfplayError = string.Empty;
    private volatile bool _stoppingManually;
    private volatile bool _pendingRetry;
    private static string? _cachedEncoder;
    private CancellationTokenSource? _facecamKeepAliveCts;
    private string? _resolvedLoopbackDevice;
    private string? _facecamWebcamDevice;
    private FacecamPosition _facecamPosition;
    private int _facecamRestartCount;
    private const int MaxFacecamRestarts = 5;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const int SW_SHOWNA = 8;
    private const int SW_HIDE = 0;

    public bool IsRecording => _ffmpegProcess is not null && !_ffmpegProcess.HasExited;
    public string? CurrentFile => _currentOutputFile;

    public event Action<bool>? RecordingStateChanged;
    public event Action<string>? StatusMessage;

    public static bool IsFfmpegAvailable() => File.Exists(FfmpegExe);
    public static bool IsFfplayAvailable() => File.Exists(FfplayExe);

    public static async Task<bool> EnsureFfmpegAsync(Action<string>? progress = null)
    {
        if (File.Exists(FfmpegExe) && File.Exists(FfplayExe))
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

                var ffplayEntry = zip.Entries
                    .FirstOrDefault(e => e.FullName.EndsWith("bin/ffplay.exe", StringComparison.OrdinalIgnoreCase));
                ffplayEntry?.ExtractToFile(FfplayExe, overwrite: true);
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

    private static (bool success, string info) DiagnoseVideoDevice(string deviceName)
    {
        if (!File.Exists(FfmpegExe)) return (false, "FFmpeg não encontrado");
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FfmpegExe,
                    Arguments = $"-f dshow -rtbufsize 100M -i video=\"{deviceName}\" -frames:v 1 -f null - -y",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                }
            };
            proc.Start();
            var stderr = proc.StandardError.ReadToEnd();
            if (!proc.WaitForExit(10000))
            {
                try { proc.Kill(); } catch { }
                return (true, "");
            }

            Debug.WriteLine($"[Diagnostic] Video device test exit={proc.ExitCode}");
            foreach (var line in stderr.Split('\n'))
                Debug.WriteLine($"[Diagnostic] {line.TrimEnd()}");

            if (proc.ExitCode == 0) return (true, "");

            var err = stderr.Split('\n')
                .LastOrDefault(l =>
                    l.Contains("Could not", StringComparison.OrdinalIgnoreCase) ||
                    l.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                    l.Contains("No such", StringComparison.OrdinalIgnoreCase) ||
                    l.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                    l.Contains("Permission", StringComparison.OrdinalIgnoreCase))
                ?.Trim() ?? $"Código de saída: {proc.ExitCode}";
            return (false, err);
        }
        catch (Exception ex) { return (false, ex.Message); }
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

    private static string? FindSystemLoopbackDevice()
    {
        // Search dshow listing first
        var dshowAudio = ListDshowDevices("audio");
        foreach (var d in dshowAudio)
        {
            if (IsLoopbackDeviceName(d))
            {
                Debug.WriteLine($"[Recording] Loopback device found (dshow): '{d}'");
                return d;
            }
        }

        // Registry fallback — search active capture devices for loopback keywords
        try
        {
            using var captureKey = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture");
            if (captureKey is null) return null;

            string? cachedAdapter = null;
            bool adapterResolved = false;

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

                    var fullName = propsKey.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6")?.ToString();
                    if (!string.IsNullOrEmpty(fullName) && IsLoopbackDeviceName(fullName))
                    {
                        Debug.WriteLine($"[Recording] Loopback device found (registry full): '{fullName}'");
                        return fullName;
                    }

                    var endpointName = propsKey.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2")?.ToString();
                    if (!string.IsNullOrEmpty(endpointName) && IsLoopbackDeviceName(endpointName))
                    {
                        if (!endpointName.Contains('('))
                        {
                            if (!adapterResolved)
                            {
                                cachedAdapter = GetPhysicalAudioAdapterName();
                                adapterResolved = true;
                            }
                            if (cachedAdapter is not null)
                            {
                                var built = $"{endpointName} ({cachedAdapter})";
                                Debug.WriteLine($"[Recording] Loopback device found (registry built): '{built}'");
                                return built;
                            }
                        }
                        Debug.WriteLine($"[Recording] Loopback device found (registry endpoint): '{endpointName}'");
                        return endpointName;
                    }
                }
                catch { continue; }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Recording] FindSystemLoopbackDevice registry error: {ex.Message}");
        }

        return null;
    }

    private static bool IsLoopbackDeviceName(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("stereo mix") || lower.Contains("mixagem est") ||
               lower.Contains("mezcla est") || lower.Contains("stereomix") ||
               lower.Contains("what u hear") || lower.Contains("wave out mix") ||
               lower.Contains("loopback");
    }

    private static bool HasDisabledLoopbackDevice()
    {
        try
        {
            using var captureKey = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture");
            if (captureKey is null) return false;
            foreach (var subKeyName in captureKey.GetSubKeyNames())
            {
                try
                {
                    using var deviceKey = captureKey.OpenSubKey(subKeyName);
                    if (deviceKey is null) continue;
                    var state = deviceKey.GetValue("DeviceState");
                    if (state is not int stateVal || stateVal != 2) continue;
                    using var propsKey = deviceKey.OpenSubKey("Properties");
                    if (propsKey is null) continue;
                    var fullName = propsKey.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6")?.ToString();
                    if (!string.IsNullOrEmpty(fullName) && IsLoopbackDeviceName(fullName))
                    {
                        Debug.WriteLine($"[Recording] Disabled loopback found: '{fullName}'");
                        return true;
                    }
                    var endpointName = propsKey.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2")?.ToString();
                    if (!string.IsNullOrEmpty(endpointName) && IsLoopbackDeviceName(endpointName))
                    {
                        Debug.WriteLine($"[Recording] Disabled loopback found: '{endpointName}'");
                        return true;
                    }
                }
                catch { continue; }
            }
        }
        catch { }
        return false;
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
        _currentOutputFile = Path.Combine(OutputDir, $"{safeName}_{timestamp}.mp4");

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

        // Detect loopback device (Stereo Mix / Mixagem estéreo) for game audio capture
        _resolvedLoopbackDevice = FindSystemLoopbackDevice();
        bool hasLoopback = _resolvedLoopbackDevice is not null;
        if (hasLoopback)
        {
            Debug.WriteLine($"[Recording] Loopback device: '{_resolvedLoopbackDevice}'");
        }
        else
        {
            if (HasDisabledLoopbackDevice())
                StatusMessage?.Invoke("⚠ 'Mixagem estéreo' está desativada — ative em: Painel de Controle → Som → aba Gravação → clique direito → Mostrar dispositivos desativados → Mixagem estéreo → Ativar.");
            else
                Debug.WriteLine("[Recording] No loopback device available — game audio will not be captured");
        }

        // Don't use the same device as both mic and loopback
        if (hasMic && hasLoopback &&
            micDevice!.Equals(_resolvedLoopbackDevice, StringComparison.OrdinalIgnoreCase))
        {
            Debug.WriteLine("[Recording] Mic device same as loopback — skipping duplicate");
            hasMic = false;
        }

        Debug.WriteLine($"[Recording] Config: mode={mode}, hasMic={hasMic}, hasCam={hasCam}, hasLoopback={hasLoopback}");
        var args = BuildFfmpegArgs(resolution, hasMic, micDevice, hasLoopback, _resolvedLoopbackDevice);
        Debug.WriteLine($"[Recording] FFmpeg args: {args}");
        // _pendingRetry covers only mic/loopback because the health check only retries for those.
        // Including hasCam here would suppress StopFacecamPreview() in the Exited handler while
        // the health check still falls through to RecordingStateChanged(false), leaving ffplay orphaned.
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
                    var proc = _ffmpegProcess;
                    int exitCode = -1;
                    try { exitCode = proc?.ExitCode ?? -1; } catch { }
                    bool crashed = exitCode != 0 && !_stoppingManually;

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
                    if (!_pendingRetry)
                        RecordingStateChanged?.Invoke(false);
                }
            };

            // Start facecam preview via ffplay before gdigrab so it appears on screen
            if (hasCam)
                StartFacecamPreview(webcamDevice!, facecamPos);

            _ffmpegProcess.Start();
            _ffmpegProcess.BeginErrorReadLine();

            // Monitor process health asynchronously — retry without mic if device failed
            var hasMicForRetry = hasMic;
            var hasLoopbackForRetry = hasLoopback;
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                _pendingRetry = false;
                try
                {
                    var proc = _ffmpegProcess;
                    if (proc is null || _stoppingManually) return;
                    if (!proc.HasExited) return; // Still running = success

                    int exitCode = -1;
                    try { exitCode = proc.ExitCode; } catch { }
                    if (exitCode == 0) return;

                    if (hasMicForRetry || hasLoopbackForRetry)
                    {
                        // Retry: drop mic (most likely cause), keep loopback
                        string? retryLoopback = hasLoopbackForRetry ? _resolvedLoopbackDevice : null;
                        Debug.WriteLine($"[Recording] FFmpeg failed (exit {exitCode}), retrying without mic (loopback={retryLoopback is not null})...");
                        StatusMessage?.Invoke(retryLoopback is not null
                            ? "⚠ Dispositivo falhou. Tentando novamente com áudio do jogo..."
                            : "⚠ Dispositivo de áudio falhou. Tentando novamente sem áudio...");
                        RetryScreenOnly(loopbackDevice: retryLoopback);
                    }
                    else
                    {
                        var errorDetail = !string.IsNullOrEmpty(_lastFfmpegError)
                            ? _lastFfmpegError
                            : "FFmpeg não conseguiu iniciar a gravação";
                        StatusMessage?.Invoke($"❌ Falha: {errorDetail}");
                        RecordingStateChanged?.Invoke(false);
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

    private string BuildFfmpegArgs(RecordingResolution resolution, bool hasMic, string? micDevice,
        bool hasLoopback = false, string? loopbackDevice = null)
    {
        var (width, height) = resolution switch
        {
            RecordingResolution.HD_720p => (1280, 720),
            RecordingResolution.FHD_1080p => (1920, 1080),
            RecordingResolution.UHD_4K => (3840, 2160),
            _ => (1920, 1080)
        };

        var hwAccel = DetectHardwareEncoder();

        var encoderArgs = hwAccel switch
        {
            "h264_nvenc" => "-c:v h264_nvenc -preset p4 -tune hq -rc vbr -cq 23",
            "h264_amf" => "-c:v h264_amf -quality balanced -rc cqp -qp_i 23 -qp_p 23",
            "h264_qsv" => "-c:v h264_qsv -preset medium -global_quality 23",
            _ => "-c:v libx264 -preset ultrafast -crf 23"
        };

        // Screen capture via gdigrab (facecam is shown by ffplay and captured as part of the desktop)
        var inputs = "-f gdigrab -framerate 60 -i desktop";
        int nextInput = 1;
        int loopbackIdx = -1, micIdx = -1;

        if (hasLoopback && !string.IsNullOrEmpty(loopbackDevice))
        {
            inputs += $" -f dshow -i audio=\"{loopbackDevice}\"";
            loopbackIdx = nextInput++;
        }

        if (hasMic && !string.IsNullOrEmpty(micDevice))
        {
            inputs += $" -f dshow -i audio=\"{micDevice}\"";
            micIdx = nextInput++;
        }

        string videoMap = "-map 0:v";
        string audioMap;
        string audioArgs;
        string filterComplex = "";

        if (hasLoopback && hasMic)
        {
            filterComplex = $"-filter_complex \"[{loopbackIdx}:a][{micIdx}:a]amix=inputs=2:duration=longest[aout]\"";
            audioMap = "-map \"[aout]\"";
            audioArgs = "-c:a aac -b:a 192k";
        }
        else if (hasLoopback)
        {
            audioMap = $"-map {loopbackIdx}:a";
            audioArgs = "-c:a aac -b:a 192k";
        }
        else if (hasMic)
        {
            audioMap = $"-map {micIdx}:a";
            audioArgs = "-c:a aac -b:a 128k";
        }
        else
        {
            audioMap = "";
            audioArgs = "";
        }

        return $"-y {inputs} {filterComplex} {videoMap} {audioMap} {encoderArgs} -s {width}x{height} -pix_fmt yuv420p {audioArgs} -movflags +faststart \"{_currentOutputFile}\"";
    }

    private void StartFacecamPreview(string webcamDevice, FacecamPosition position)
    {
        StopFacecamPreview();
        _facecamWebcamDevice = webcamDevice;
        _facecamPosition = position;
        _facecamRestartCount = 0;

        // Verify webcam is accessible via ffmpeg before launching ffplay
        var (deviceOk, diagInfo) = DiagnoseVideoDevice(webcamDevice);
        if (!deviceOk)
        {
            Debug.WriteLine($"[FFplay] Video device diagnosis FAILED: {diagInfo}");
            StatusMessage?.Invoke($"⚠ Câmera '{webcamDevice}' inacessível: {diagInfo}");
            return;
        }
        Debug.WriteLine("[FFplay] Video device diagnosis OK");

        // Small delay after releasing device from diagnostic test
        Thread.Sleep(500);

        LaunchFfplayProcess(webcamDevice, position);
        StartFacecamKeepAlive();
    }

    private void LaunchFfplayProcess(string webcamDevice, FacecamPosition position)
    {
        if (_ffplayProcess is not null)
        {
            if (!_ffplayProcess.HasExited)
                try { _ffplayProcess.Kill(); } catch { }
            _ffplayProcess.Dispose();
            _ffplayProcess = null;
        }

        var screenW = GetSystemMetrics(SM_CXSCREEN);
        var screenH = GetSystemMetrics(SM_CYSCREEN);
        var camW = 480;
        var camH = 270;
        var margin = 20;

        var (left, top) = position switch
        {
            FacecamPosition.TopRight => (screenW - camW - margin, margin),
            FacecamPosition.TopLeft => (margin, margin),
            FacecamPosition.BottomRight => (screenW - camW - margin, screenH - camH - margin - 50),
            FacecamPosition.BottomLeft => (margin, screenH - camH - margin - 50),
            _ => (screenW - camW - margin, margin)
        };

        try
        {
            // No stdio redirection — stderr pipes can interfere with SDL2 window lifecycle.
            // -rtbufsize 100M: prevents dshow real-time buffer overflow.
            // CreateNoWindow = true: suppresses the ffplay console window; the SDL2 video
            // window (GLauncherFacecam) is created by SDL2 independently and still appears.
            // Device accessibility already verified by DiagnoseVideoDevice.
            _ffplayProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FfplayExe,
                    Arguments = $"-f dshow -rtbufsize 100M -i video=\"{webcamDevice}\" " +
                                $"-an -window_title GLauncherFacecam -noborder -alwaysontop " +
                                $"-left {left} -top {top} -x {camW} -y {camH}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            _ffplayProcess.Exited += (_, _) =>
            {
                int code = -1;
                try { code = _ffplayProcess?.ExitCode ?? -1; } catch { }
                Debug.WriteLine($"[FFplay] Facecam exited (code {code})");
            };

            _ffplayProcess.Start();
            Debug.WriteLine($"[FFplay] Facecam started (PID: {_ffplayProcess.Id})");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FFplay] Failed to start facecam: {ex.Message}");
            StatusMessage?.Invoke($"⚠ Erro ao iniciar facecam: {ex.Message}");
        }
    }

    private IntPtr FindFfplayWindow()
    {
        // Try title-based search first (fastest)
        var hwnd = FindWindow(null!, "GLauncherFacecam");
        if (hwnd != IntPtr.Zero) return hwnd;

        // Fallback: search by PID — SDL2 may use a different window title format
        if (_ffplayProcess is null || _ffplayProcess.HasExited) return IntPtr.Zero;

        uint pid;
        try { pid = (uint)_ffplayProcess.Id; } catch { return IntPtr.Zero; }

        IntPtr result = IntPtr.Zero;
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out uint windowPid);
            if (windowPid == pid && IsWindowVisible(h))
            {
                // Skip console windows — we want the SDL2 window
                var sb = new StringBuilder(256);
                GetClassName(h, sb, 256);
                if (!sb.ToString().Equals("ConsoleWindowClass", StringComparison.OrdinalIgnoreCase))
                {
                    result = h;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);

        return result;
    }

    private void HideFfplayConsoleWindows(IntPtr sdlHwnd)
    {
        if (_ffplayProcess is null || _ffplayProcess.HasExited) return;
        uint pid;
        try { pid = (uint)_ffplayProcess.Id; } catch { return; }

        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out uint windowPid);
            if (windowPid == pid && h != sdlHwnd && IsWindowVisible(h))
                ShowWindow(h, SW_HIDE);
            return true;
        }, IntPtr.Zero);
    }

    private void StartFacecamKeepAlive()
    {
        _facecamKeepAliveCts?.Cancel();
        _facecamKeepAliveCts = new CancellationTokenSource();
        var ct = _facecamKeepAliveCts.Token;

        _ = Task.Run(async () =>
        {
            int noWindowTicks = 0;
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(500, ct); } catch (OperationCanceledException) { break; }
                try
                {
                    var hwnd = FindFfplayWindow();
                    if (hwnd != IntPtr.Zero)
                    {
                        noWindowTicks = 0;
                        HideFfplayConsoleWindows(hwnd);
                        // Re-assert topmost every tick to survive game fullscreen transitions.
                        // SetWindowPos with SWP_NOACTIVATE is safe — does not disturb SDL2 event loop.
                        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    }
                    else if (_ffplayProcess is not null && !_ffplayProcess.HasExited)
                    {
                        // Process alive but no visible SDL window — may be stuck
                        noWindowTicks++;
                        if (noWindowTicks >= 10 && _facecamRestartCount < MaxFacecamRestarts &&
                            _facecamWebcamDevice is not null &&
                            _ffmpegProcess is not null && !_ffmpegProcess.HasExited)
                        {
                            noWindowTicks = 0;
                            _facecamRestartCount++;
                            Debug.WriteLine($"[FFplay] Force restart: no visible window (attempt {_facecamRestartCount}/{MaxFacecamRestarts})");
                            try { _ffplayProcess.Kill(); } catch { }
                            try { _ffplayProcess.Dispose(); } catch { }
                            _ffplayProcess = null;
                            try { await Task.Delay(1000, ct); } catch (OperationCanceledException) { break; }
                            if (_facecamWebcamDevice is not null)
                                LaunchFfplayProcess(_facecamWebcamDevice, _facecamPosition);
                        }
                    }
                    else
                    {
                        noWindowTicks = 0;
                        // Auto-restart ffplay if recording is still active
                        if (_facecamRestartCount < MaxFacecamRestarts &&
                            _facecamWebcamDevice is not null &&
                            _ffmpegProcess is not null && !_ffmpegProcess.HasExited)
                        {
                            _facecamRestartCount++;
                            Debug.WriteLine($"[FFplay] Auto-restart facecam (attempt {_facecamRestartCount}/{MaxFacecamRestarts})");
                            // Longer delay between restarts — give webcam time to reinitialize
                            try { await Task.Delay(2000, ct); } catch (OperationCanceledException) { break; }
                            LaunchFfplayProcess(_facecamWebcamDevice, _facecamPosition);
                        }
                        else
                        {
                            // Run fresh diagnostic to get the actual error
                            var diagDevice = _facecamWebcamDevice ?? "";
                            var (_, diagErr) = DiagnoseVideoDevice(diagDevice);
                            Debug.WriteLine($"[FFplay] Keepalive: stopping. Fresh diagnostic: {diagErr}");
                            if (!string.IsNullOrEmpty(diagErr))
                                StatusMessage?.Invoke($"⚠ Facecam falhou: {diagErr}");
                            else
                                StatusMessage?.Invoke("⚠ Facecam não conseguiu iniciar. Verifique permissões da câmera.");
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }, ct);
    }

    private void StopFacecamKeepAlive()
    {
        try { _facecamKeepAliveCts?.Cancel(); } catch { }
        _facecamKeepAliveCts?.Dispose();
        _facecamKeepAliveCts = null;
    }

    public void StopFacecamPreview()
    {
        StopFacecamKeepAlive();
        if (_ffplayProcess is not null)
        {
            if (!_ffplayProcess.HasExited)
                try { _ffplayProcess.Kill(); } catch { }
            _ffplayProcess.Dispose();
            _ffplayProcess = null;
        }
    }

    private void RetryScreenOnly(string? loopbackDevice = null)
    {
        if (_ffmpegProcess is not null)
        {
            try { if (!_ffmpegProcess.HasExited) _ffmpegProcess.Kill(); } catch { }
            try { _ffmpegProcess.Dispose(); } catch { }
            _ffmpegProcess = null;
        }

        _lastFfmpegError = string.Empty;
        bool hasLoopback = loopbackDevice is not null;
        var args = BuildFfmpegArgs(_currentResolution, hasMic: false, micDevice: null,
            hasLoopback, loopbackDevice);
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
                    int exitCode = -1;
                    try { exitCode = _ffmpegProcess?.ExitCode ?? -1; } catch { }
                    if (exitCode != 0 && !_stoppingManually)
                        StatusMessage?.Invoke($"❌ Erro na gravação: {_lastFfmpegError}");
                    RecordingStateChanged?.Invoke(false);
                }
                catch
                {
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

        if (_ffmpegProcess is null || _ffmpegProcess.HasExited)
        {
            _ffmpegProcess?.Dispose();
            _ffmpegProcess = null;
            StopFacecamPreview();
            RecordingStateChanged?.Invoke(false);
            return;
        }

        try
        {
            _ffmpegProcess.StandardInput.Write("q");
            _ffmpegProcess.StandardInput.Flush();

            if (!_ffmpegProcess.WaitForExit(5000))
            {
                _ffmpegProcess.Kill();
            }

            var file = _currentOutputFile;
            StatusMessage?.Invoke(
                File.Exists(file)
                    ? $"✅ Vídeo salvo: {Path.GetFileName(file)}"
                    : "Gravação finalizada.");
        }
        catch
        {
            try { _ffmpegProcess?.Kill(); } catch { }
            StatusMessage?.Invoke("Gravação parada.");
        }
        finally
        {
            _ffmpegProcess?.Dispose();
            _ffmpegProcess = null;
            StopFacecamPreview();
            RecordingStateChanged?.Invoke(false);
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
            return gpuName;

        if (_cachedEncoder is not null)
            return _cachedEncoder;

        if (TestEncoder("h264_nvenc")) { _cachedEncoder = "h264_nvenc"; return _cachedEncoder; }
        if (TestEncoder("h264_amf")) { _cachedEncoder = "h264_amf"; return _cachedEncoder; }
        if (TestEncoder("h264_qsv")) { _cachedEncoder = "h264_qsv"; return _cachedEncoder; }
        _cachedEncoder = "libx264";
        return _cachedEncoder;
    }

    private bool TestEncoder(string encoder)
    {
        try
        {
            var testFile = Path.Combine(Path.GetTempPath(), $"glauncher_test_{encoder}.mp4");
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = FfmpegExe,
                Arguments = $"-f lavfi -i nullsrc=s=256x256:d=0.1 -c:v {encoder} -frames:v 1 \"{testFile}\" -y",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            });

            proc?.WaitForExit(5000);
            var success = proc?.ExitCode == 0;
            try { File.Delete(testFile); } catch { }
            return success;
        }
        catch
        {
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

    public static string GetResolutionLabel(RecordingResolution res) => res switch
    {
        RecordingResolution.HD_720p => "720p (1280×720) — 60 FPS",
        RecordingResolution.FHD_1080p => "1080p (1920×1080) — 60 FPS",
        RecordingResolution.UHD_4K => "4K (3840×2160) — 60 FPS",
        _ => "1080p"
    };

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
        if (IsRecording)
            StopRecording();
        StopFacecamPreview();
        _ffmpegProcess?.Dispose();
    }
}
