using System.Management;
using System.Timers;
using LibreHardwareMonitor.Hardware;

namespace GameLauncher.Services;

public class HardwareMetrics
{
    public float CpuUsage { get; init; }
    public float CpuTemp { get; init; }
    public float GpuUsage { get; init; }
    public float GpuTemp { get; init; }
    public float RamUsage { get; init; }
    public string CpuName { get; init; } = "";
    public string GpuName { get; init; } = "";
    public string RamTotal { get; init; } = "";
    public List<string> StorageDrives { get; init; } = [];
}

public sealed class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer;
    private readonly System.Timers.Timer _timer;
    private bool _disposed;
    private readonly List<string> _storageDrives;

    public event Action<HardwareMetrics>? MetricsUpdated;

    public HardwareMonitorService(double intervalMs = 1500)
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true
        };

        try { _computer.Open(); }
        catch { }

        _storageDrives = QueryStorageDrives();

        _timer = new System.Timers.Timer(intervalMs);
        _timer.Elapsed += OnTimerElapsed;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            float cpuUsage = 0, cpuTemp = 0;
            float gpuUsage = 0, gpuTemp = 0;
            float ramUsage = 0;
            string cpuName = "", gpuName = "";
            float ramUsed = 0, ramAvailable = 0;

            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                foreach (var sub in hw.SubHardware)
                    sub.Update();

                switch (hw.HardwareType)
                {
                    case HardwareType.Cpu:
                        if (string.IsNullOrEmpty(cpuName)) cpuName = hw.Name;
                        foreach (var sensor in hw.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Total"))
                                cpuUsage = sensor.Value ?? 0;
                            if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Package"))
                                cpuTemp = sensor.Value ?? 0;
                        }
                        if (cpuTemp == 0)
                        {
                            foreach (var sensor in hw.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Temperature && sensor.Value > 0)
                                { cpuTemp = sensor.Value ?? 0; break; }
                            }
                        }
                        break;

                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                    case HardwareType.GpuIntel:
                        if (string.IsNullOrEmpty(gpuName)) gpuName = hw.Name;
                        foreach (var sensor in hw.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Core"))
                                gpuUsage = sensor.Value ?? 0;
                            if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Core"))
                                gpuTemp = sensor.Value ?? 0;
                        }
                        break;

                    case HardwareType.Memory:
                        foreach (var sensor in hw.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Memory"))
                                ramUsage = sensor.Value ?? 0;
                            if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Used"))
                                ramUsed = sensor.Value ?? 0;
                            if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Available"))
                                ramAvailable = sensor.Value ?? 0;
                        }
                        break;
                }
            }

            var totalRam = ramUsed + ramAvailable;
            var ramText = totalRam > 0 ? $"{totalRam:F0} GB" : "";

            MetricsUpdated?.Invoke(new HardwareMetrics
            {
                CpuUsage = cpuUsage,
                CpuTemp = cpuTemp,
                GpuUsage = gpuUsage,
                GpuTemp = gpuTemp,
                RamUsage = ramUsage,
                CpuName = cpuName,
                GpuName = gpuName,
                RamTotal = ramText,
                StorageDrives = _storageDrives
            });
        }
        catch { }
    }

    private static List<string> QueryStorageDrives()
    {
        var drives = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Model, Size, MediaType FROM Win32_DiskDrive");
            foreach (var obj in searcher.Get())
            {
                var model = obj["Model"]?.ToString()?.Trim();
                var sizeBytes = obj["Size"] as ulong? ?? 0;
                if (string.IsNullOrEmpty(model)) continue;

                var sizeGb = sizeBytes / (1024.0 * 1024 * 1024);
                var sizeText = sizeGb >= 1000 ? $"{sizeGb / 1024:F1} TB" : $"{sizeGb:F0} GB";
                drives.Add($"{model} ({sizeText})");
            }
        }
        catch { }
        return drives;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Dispose();
        try { _computer.Close(); } catch { }
    }
}
