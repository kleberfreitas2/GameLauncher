using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace GameLauncher.Controls;

public class AnimatedImage : Image
{
    private List<byte[]>? _framePixels;
    private long[]? _cumulativeMs;
    private long _totalDurationMs;
    private int _frameWidth, _frameHeight, _frameStride;
    private WriteableBitmap? _wb;
    private int _frameIndex;
    private TimeSpan _lastRenderTime;
    private double _elapsedMs;
    private bool _renderingAttached;
    private CancellationTokenSource? _decodeCts;

    public static readonly DependencyProperty ImagePathProperty =
        DependencyProperty.Register(nameof(ImagePath), typeof(string), typeof(AnimatedImage),
            new PropertyMetadata(null, (d, _) => ((AnimatedImage)d).Reload()));

    private static readonly DependencyPropertyKey IsLoadingPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsLoading), typeof(bool), typeof(AnimatedImage),
            new PropertyMetadata(false));
    public static readonly DependencyProperty IsLoadingProperty = IsLoadingPropertyKey.DependencyProperty;
    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        private set => SetValue(IsLoadingPropertyKey, value);
    }

    private static readonly DependencyPropertyKey LoadingProgressPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(LoadingProgress), typeof(double), typeof(AnimatedImage),
            new PropertyMetadata(0.0));
    public static readonly DependencyProperty LoadingProgressProperty = LoadingProgressPropertyKey.DependencyProperty;
    public double LoadingProgress
    {
        get => (double)GetValue(LoadingProgressProperty);
        private set => SetValue(LoadingProgressPropertyKey, value);
    }

    public string? ImagePath
    {
        get => (string?)GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public static readonly DependencyProperty ImageDataProperty =
        DependencyProperty.Register(nameof(ImageData), typeof(object), typeof(AnimatedImage),
            new PropertyMetadata(null, (d, _) => ((AnimatedImage)d).ReloadFromData()));

    public object? ImageData
    {
        get => GetValue(ImageDataProperty);
        set => SetValue(ImageDataProperty, value);
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
            TryLoadAnimated(path);
        else
            LoadStatic(path);
    }

    private void TryLoadAnimated(string path)
    {
        try
        {
            var skData = SKData.Create(path);
            var codec = SKCodec.Create(skData);

            if (codec is null)
            {
                skData.Dispose();
                LoadStatic(path);
                return;
            }

            if (codec.FrameCount <= 1)
            {
                DecodeSingleFrame(codec, skData);
                return;
            }

            _ = DecodeAnimatedAsync(codec, skData);
        }
        catch
        {
            Cleanup();
            LoadStatic(path);
        }
    }

    private (double dpiX, double dpiY) GetEffectiveDpi()
    {
        try
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            if (dpi.PixelsPerInchX > 0 && dpi.PixelsPerInchY > 0)
                return (dpi.PixelsPerInchX, dpi.PixelsPerInchY);
        }
        catch { }
        return (96.0, 96.0);
    }

    private void DecodeSingleFrame(SKCodec codec, SKData skData)
    {
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height,
            SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bmp = new SKBitmap(info);
        codec.GetPixels(info, bmp.GetPixels());

        var (dpiX, dpiY) = GetEffectiveDpi();
        var wb = new WriteableBitmap(info.Width, info.Height, dpiX, dpiY,
            PixelFormats.Pbgra32, null);
        wb.WritePixels(
            new Int32Rect(0, 0, info.Width, info.Height),
            bmp.GetPixels(), bmp.RowBytes * bmp.Height, bmp.RowBytes);
        wb.Freeze();
        Source = wb;

        codec.Dispose();
        skData.Dispose();
    }

    private async Task DecodeAnimatedAsync(SKCodec codec, SKData skData)
    {
        IsLoading = true;
        LoadingProgress = 0;

        var cts = new CancellationTokenSource();
        _decodeCts = cts;

        try
        {
            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height,
                SKColorType.Bgra8888, SKAlphaType.Premul);
            _frameWidth = info.Width;
            _frameHeight = info.Height;

            using (var firstBmp = new SKBitmap(info))
            {
                codec.GetPixels(info, firstBmp.GetPixels(), new SKCodecOptions(0));
                _frameStride = firstBmp.RowBytes;

                var firstPixels = new byte[firstBmp.RowBytes * firstBmp.Height];
                Marshal.Copy(firstBmp.GetPixels(), firstPixels, 0, firstPixels.Length);

                var (dpiX, dpiY) = GetEffectiveDpi();
                _wb = new WriteableBitmap(info.Width, info.Height, dpiX, dpiY, PixelFormats.Pbgra32, null);
                _wb.WritePixels(new Int32Rect(0, 0, _frameWidth, _frameHeight), firstPixels, _frameStride, 0);
                Source = _wb;
            }

            var frameCount = codec.FrameCount;
            var progress = new Progress<int>(pct => LoadingProgress = pct);

            var (framePixels, cumulative, total) = await Task.Run(() =>
            {
                var pixels = new List<byte[]>(frameCount);
                var delays = new List<int>(frameCount);
                var skBitmaps = new List<SKBitmap>(frameCount);

                try
                {
                    for (int i = 0; i < frameCount; i++)
                    {
                        cts.Token.ThrowIfCancellationRequested();

                        var fi = codec.FrameInfo[i];
                        var frameBmp = new SKBitmap(info);

                        if (fi.RequiredFrame >= 0 && fi.RequiredFrame < skBitmaps.Count)
                            skBitmaps[fi.RequiredFrame].CopyTo(frameBmp);

                        codec.GetPixels(info, frameBmp.GetPixels(), new SKCodecOptions(i));
                        skBitmaps.Add(frameBmp);

                        var frameBytes = new byte[frameBmp.RowBytes * frameBmp.Height];
                        Marshal.Copy(frameBmp.GetPixels(), frameBytes, 0, frameBytes.Length);
                        pixels.Add(frameBytes);
                        delays.Add(fi.Duration > 0 ? fi.Duration : 100);

                        ((IProgress<int>)progress).Report((i + 1) * 100 / frameCount);
                    }
                }
                finally
                {
                    foreach (var b in skBitmaps) b.Dispose();
                    codec.Dispose();
                    skData.Dispose();
                }

                var cum = new long[frameCount];
                long t = 0;
                for (int i = 0; i < frameCount; i++)
                {
                    t += delays[i];
                    cum[i] = t;
                }

                return (pixels, cum, t);
            }, cts.Token);

            if (cts.IsCancellationRequested) return;

            _framePixels = framePixels;
            _cumulativeMs = cumulative;
            _totalDurationMs = total;
            _frameIndex = 0;
            StartRendering();
        }
        catch (OperationCanceledException) { }
        catch
        {
            Cleanup();
        }
        finally
        {
            IsLoading = false;
            LoadingProgress = 100;
        }
    }

    private void StartRendering()
    {
        if (_renderingAttached) return;
        _lastRenderTime = TimeSpan.Zero;
        _elapsedMs = 0;
        _frameIndex = 0;
        CompositionTarget.Rendering += OnRendering;
        _renderingAttached = true;
    }

    private void StopRendering()
    {
        if (!_renderingAttached) return;
        CompositionTarget.Rendering -= OnRendering;
        _renderingAttached = false;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_framePixels is null || _wb is null || _cumulativeMs is null) return;

        var args = (RenderingEventArgs)e;
        if (_lastRenderTime == TimeSpan.Zero)
        {
            _lastRenderTime = args.RenderingTime;
            return;
        }

        var delta = (args.RenderingTime - _lastRenderTime).TotalMilliseconds;
        _lastRenderTime = args.RenderingTime;
        _elapsedMs += delta;

        if (_totalDurationMs > 0 && _elapsedMs >= _totalDurationMs)
            _elapsedMs %= _totalDurationMs;

        int newIndex = _cumulativeMs.Length - 1;
        for (int i = 0; i < _cumulativeMs.Length; i++)
        {
            if (_elapsedMs < _cumulativeMs[i])
            {
                newIndex = i;
                break;
            }
        }

        if (newIndex != _frameIndex)
        {
            _frameIndex = newIndex;
            _wb.WritePixels(new Int32Rect(0, 0, _frameWidth, _frameHeight),
                _framePixels[_frameIndex], _frameStride, 0);
        }
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

    private void ReloadFromData()
    {
        Cleanup();
        Source = null;

        if (ImageData is not byte[] data || data.Length == 0)
            return;

        TryLoadAnimatedFromBytes(data);
    }

    private void TryLoadAnimatedFromBytes(byte[] bytes)
    {
        try
        {
            var skData = SKData.CreateCopy(bytes);
            var codec = SKCodec.Create(skData);

            if (codec is null)
            {
                skData.Dispose();
                LoadStaticFromBytes(bytes);
                return;
            }

            if (codec.FrameCount <= 1)
            {
                DecodeSingleFrame(codec, skData);
                return;
            }

            _ = DecodeAnimatedAsync(codec, skData);
        }
        catch
        {
            Cleanup();
            LoadStaticFromBytes(bytes);
        }
    }

    private void LoadStaticFromBytes(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = ms;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            Source = bitmap;
        }
        catch { }
    }

    private void Cleanup()
    {
        _decodeCts?.Cancel();
        _decodeCts = null;
        StopRendering();
        _framePixels = null;
        _cumulativeMs = null;
        _totalDurationMs = 0;
        _wb = null;
        _frameIndex = 0;
        _elapsedMs = 0;
        _lastRenderTime = TimeSpan.Zero;
        IsLoading = false;
        LoadingProgress = 0;
    }
}
