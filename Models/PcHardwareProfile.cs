namespace GameLauncher.Models;

/// <summary>Perfil completo de hardware do PC do usuário.</summary>
public class PcHardwareProfile
{
    public string CpuName         { get; init; } = "";
    public int    CpuCores        { get; init; }
    public double CpuClockGhz     { get; init; }
    public string GpuName         { get; init; } = "";
    public int    GpuVramGb       { get; init; }
    public int    GpuScore        { get; init; }  // 0–100 (RTX 4090 = 100)
    public double RamGb           { get; init; }

    public string CpuDisplay  => CpuCores > 0 ? $"{CpuName} ({CpuCores} núcleos)" : CpuName;
    public string GpuDisplay  => GpuVramGb > 0 ? $"{GpuName} ({GpuVramGb} GB VRAM)" : GpuName;
    public string RamDisplay  => RamGb > 0     ? $"{RamGb:F0} GB RAM" : "RAM desconhecida";
    public string ClockDisplay => CpuClockGhz > 0 ? $"{CpuClockGhz:F1} GHz" : "";

    public bool IsComplete => !string.IsNullOrEmpty(GpuName) && RamGb > 0;
}
