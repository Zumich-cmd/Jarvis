namespace Jarvis.Models;

public sealed class ProcessSnapshot
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public long WorkingSetBytes { get; init; }
    public string MemoryDisplay => $"{WorkingSetBytes / 1024d / 1024d:0} MB";
}
