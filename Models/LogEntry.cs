namespace Jarvis.Models;

public sealed class LogEntry
{
    public string Icon { get; init; } = ">";
    public string Category { get; init; } = "SYSTEM";
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string IconColor { get; init; } = "#00C8FF";
    public string Time { get; init; } = DateTime.Now.ToString("HH:mm:ss");
}
