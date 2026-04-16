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

    // ─── Performance Score (0–100, RTX 4090 = 100) ───────────────────────

    /// <summary>
    /// Retorna uma pontuação de desempenho estimada (0–100) com base no nome da GPU.
    /// Usada pelo motor de recomendação gráfica.
    /// </summary>
    public static int GetPerformanceScore(string gpuName)
    {
        if (string.IsNullOrWhiteSpace(gpuName)) return 0;
        var u = gpuName.ToUpperInvariant();

        // ── Nvidia RTX 50xx ────────────────────────────────────────────────
        if (u.Contains("RTX 5090") || u.Contains("RTX5090")) return 100;
        if (u.Contains("RTX 5080") || u.Contains("RTX5080")) return 90;
        if (u.Contains("RTX 5070 TI")  || u.Contains("RTX5070TI"))  return 80;
        if (u.Contains("RTX 5070") || u.Contains("RTX5070")) return 72;
        if (u.Contains("RTX 5060 TI")  || u.Contains("RTX5060TI"))  return 62;
        if (u.Contains("RTX 5060") || u.Contains("RTX5060")) return 52;

        // ── Nvidia RTX 40xx ────────────────────────────────────────────────
        if (u.Contains("RTX 4090") || u.Contains("RTX4090")) return 100;
        if (u.Contains("RTX 4080 SUPER")) return 87;
        if (u.Contains("RTX 4080") || u.Contains("RTX4080")) return 84;
        if (u.Contains("RTX 4070 TI SUPER") || u.Contains("RTX 4070TI SUPER")) return 80;
        if (u.Contains("RTX 4070 TI")  || u.Contains("RTX4070TI")  || u.Contains("RTX 4070TI"))  return 76;
        if (u.Contains("RTX 4070 SUPER")) return 72;
        if (u.Contains("RTX 4070") || u.Contains("RTX4070")) return 65;
        if (u.Contains("RTX 4060 TI")  || u.Contains("RTX4060TI")  || u.Contains("RTX 4060TI"))  return 56;
        if (u.Contains("RTX 4060") || u.Contains("RTX4060")) return 48;
        if (u.Contains("RTX 40")) return 60;

        // ── Nvidia RTX 30xx ────────────────────────────────────────────────
        if (u.Contains("RTX 3090 TI")  || u.Contains("RTX3090TI"))  return 82;
        if (u.Contains("RTX 3090") || u.Contains("RTX3090")) return 80;
        if (u.Contains("RTX 3080 TI")  || u.Contains("RTX3080TI"))  return 78;
        if (u.Contains("RTX 3080 12")) return 75;
        if (u.Contains("RTX 3080") || u.Contains("RTX3080")) return 72;
        if (u.Contains("RTX 3070 TI")  || u.Contains("RTX3070TI"))  return 64;
        if (u.Contains("RTX 3070") || u.Contains("RTX3070")) return 60;
        if (u.Contains("RTX 3060 TI")  || u.Contains("RTX3060TI"))  return 54;
        if (u.Contains("RTX 3060") || u.Contains("RTX3060")) return 46;
        if (u.Contains("RTX 3050 TI")  || u.Contains("RTX3050TI"))  return 34;
        if (u.Contains("RTX 3050") || u.Contains("RTX3050")) return 30;
        if (u.Contains("RTX 30")) return 48;

        // ── Nvidia RTX 20xx ────────────────────────────────────────────────
        if (u.Contains("RTX 2080 TI")  || u.Contains("RTX2080TI"))  return 62;
        if (u.Contains("RTX 2080 SUPER")) return 58;
        if (u.Contains("RTX 2080") || u.Contains("RTX2080")) return 55;
        if (u.Contains("RTX 2070 SUPER")) return 52;
        if (u.Contains("RTX 2070") || u.Contains("RTX2070")) return 48;
        if (u.Contains("RTX 2060 SUPER")) return 44;
        if (u.Contains("RTX 2060") || u.Contains("RTX2060")) return 40;
        if (u.Contains("RTX 20")) return 44;

        // ── Nvidia GTX 16xx ────────────────────────────────────────────────
        if (u.Contains("GTX 1660 SUPER")) return 32;
        if (u.Contains("GTX 1660 TI") || u.Contains("GTX 1660TI")) return 33;
        if (u.Contains("GTX 1660")) return 28;
        if (u.Contains("GTX 1650 SUPER")) return 24;
        if (u.Contains("GTX 1650")) return 18;

        // ── Nvidia GTX 10xx ────────────────────────────────────────────────
        if (u.Contains("GTX 1080 TI") || u.Contains("GTX 1080TI")) return 48;
        if (u.Contains("GTX 1080")) return 42;
        if (u.Contains("GTX 1070 TI") || u.Contains("GTX 1070TI")) return 36;
        if (u.Contains("GTX 1070")) return 34;
        if (u.Contains("GTX 1060 6")) return 24;
        if (u.Contains("GTX 1060 3")) return 20;
        if (u.Contains("GTX 1060")) return 22;
        if (u.Contains("GTX 1050 TI") || u.Contains("GTX 1050TI")) return 15;
        if (u.Contains("GTX 1050")) return 12;
        if (u.Contains("GTX 10")) return 20;

        // ── Nvidia GTX 9xx ─────────────────────────────────────────────────
        if (u.Contains("GTX 980 TI") || u.Contains("GTX980TI")) return 28;
        if (u.Contains("GTX 980")) return 22;
        if (u.Contains("GTX 970")) return 18;
        if (u.Contains("GTX 960")) return 14;
        if (u.Contains("GTX 9")) return 14;

        // ── Fallback Nvidia ────────────────────────────────────────────────
        if (u.Contains("RTX")) return 45;
        if (u.Contains("GTX")) return 18;

        // ── AMD RX 7xxx ────────────────────────────────────────────────────
        if (u.Contains("RX 7900 XTX")) return 88;
        if (u.Contains("RX 7900 XT"))  return 82;
        if (u.Contains("RX 7900 GRE")) return 76;
        if (u.Contains("RX 7800 XT"))  return 66;
        if (u.Contains("RX 7700 XT"))  return 60;
        if (u.Contains("RX 7600 XT"))  return 52;
        if (u.Contains("RX 7600"))     return 46;
        if (u.Contains("RX 7"))        return 55;

        // ── AMD RX 6xxx ────────────────────────────────────────────────────
        if (u.Contains("RX 6950 XT"))  return 82;
        if (u.Contains("RX 6900 XT"))  return 78;
        if (u.Contains("RX 6800 XT"))  return 72;
        if (u.Contains("RX 6800"))     return 66;
        if (u.Contains("RX 6750 XT"))  return 58;
        if (u.Contains("RX 6700 XT"))  return 56;
        if (u.Contains("RX 6700"))     return 52;
        if (u.Contains("RX 6650 XT"))  return 47;
        if (u.Contains("RX 6600 XT"))  return 43;
        if (u.Contains("RX 6600"))     return 39;
        if (u.Contains("RX 6500 XT"))  return 22;
        if (u.Contains("RX 6400"))     return 16;
        if (u.Contains("RX 6"))        return 48;

        // ── AMD RX 5xxx ────────────────────────────────────────────────────
        if (u.Contains("RX 5700 XT"))  return 44;
        if (u.Contains("RX 5700"))     return 40;
        if (u.Contains("RX 5600 XT"))  return 36;
        if (u.Contains("RX 5500 XT"))  return 25;
        if (u.Contains("RX 5"))        return 33;

        // ── AMD RX 5xx legado ──────────────────────────────────────────────
        if (u.Contains("RX 590"))  return 22;
        if (u.Contains("RX 580"))  return 20;
        if (u.Contains("RX 570"))  return 17;
        if (u.Contains("RX 480"))  return 19;
        if (u.Contains("RX 470"))  return 16;
        if (u.Contains("VEGA 64")) return 30;
        if (u.Contains("VEGA 56")) return 26;

        // ── Fallback AMD ───────────────────────────────────────────────────
        if (u.Contains("RADEON") || u.Contains("AMD RX")) return 22;

        // ── Intel Arc ─────────────────────────────────────────────────────
        if (u.Contains("ARC B770")) return 56;
        if (u.Contains("ARC B580")) return 47;
        if (u.Contains("ARC A770")) return 43;
        if (u.Contains("ARC A750")) return 39;
        if (u.Contains("ARC A580")) return 32;
        if (u.Contains("ARC A380")) return 18;
        if (u.Contains("ARC"))      return 30;

        // ── Intel integrada ────────────────────────────────────────────────
        if (u.Contains("IRIS XE")) return 8;
        if (u.Contains("INTEL HD") || u.Contains("UHD")) return 4;

        return 18; // desconhecida — assume valor conservador
    }
}
