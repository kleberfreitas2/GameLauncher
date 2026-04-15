using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace GameLauncher.Views;

public partial class FacecamOverlayWindow : Window
{
    private const int FrameWidth = 320;
    private const int FrameHeight = 180;
    private const int BytesPerPixel = 3; // BGR24
    private const int FrameSize = FrameWidth * FrameHeight * BytesPerPixel;

    private static readonly string FfmpegExe = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameLauncher", "ffmpeg", "ffmpeg.exe");

    private Process? _captureProcess;
    private WriteableBitmap? _bitmap;
    private CancellationTokenSource? _cts;
    private DispatcherTimer? _topmostTimer;
    private string _lastError = string.Empty;
    private volatile bool _renderPending;
    private volatile bool _stopped;
    private string? _webcamDevice;
    private bool _retriedLowerFps;

    public event Action<string>? CaptureFailed;

    public FacecamOverlayWindow(string webcamDevice, string position)
    {
        InitializeComponent();
        PositionWindow(position);

        Loaded += (_, _) =>
        {
            StartCapture(webcamDevice);
            StartTopmostTimer();
        };
    }

    private void StartTopmostTimer()
    {
        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _topmostTimer.Tick += (_, _) =>
        {
            if (!IsVisible || _stopped) return;
            Topmost = false;
            Topmost = true;
        };
        _topmostTimer.Start();
    }

    private void PositionWindow(string position)
    {
        var screen = SystemParameters.WorkArea;
        const double margin = 20;

        (Left, Top) = position switch
        {
            "top_left" => (screen.Left + margin, screen.Top + margin),
            "bottom_right" => (screen.Right - Width - margin, screen.Bottom - Height - margin),
            "bottom_left" => (screen.Left + margin, screen.Bottom - Height - margin),
            _ => (screen.Right - Width - margin, screen.Top + margin)
        };
    }

    private void StartCapture(string webcamDevice, int fps = 30)
    {
        if (!File.Exists(FfmpegExe))
        {
            CaptureFailed?.Invoke("FFmpeg não encontrado");
            return;
        }

        _webcamDevice = webcamDevice;
        _bitmap = new WriteableBitmap(FrameWidth, FrameHeight, 96, 96, PixelFormats.Bgr24, null);
        CameraImage.Source = _bitmap;
        _cts = new CancellationTokenSource();

        try
        {
            _captureProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FfmpegExe,
                    Arguments = $"-hide_banner -loglevel warning " +
                                $"-fflags nobuffer -probesize 32 -analyzeduration 0 " +
                                $"-f dshow -framerate {fps} -rtbufsize 50M -i video=\"{webcamDevice}\" " +
                                $"-vf scale={FrameWidth}:{FrameHeight}:flags=fast_bilinear " +
                                $"-f rawvideo -pix_fmt bgr24 -",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                },
                EnableRaisingEvents = true
            };

            _captureProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Debug.WriteLine($"[Facecam] {e.Data}");
                    _lastError = e.Data;
                }
            };

            _captureProcess.Exited += (_, _) =>
            {
                int code = -1;
                try { code = _captureProcess?.ExitCode ?? -1; } catch { }
                Debug.WriteLine($"[Facecam] FFmpeg exited (code {code}, fps={fps}), last error: {_lastError}");

                // If first attempt failed and we haven't retried yet, fallback to 15fps
                if (code != 0 && !_stopped && !_retriedLowerFps && fps > 15)
                {
                    _retriedLowerFps = true;
                    Debug.WriteLine("[Facecam] Retrying with 15fps fallback...");
                    Dispatcher.BeginInvoke(() =>
                    {
                        CleanupProcess();
                        StartCapture(webcamDevice, 15);
                    });
                    return;
                }

                if (code != 0 && !_stopped)
                {
                    var error = !string.IsNullOrEmpty(_lastError)
                        ? _lastError
                        : $"FFmpeg encerrou com código {code}";
                    Dispatcher.BeginInvoke(() => CaptureFailed?.Invoke(error));
                }
            };

            Debug.WriteLine($"[Facecam] Starting capture at {fps}fps...");
            _captureProcess.Start();
            _captureProcess.BeginErrorReadLine();

            var stdout = _captureProcess.StandardOutput.BaseStream;
            var ct = _cts.Token;
            _ = Task.Run(() => ReadFrames(stdout, ct), ct);

            Debug.WriteLine($"[Facecam] Capture started (PID: {_captureProcess.Id}, {fps}fps)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Facecam] Failed to start capture: {ex.Message}");
            CaptureFailed?.Invoke(ex.Message);
        }
    }

    private void CleanupProcess()
    {
        _cts?.Cancel();
        if (_captureProcess is not null && !_captureProcess.HasExited)
        {
            try { _captureProcess.Kill(); } catch { }
        }
        _captureProcess?.Dispose();
        _captureProcess = null;
        _cts?.Dispose();
        _cts = null;
    }

    private void ReadFrames(Stream stdout, CancellationToken ct)
    {
        var buffer = new byte[FrameSize];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Read one complete frame
                int totalRead = 0;
                while (totalRead < FrameSize)
                {
                    int read = stdout.Read(buffer, totalRead, FrameSize - totalRead);
                    if (read == 0) return; // EOF — process ended
                    totalRead += read;
                }

                // Skip if previous frame not yet rendered (prevents UI queue buildup)
                if (_renderPending) continue;

                _renderPending = true;
                var frameCopy = new byte[FrameSize];
                Buffer.BlockCopy(buffer, 0, frameCopy, 0, FrameSize);

                Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
                {
                    try
                    {
                        if (_bitmap is not null && !ct.IsCancellationRequested)
                        {
                            _bitmap.WritePixels(
                                new Int32Rect(0, 0, FrameWidth, FrameHeight),
                                frameCopy, FrameWidth * BytesPerPixel, 0);
                        }
                    }
                    finally
                    {
                        _renderPending = false;
                    }
                });
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { } // Pipe closed
        catch (Exception ex)
        {
            Debug.WriteLine($"[Facecam] Frame reader error: {ex.Message}");
        }
    }

    public void StopCapture()
    {
        if (_stopped) return;
        _stopped = true;

        _topmostTimer?.Stop();
        _topmostTimer = null;
        _cts?.Cancel();

        if (_captureProcess is not null && !_captureProcess.HasExited)
        {
            try
            {
                // Graceful stop via stdin 'q'
                _captureProcess.StandardInput.Write("q");
                _captureProcess.StandardInput.Flush();
                if (!_captureProcess.WaitForExit(2000))
                    _captureProcess.Kill();
            }
            catch
            {
                try { _captureProcess.Kill(); } catch { }
            }
        }

        _captureProcess?.Dispose();
        _captureProcess = null;
        _cts?.Dispose();
        _cts = null;
    }

    protected override void OnClosed(EventArgs e)
    {
        StopCapture();
        base.OnClosed(e);
    }
}
