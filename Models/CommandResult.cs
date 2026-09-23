namespace Jarvis.Models;

public sealed class CommandResult
{
    public bool Handled { get; init; }
    public string Response { get; init; } = "";
    public string Category { get; init; } = "COMMAND";
    public string Title { get; init; } = "Команда";
    public string Icon { get; init; } = ">";
    public string Color { get; init; } = "#00C8FF";
    public bool ShouldSpeak { get; init; } = true;
}
