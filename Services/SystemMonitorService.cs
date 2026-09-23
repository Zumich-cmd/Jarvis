using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using Jarvis.Models;
using Forms = System.Windows.Forms;

namespace Jarvis.Services;

public sealed class SystemMonitorService : IDisposable
{
    private readonly PerformanceCounter _cpuCounter = new("Processor", "% Processor Time", "_Total");
    private readonly PerformanceCounter _ramCounter = new("Memory", "% Committed Bytes In Use");
    private long _lastReceived;
    private long _lastSent;
    private DateTime _lastNetworkSample = DateTime.Now;

    public SystemSnapshot GetSnapshot()
    {
        double diskPercent = 0;
        var systemDrive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.Name.StartsWith(Path.GetPathRoot(Environment.SystemDirectory) ?? "C"));
        if (systemDrive != null && systemDrive.TotalSize > 0)
            diskPercent = 100d - (systemDrive.AvailableFreeSpace * 100d / systemDrive.TotalSize);

        long received = 0;
        long sent = 0;
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up))
        {
            var stats = ni.GetIPv4Statistics();
            received += stats.BytesReceived;
            sent += stats.BytesSent;
        }

        DateTime now = DateTime.Now;
        double seconds = Math.Max(1, (now - _lastNetworkSample).TotalSeconds);
        double networkKbps = ((received - _lastReceived) + (sent - _lastSent)) / 1024d / seconds;
        _lastReceived = received;
        _lastSent = sent;
        _lastNetworkSample = now;

        var power = Forms.SystemInformation.PowerStatus;
        string battery = power.BatteryChargeStatus == Forms.BatteryChargeStatus.NoSystemBattery
            ? "немає батареї"
            : $"{power.BatteryLifePercent * 100:0}%";

        return new SystemSnapshot
        {
            CpuPercent = Math.Clamp(_cpuCounter.NextValue(), 0, 100),
            RamPercent = Math.Clamp(_ramCounter.NextValue(), 0, 100),
            DiskPercent = Math.Clamp(diskPercent, 0, 100),
            NetworkKbps = Math.Max(0, networkKbps),
            BatteryStatus = battery,
            Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
        };
    }

    public IReadOnlyList<ProcessSnapshot> GetTopProcesses(int count = 8)
    {
        return Process.GetProcesses()
            .Where(p => !string.IsNullOrWhiteSpace(p.ProcessName))
            .Select(p =>
            {
                try
                {
                    return new ProcessSnapshot { Id = p.Id, Name = p.ProcessName, WorkingSetBytes = p.WorkingSet64 };
                }
                catch
                {
                    return null;
                }
            })
            .Where(p => p != null)
            .OrderByDescending(p => p!.WorkingSetBytes)
            .Take(count)
            .Cast<ProcessSnapshot>()
            .ToList();
    }

    public void Dispose()
    {
        _cpuCounter.Dispose();
        _ramCounter.Dispose();
    }
}
