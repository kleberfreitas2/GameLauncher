using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SkiaSharp;

namespace GameLauncher.Controls;

/// <summary>
/// WPF Image control that supports animated WEBP and GIF playback via SkiaSharp.
/// For static images falls back to WPF native BitmapImage loading.
/// </summary>
public class AnimatedImage : Image
{
    private SKCodec? _codec;
    private SKData? _skData;
    private List<SKBitmap>? _frames;
    private List<int>? _delays;
    private WriteableBitmap? _wb;
    private DispatcherTimer? _timer;
    private int _frameIndex;

    public static readonly DependencyProperty ImagePathProperty =
        DependencyProperty.Register(nameof(ImagePath), typeof(string), typeof(AnimatedImage),
            new PropertyMetadata(null, (d, _) => ((AnimatedImage)d).Reload()));

    public string? ImagePath
    {
        get => (string?)GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public AnimatedImage()
    {
        Unloaded += (_, _) => Cleanup();
    }

    private void Reload()
    {
        Cleanup();
        Source = null;

        var path = ImagePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".webp" or ".gif")
        {
            TryLoadAnimated(path);
        }
        else
        {
            LoadStatic(path);
        }
    }

    private void TryLoadAnimated(string path)
    {
        try
        {
            _skData = SKData.Create(path);
            _codec = SKCodec.Create(_skData);

            if (_codec is null)
            {
                _skData.Dispose();
                _skData = null;
                LoadStatic(path);
                return;
            }

            if (_codec.FrameCount <= 1)
            {
                // Single-frame WEBP/GIF — decode via SkiaSharp for reliable WEBP support
                var info = new SKImageInfo(_codec.Info.Width, _codec.Info.Height,
                    SKColorType.Bgra8888, SKAlphaType.Premul);
                using var bmp = new SKBitmap(info);
                _codec.GetPixels(info, bmp.GetPixels());

                var wb = new WriteableBitmap(info.Width, info.Height, 96, 96,
                    PixelFormats.Pbgra32, null);
                wb.WritePixels(
                    new Int32Rect(0, 0, info.Width, info.Height),
                    bmp.GetPixels(), bmp.RowBytes * bmp.Height, bmp.RowBytes);
                wb.Freeze();
                Source = wb;

                _codec.Dispose(); _codec = null;
                _skData.Dispose(); _skData = null;
                return;
            }

            // Multi-frame (animated) — decode all frames
            DecodeAllFrames();

            if (_frames is null || _frames.Count == 0)
            {
                LoadStatic(path);
                return;
            }

            var firstFrame = _frames[0];
            _wb = new WriteableBitmap(firstFrame.Width, firstFrame.Height, 96, 96,
                PixelFormats.Pbgra32, null);
            Source = _wb;

            _frameIndex = 0;
            RenderFrame(0);
            StartTimer();
        }
        catch
        {
            Cleanup();
            LoadStatic(path);
        }
    }

    private void DecodeAllFrames()
    {
        if (_codec is null) return;

        var info = new SKImageInfo(_codec.Info.Width, _codec.Info.Height,
            SKColorType.Bgra8888, SKAlphaType.Premul);
        _frames = new List<SKBitmap>(_codec.FrameCount);
        _delays = new List<int>(_codec.FrameCount);

        for (int i = 0; i < _codec.FrameCount; i++)
        {
            var fi = _codec.FrameInfo[i];
            var frameBmp = new SKBitmap(info);

            // If this frame requires a previous frame, copy it as the base
            if (fi.RequiredFrame >= 0 && fi.RequiredFrame < _frames.Count)
            {
                _frames[fi.RequiredFrame].CopyTo(frameBmp);
            }

            _codec.GetPixels(info, frameBmp.GetPixels(), new SKCodecOptions(i));
            _frames.Add(frameBmp);
            _delays.Add(fi.Duration > 0 ? fi.Duration : 100);
        }

        // Free the codec/data after decoding all frames into memory
        _codec.Dispose(); _codec = null;
        _skData?.Dispose(); _skData = null;
    }

    private void RenderFrame(int index)
    {
        if (_frames is null || _wb is null || index >= _frames.Count) return;

        var frame = _frames[index];
        _wb.WritePixels(
            new Int32Rect(0, 0, frame.Width, frame.Height),
            frame.GetPixels(), frame.RowBytes * frame.Height, frame.RowBytes);
    }

    private void StartTimer()
    {
        if (_delays is null || _delays.Count == 0) return;

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(_delays[0])
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_frames is null || _delays is null) return;

        _frameIndex = (_frameIndex + 1) % _frames.Count;
        RenderFrame(_frameIndex);

        _timer!.Interval = TimeSpan.FromMilliseconds(_delays[_frameIndex]);
    }

    private void LoadStatic(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = fs;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            Source = bitmap;
        }
        catch { }
    }

    private void Cleanup()
    {
        _timer?.Stop();
        _timer = null;

        if (_frames is not null)
        {
            foreach (var f in _frames) f.Dispose();
            _frames = null;
        }
        _delays = null;

        _codec?.Dispose(); _codec = null;
        _skData?.Dispose(); _skData = null;
        _wb = null;
        _frameIndex = 0;
    }
}
