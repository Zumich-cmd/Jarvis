using System.Collections.ObjectModel;
using Jarvis.Models;

namespace Jarvis.ViewModels;

public sealed class MainViewModel
{
    public ObservableCollection<LogEntry> Logs { get; } = new();
    public ObservableCollection<ProcessSnapshot> Processes { get; } = new();
}
