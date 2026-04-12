using System.Text.RegularExpressions;

namespace GameLauncher.Services;

public partial class GpuCapabilities
{
    public string GpuName { get; init; } = "";
    public GpuVendor Vendor { get; init; }
    public bool SupportsRayTracing { get; init; }
    public bool SupportsDLSS { get; init; }
    public string? MaxDlssVersion { get; init; }
    public bool SupportsFSR { get; init; }
    public string? FsrNote { get; init; }
    public bool SupportsXeSS { get; init; }
    public bool SupportsFrameGeneration { get; init; }
    public bool SupportsHDR { get; init; }
    public bool SupportsDirectX12 { get; init; }
    public bool SupportsVulkan { get; init; }
}

public enum GpuVendor { Unknown, Nvidia, Amd, Intel }

public static partial class GpuCapabilityService
{
    public static GpuCapabilities Detect(string gpuName)
    {
        if (string.IsNullOrWhiteSpace(gpuName))
            return new GpuCapabilities { GpuName = gpuName };

        var upper = gpuName.ToUpperInvariant();

        if (upper.Contains("NVIDIA") || upper.Contains("GEFORCE") || upper.Contains("RTX") || upper.Contains("GTX"))
            return DetectNvidia(gpuName, upper);

        if (upper.Contains("AMD") || upper.Contains("RADEON"))
            return DetectAmd(gpuName, upper);

        if (upper.Contains("INTEL") || upper.Contains("ARC"))
            return DetectIntel(gpuName, upper);

        return new GpuCapabilities
        {
            GpuName = gpuName,
            Vendor = GpuVendor.Unknown,
            SupportsDirectX12 = true,
            SupportsVulkan = true,
            SupportsHDR = true
        };
    }

    private static GpuCapabilities DetectNvidia(string name, string upper)
    {
        var series = ExtractNvidiaSeries(upper);

        return series switch
        {
            >= 5000 => new GpuCapabilities
            {
                GpuName = name, Vendor = GpuVendor.Nvidia,
                SupportsRayTracing = true, SupportsDLSS = true, MaxDlssVersion = "4.0",
                SupportsFSR = true, SupportsXeSS = true,
                SupportsFrameGeneration = true, SupportsHDR = true,
                SupportsDirectX12 = true, SupportsVulkan = true
            },
            >= 4000 => new GpuCapabilities
            {
                GpuName = name, Vendor = GpuVendor.Nvidia,
                SupportsRayTracing = true, SupportsDLSS = true, MaxDlssVersion = "3.5",
                SupportsFSR = true, SupportsXeSS = true,
                SupportsFrameGeneration = true, SupportsHDR = true,
                SupportsDirectX12 = true, SupportsVulkan = true
            },
            >= 3000 => new GpuCapabilities
            {
                GpuName = name, Vendor = GpuVendor.Nvidia,
                SupportsRayTracing = true, SupportsDLSS = true, MaxDlssVersion = "3.5",
                SupportsFSR = true, SupportsXeSS = true,
                SupportsFrameGeneration = false, SupportsHDR = true,
                SupportsDirectX12 = true, SupportsVulkan = true
            },
            >= 2000 => new GpuCapabilities
            {
                GpuName = name, Vendor = GpuVendor.Nvidia,
                SupportsRayTracing = true, SupportsDLSS = true, MaxDlssVersion = "2.x",
                SupportsFSR = true, SupportsXeSS = true,
                SupportsFrameGeneration = false, SupportsHDR = true,
                SupportsDirectX12 = true, SupportsVulkan = true
            },
            >= 1000 => new GpuCapabilities
            {
                GpuName = name, Vendor = GpuVendor.Nvidia,
                SupportsRayTracing = false, SupportsDLSS = false,
                SupportsFSR = true, SupportsXeSS = true,
                SupportsFrameGeneration = false, SupportsHDR = true,
                SupportsDirectX12 = true, SupportsVulkan = true
            },
            _ => new GpuCapabilities
            {
                GpuName = name, Vendor = GpuVendor.Nvidia,
                SupportsRayTracing = false, SupportsDLSS = false,
                SupportsFSR = upper.Contains("RTX"),
                SupportsDirectX12 = true, SupportsVulkan = true,
                SupportsHDR = true
            }
        };
    }

    private static int ExtractNvidiaSeries(string upper)
    {
        if (upper.Contains("RTX"))
        {
            var m = RtxPattern().Match(upper);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n))
                return n >= 2000 ? n : n * 10;
        }

        if (upper.Contains("GTX"))
        {
            var m = GtxPattern().Match(upper);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n))
                return n >= 1000 ? n : n * 10;
        }

        return 0;
    }

    private static GpuCapabilities DetectAmd(string name, string upper)
    {
        var series = ExtractAmdSeries(upper);

        return series switch
        {
            >= 9000 => new GpuCapabilities
            {
                GpuName = name, Vendor = GpuVendor.Amd,
                SupportsRayTracing = true, SupportsDLSS = false,
                SupportsFSR = true, FsrNote = "FSR 4",
                SupportsXeSS = true, SupportsFrameGeneration = true,
                SupportsHDR = true, SupportsDirectX12 = true, SupportsVulkan = true
            },
            >= 7000 => new GpuCapabilities
            {
                GpuName = name, Vendor = GpuVendor.Amd,
                SupportsRayTracing = true, SupportsDLSS = false,
                SupportsFSR = true, FsrNote = "FSR 3",
                SupportsXeSS = true, SupportsFrameGeneration = true,
                SupportsHDR = true, SupportsDirectX12 = true, SupportsVulkan = true
            },
            >= 6000 => new GpuCapabilities
            {
                GpuName = name, Vendor = GpuVendor.Amd,
                SupportsRayTracing = true, SupportsDLSS = false,
                SupportsFSR = true, FsrNote = "FSR 2",
                SupportsXeSS = true, SupportsFrameGeneration = false,
                SupportsHDR = true, SupportsDirectX12 = true, SupportsVulkan = true
            },
            >= 5000 => new GpuCapabilities
            {
                GpuName = name, Vendor = GpuVendor.Amd,
                SupportsRayTracing = false, SupportsDLSS = false,
                SupportsFSR = true, FsrNote = "FSR 1",
                SupportsXeSS = true, SupportsFrameGeneration = false,
                SupportsHDR = true, SupportsDirectX12 = true, SupportsVulkan = true
            },
            _ => new GpuCapabilities
            {
                GpuName = name, Vendor = GpuVendor.Amd,
                SupportsFSR = true, SupportsHDR = true,
                SupportsDirectX12 = true, SupportsVulkan = true
            }
        };
    }

    private static int ExtractAmdSeries(string upper)
    {
        var m = RxPattern().Match(upper);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n))
            return n >= 5000 ? n : n * 10;
        return 0;
    }

    private static GpuCapabilities DetectIntel(string name, string upper)
    {
        bool isArc = upper.Contains("ARC");
        bool isBSeries = isArc && (upper.Contains(" B") || upper.Contains("B5") || upper.Contains("B7") || upper.Contains("B9"));

        if (isArc)
        {
            return new GpuCapabilities
            {
                GpuName = name, Vendor = GpuVendor.Intel,
                SupportsRayTracing = true, SupportsDLSS = false,
                SupportsFSR = true, SupportsXeSS = true,
                SupportsFrameGeneration = isBSeries,
                SupportsHDR = true, SupportsDirectX12 = true, SupportsVulkan = true
            };
        }

        return new GpuCapabilities
        {
            GpuName = name, Vendor = GpuVendor.Intel,
            SupportsHDR = true, SupportsDirectX12 = true
        };
    }

    [GeneratedRegex(@"RTX\s*(\d{3,4})")]
    private static partial Regex RtxPattern();

    [GeneratedRegex(@"GTX\s*(\d{3,4})")]
    private static partial Regex GtxPattern();

    [GeneratedRegex(@"RX\s*(\d{4})")]
    private static partial Regex RxPattern();
}
