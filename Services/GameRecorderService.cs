using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

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

    public static List<string> ListVideoDevices() => ListDshowDevices("video");
    public static List<string> ListAudioDevices() => ListDshowDevices("audio");

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

    public bool StartRecording(RecordingResolution resolution, RecordingMode mode = RecordingMode.ScreenOnly,
        FacecamPosition facecamPos = FacecamPosition.TopRight,
        string? webcamDevice = null, string? micDevice = null, string? gameName = null)
    {
        if (IsRecording) return false;
        if (!File.Exists(FfmpegExe)) return false;

        _currentResolution = resolution;
        Directory.CreateDirectory(OutputDir);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var safeName = string.IsNullOrEmpty(gameName) ? "Recording" : SanitizeFileName(gameName);
        _currentOutputFile = Path.Combine(OutputDir, $"{safeName}_{timestamp}.mp4");

        bool hasMic = mode != RecordingMode.ScreenOnly && !string.IsNullOrEmpty(micDevice);
        bool hasCam = mode == RecordingMode.FacecamMic && !string.IsNullOrEmpty(webcamDevice);

        if (hasCam && File.Exists(FfplayExe))
            StartFacecamPreview(webcamDevice!, facecamPos);

        var args = BuildFfmpegArgs(resolution, hasCam: false, hasMic, micDevice);

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

            _ffmpegProcess.Exited += (_, _) =>
            {
                StopFacecamPreview();
                RecordingStateChanged?.Invoke(false);
            };

            _ffmpegProcess.Start();
            _ffmpegProcess.BeginErrorReadLine();

            RecordingStateChanged?.Invoke(true);
            StatusMessage?.Invoke($"🔴 Gravando: {Path.GetFileName(_currentOutputFile)}");
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

    private string BuildFfmpegArgs(RecordingResolution resolution, bool hasCam, bool hasMic, string? micDevice)
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

        var inputs = $"-f gdigrab -framerate 60 -i desktop";

        if (hasMic)
            inputs += $" -f dshow -i audio=\"{micDevice}\"";

        var audioArgs = hasMic ? "-c:a aac -b:a 128k" : "";

        return $"{inputs} {encoderArgs} -s {width}x{height} -pix_fmt yuv420p {audioArgs} -movflags +faststart \"{_currentOutputFile}\"";
    }

    private void StartFacecamPreview(string webcamDevice, FacecamPosition position)
    {
        StopFacecamPreview();

        var screenW = GetSystemMetrics(SM_CXSCREEN);
        var screenH = GetSystemMetrics(SM_CYSCREEN);
        var camW = 320;
        var camH = 240;
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
            _ffplayProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FfplayExe,
                    Arguments = $"-f dshow -video_size 640x480 -i video=\"{webcamDevice}\" " +
                                $"-window_title GLauncherFacecam -noborder -alwaysontop " +
                                $"-left {left} -top {top} -x {camW} -y {camH} -loglevel quiet",
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
            _ffplayProcess.Start();
        }
        catch { }
    }

    public void StopFacecamPreview()
    {
        if (_ffplayProcess is not null)
        {
            if (!_ffplayProcess.HasExited)
                try { _ffplayProcess.Kill(); } catch { }
            _ffplayProcess.Dispose();
            _ffplayProcess = null;
        }
    }

    public void StopRecording()
    {
        if (_ffmpegProcess is null || _ffmpegProcess.HasExited)
        {
            _ffmpegProcess = null;
            StopFacecamPreview();
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

        if (TestEncoder("h264_nvenc")) return "h264_nvenc";
        if (TestEncoder("h264_amf")) return "h264_amf";
        if (TestEncoder("h264_qsv")) return "h264_qsv";
        return "libx264";
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
