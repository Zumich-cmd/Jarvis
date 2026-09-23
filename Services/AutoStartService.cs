using Microsoft.Win32;

namespace Jarvis.Services;

public sealed class AutoStartService
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Jarvis";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(ValueName) != null;
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (key == null)
            return;

        if (enabled)
            key.SetValue(ValueName, Environment.ProcessPath ?? "");
        else
            key.DeleteValue(ValueName, false);
    }
}
