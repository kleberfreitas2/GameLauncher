using System.Diagnostics;
using System.IO;
using GameLauncher.Models;

namespace GameLauncher.Services;

public static class GameTechDetectorService
{
    private static readonly string[] DlssDllNames =
    [
        "nvngx_dlss.dll", "nvngx_dlssd.dll", "nvngx_dlssg.dll",
        "libxess.dll", "_nvngx.dll"
    ];

    private static readonly string[] FsrDllPatterns =
    [
        "amd_fidelityfx_", "ffx_fsr", "fsr2api", "fsr3api",
        "amd_ags", "ffx_frameinterpolation"
    ];

    private static readonly string[] XeSSPatterns = ["libxess.dll", "xess.dll", "xess_d.dll"];

    private static readonly string[] FrameGenPatterns =
    [
        "nvngx_dlssg.dll", "dlssg_to_fsr3.dll", "ffx_frameinterpolation",
        "amd_fidelityfx_framegeneration", "streamline_sl.interposer.dll",
        "sl.dlss_g.dll"
    ];

    private static readonly string[] VulkanPatterns =
    [
        "vulkan-1.dll", "amdvlk", "vulkan_radeon"
    ];

    private static readonly string[] RayTracingPatterns =
    [
        "dxr.dll", "rtxdi", "nvngx_dlssd.dll",
        "rtxgi", "d3d12core.dll"
    ];

    private static readonly string[] HdrPatterns = ["hdr", "scrgb"];

    private static readonly EnumerationOptions SafeSearchOptions = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.System
    };

    public static GameTechInfo Scan(string gameRootDirectory)
    {
        var info = new GameTechInfo();

        if (string.IsNullOrEmpty(gameRootDirectory) || !Directory.Exists(gameRootDirectory))
            return info;

        try
        {
            var files = Directory.EnumerateFiles(gameRootDirectory, "*", SafeSearchOptions)
                .Select(f => Path.GetFileName(f).ToLowerInvariant())
                .ToHashSet();

            info.HasDLSS = files.Any(f => f is "nvngx_dlss.dll" or "nvngx_dlssd.dll");
            if (info.HasDLSS)
                info.DlssVersion = DetectDlssVersion(gameRootDirectory);

            info.HasFSR = files.Any(f => FsrDllPatterns.Any(p => f.Contains(p, StringComparison.OrdinalIgnoreCase)));
            if (info.HasFSR)
                info.FsrVersion = DetectFsrVersion(files);

            info.HasXeSS = files.Any(f => XeSSPatterns.Any(p => f.Equals(p, StringComparison.OrdinalIgnoreCase)));

            info.HasFrameGeneration = files.Any(f =>
                FrameGenPatterns.Any(p => f.Contains(p, StringComparison.OrdinalIgnoreCase)));

            info.HasVulkan = files.Any(f =>
                VulkanPatterns.Any(p => f.Contains(p, StringComparison.OrdinalIgnoreCase)));

            info.HasRayTracing = info.HasDLSS ||
                files.Any(f => RayTracingPatterns.Any(p => f.Contains(p, StringComparison.OrdinalIgnoreCase)));

            var hasD3d12 = files.Any(f => f.Contains("d3d12") || f.Contains("dx12"));
            var hasD3d11 = files.Any(f => f.Contains("d3d11") || f.Contains("dx11") || f.Contains("dxgi.dll"));

            if (hasD3d12 || info.HasRayTracing || info.HasDLSS)
                info.DirectXVersion = "DirectX 12";
            else if (hasD3d11)
                info.DirectXVersion = "DirectX 11";

            info.HasHDR = files.Any(f =>
                HdrPatterns.Any(p => f.Contains(p, StringComparison.OrdinalIgnoreCase))) ||
                ScanConfigFilesForHdr(gameRootDirectory);
        }
        catch { }

        return info;
    }

    private static string? DetectDlssVersion(string rootDir)
    {
        try
        {
            var dlssDll = Directory.EnumerateFiles(rootDir, "nvngx_dlss.dll", SafeSearchOptions)
                .FirstOrDefault();

            if (dlssDll is null) return null;

            var vi = FileVersionInfo.GetVersionInfo(dlssDll);
            if (vi.FileMajorPart > 0)
            {
                var ver = $"{vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart}";
                return vi.FileMajorPart switch
                {
                    >= 4 => $"DLSS 4 ({ver})",
                    >= 3 when vi.FileMinorPart >= 5 => $"DLSS 3.5 ({ver})",
                    >= 3 => $"DLSS 3 ({ver})",
                    >= 2 => $"DLSS 2 ({ver})",
                    _ => $"DLSS ({ver})"
                };
            }
        }
        catch { }
        return "DLSS";
    }

    private static string? DetectFsrVersion(HashSet<string> files)
    {
        if (files.Any(f => f.Contains("fsr3") || f.Contains("ffx_frameinterpolation")))
            return "FSR 3";
        if (files.Any(f => f.Contains("fsr2")))
            return "FSR 2";
        return "FSR";
    }

    private static bool ScanConfigFilesForHdr(string rootDir)
    {
        try
        {
            var configFiles = Directory.EnumerateFiles(rootDir, "*.ini", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(rootDir, "*.cfg", SearchOption.TopDirectoryOnly))
                .Take(20);

            foreach (var cfg in configFiles)
            {
                try
                {
                    var content = File.ReadAllText(cfg);
                    if (content.Contains("HDR", StringComparison.OrdinalIgnoreCase) &&
                        (content.Contains("hdr_enable", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("HDROutput", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("EnableHDR", StringComparison.OrdinalIgnoreCase)))
                        return true;
                }
                catch { }
            }
        }
        catch { }
        return false;
    }
}
