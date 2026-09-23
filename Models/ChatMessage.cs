namespace Jarvis.Models;

public sealed class ChatMessage
{
    public long Id { get; set; }
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
