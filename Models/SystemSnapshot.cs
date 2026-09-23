namespace Jarvis.Models;

public sealed class SystemSnapshot
{
    public double CpuPercent { get; init; }
    public double RamPercent { get; init; }
    public double DiskPercent { get; init; }
    public double NetworkKbps { get; init; }
    public string BatteryStatus { get; init; } = "";
    public TimeSpan Uptime { get; init; }
    public string GpuStatus { get; init; } = "N/A";
    public string TemperatureStatus { get; init; } = "N/A";
}
