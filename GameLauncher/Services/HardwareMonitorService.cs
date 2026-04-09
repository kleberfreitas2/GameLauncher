using System;
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
}

public sealed class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer;
    private readonly Timer _timer;
    private bool _disposed;

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

        _timer = new Timer(intervalMs);
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

            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                foreach (var sub in hw.SubHardware)
                    sub.Update();

                switch (hw.HardwareType)
                {
                    case HardwareType.Cpu:
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
                            { ramUsage = sensor.Value ?? 0; break; }
                        }
                        break;
                }
            }

            MetricsUpdated?.Invoke(new HardwareMetrics
            {
                CpuUsage = cpuUsage,
                CpuTemp = cpuTemp,
                GpuUsage = gpuUsage,
                GpuTemp = gpuTemp,
                RamUsage = ramUsage
            });
        }
        catch { }
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
