using System.Management;
using GameLauncher.Models;

namespace GameLauncher.Services;

/// <summary>
/// Lê especificações detalhadas do hardware via WMI e monta um <see cref="PcHardwareProfile"/>.
/// Combina dados do <see cref="HardwareMonitorService"/> (já em execução) com consultas
/// WMI pontuais para VRAM, número de núcleos e RAM total.
/// </summary>
public static class HardwareProfileService
{
    private static PcHardwareProfile? _cached;

    /// <summary>Retorna o perfil (cache após primeira leitura).</summary>
    public static PcHardwareProfile Get(string gpuNameFromMonitor = "", string cpuNameFromMonitor = "", string ramTotalFromMonitor = "")
    {
        if (_cached is not null) return _cached;

        var cpuName  = cpuNameFromMonitor;
        var gpuName  = gpuNameFromMonitor;
        int cpuCores = 0;
        double cpuGhz = 0;
        int gpuVram  = 0;
        double ramGb = ParseRamGb(ramTotalFromMonitor);

        // ── CPU via WMI ────────────────────────────────────────────────────
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name, NumberOfCores, MaxClockSpeed FROM Win32_Processor");
            foreach (var obj in s.Get())
            {
                if (string.IsNullOrEmpty(cpuName))
                    cpuName = obj["Name"]?.ToString()?.Trim() ?? "";
                cpuCores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                var mhz  = Convert.ToDouble(obj["MaxClockSpeed"] ?? 0);
                cpuGhz   = mhz / 1000.0;
                break;
            }
        }
        catch { }

        // ── GPU + VRAM via WMI ─────────────────────────────────────────────
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT Name, AdapterRAM FROM Win32_VideoController");
            foreach (var obj in s.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim() ?? "";
                // Preferir GPU dedicada (não Intel HD integrada)
                bool isIntegrated = name.Contains("Intel HD", StringComparison.OrdinalIgnoreCase)
                                 || name.Contains("Intel UHD", StringComparison.OrdinalIgnoreCase)
                                 || name.Contains("Iris Xe", StringComparison.OrdinalIgnoreCase);
                if (!isIntegrated || string.IsNullOrEmpty(gpuName))
                {
                    if (string.IsNullOrEmpty(gpuName)) gpuName = name;

                    var rawVram = Convert.ToUInt64(obj["AdapterRAM"] ?? 0UL);
                    var vramGb  = (int)Math.Round(rawVram / (1024.0 * 1024 * 1024));

                    // AdapterRAM trava em ~4 GB para GPUs com mais VRAM (overflow 32-bit)
                    // Nesse caso infere do nome da GPU
                    if (vramGb <= 4 && vramGb > 0)
                    {
                        var inferred = InferVramFromName(name);
                        gpuVram = inferred > 0 ? inferred : vramGb;
                    }
                    else if (vramGb > 4)
                    {
                        gpuVram = vramGb;
                    }
                    else
                    {
                        gpuVram = InferVramFromName(name);
                    }

                    if (!isIntegrated) break; // GPU dedicada encontrada — parar
                }
            }
        }
        catch { }

        // ── RAM via WMI se não disponível ───────────────────────────────────
        if (ramGb <= 0)
        {
            try
            {
                using var s = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in s.Get())
                {
                    var bytes = Convert.ToUInt64(obj["TotalPhysicalMemory"] ?? 0UL);
                    ramGb = bytes / (1024.0 * 1024 * 1024);
                    break;
                }
            }
            catch { }
        }

        var gpuScore = GpuCapabilityService.GetPerformanceScore(gpuName);

        _cached = new PcHardwareProfile
        {
            CpuName    = CleanCpuName(cpuName),
            CpuCores   = cpuCores,
            CpuClockGhz = Math.Round(cpuGhz, 1),
            GpuName    = gpuName,
            GpuVramGb  = gpuVram,
            GpuScore   = gpuScore,
            RamGb      = Math.Round(ramGb, 0)
        };
        return _cached;
    }

    /// <summary>Invalida o cache (para forçar releitura).</summary>
    public static void Invalidate() => _cached = null;

    // ─── Helpers ───────────────────────────────────────────────────────────

    private static double ParseRamGb(string ramText)
    {
        if (string.IsNullOrWhiteSpace(ramText)) return 0;
        var clean = ramText.Replace("GB", "", StringComparison.OrdinalIgnoreCase).Trim();
        return double.TryParse(clean, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static string CleanCpuName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        // Remove sufixos longos: "(R)", "(TM)", " CPU @ X.XXGHz"
        return System.Text.RegularExpressions.Regex.Replace(name, @"\(R\)|\(TM\)| CPU @ \S+", "")
               .Replace("  ", " ").Trim();
    }

    /// <summary>Infere VRAM em GB a partir do nome da GPU quando WMI reporta valor incorreto.</summary>
    private static int InferVramFromName(string name)
    {
        var u = name.ToUpperInvariant();

        // Padrões comuns: "16GB", "12 GB", etc.
        var m = System.Text.RegularExpressions.Regex.Match(u, @"(\d+)\s*GB");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var gb) && gb is >= 2 and <= 80)
            return gb;

        // Modelos conhecidos com VRAM específica
        if (u.Contains("4090")) return 24;
        if (u.Contains("4080")) return 16;
        if (u.Contains("4070 TI") || u.Contains("4070TI")) return 12;
        if (u.Contains("4070 SUPER")) return 12;
        if (u.Contains("4070")) return 12;
        if (u.Contains("4060 TI") || u.Contains("4060TI")) return 8;
        if (u.Contains("4060")) return 8;
        if (u.Contains("3090")) return 24;
        if (u.Contains("3080 12")) return 12;
        if (u.Contains("3080")) return 10;
        if (u.Contains("3070 TI") || u.Contains("3070TI")) return 8;
        if (u.Contains("3070")) return 8;
        if (u.Contains("3060 TI") || u.Contains("3060TI")) return 8;
        if (u.Contains("3060")) return 12;
        if (u.Contains("3050")) return 8;
        if (u.Contains("2080 TI") || u.Contains("2080TI")) return 11;
        if (u.Contains("2080")) return 8;
        if (u.Contains("2070")) return 8;
        if (u.Contains("2060 SUPER")) return 8;
        if (u.Contains("2060")) return 6;
        if (u.Contains("1660 SUPER") || u.Contains("1660 TI")) return 6;
        if (u.Contains("1660")) return 6;
        if (u.Contains("1650")) return 4;
        if (u.Contains("1080 TI") || u.Contains("1080TI")) return 11;
        if (u.Contains("1080")) return 8;
        if (u.Contains("1070 TI") || u.Contains("1070TI")) return 8;
        if (u.Contains("1070")) return 8;
        if (u.Contains("1060 6") || (u.Contains("1060") && !u.Contains("3"))) return 6;
        if (u.Contains("1060 3")) return 3;
        if (u.Contains("1050 TI") || u.Contains("1050TI")) return 4;
        if (u.Contains("1050")) return 2;
        if (u.Contains("7900 XTX")) return 24;
        if (u.Contains("7900")) return 20;
        if (u.Contains("7800 XT")) return 16;
        if (u.Contains("7700 XT")) return 12;
        if (u.Contains("7600")) return 8;
        if (u.Contains("6950 XT") || u.Contains("6900 XT")) return 16;
        if (u.Contains("6800 XT") || u.Contains("6800")) return 16;
        if (u.Contains("6700 XT")) return 12;
        if (u.Contains("6700")) return 10;
        if (u.Contains("6600 XT") || u.Contains("6600")) return 8;
        if (u.Contains("6500 XT")) return 4;
        if (u.Contains("5700 XT") || u.Contains("5700")) return 8;
        if (u.Contains("5600 XT")) return 6;
        if (u.Contains("580") || u.Contains("570") || u.Contains("480") || u.Contains("470")) return 8;
        if (u.Contains("A770")) return 16;
        if (u.Contains("A750")) return 8;
        if (u.Contains("A580")) return 8;
        if (u.Contains("B770")) return 16;
        if (u.Contains("ARC")) return 8;

        return 0;
    }
}
