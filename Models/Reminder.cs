namespace Jarvis.Models;

public sealed class Reminder
{
    public long Id { get; set; }
    public string Text { get; set; } = "";
    public DateTime DueAt { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
