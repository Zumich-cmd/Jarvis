using System.Windows.Threading;
using Jarvis.Data;
using Jarvis.Models;

namespace Jarvis.Services;

public sealed class ReminderService
{
    private readonly DatabaseService _database;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(15) };

    public event Func<Reminder, Task>? ReminderDue;

    public ReminderService(DatabaseService database)
    {
        _database = database;
        _timer.Tick += async (_, _) => await CheckDueRemindersAsync();
    }

    public void Start() => _timer.Start();

    public async Task<long> CreateAsync(string text, DateTime dueAt)
    {
        long id = await _database.AddReminderAsync(text, dueAt);
        await CheckDueRemindersAsync();
        return id;
    }

    public Task<IReadOnlyList<Reminder>> GetActiveAsync() => _database.GetActiveRemindersAsync();

    private async Task CheckDueRemindersAsync()
    {
        var due = (await _database.GetActiveRemindersAsync()).Where(r => r.DueAt <= DateTime.Now).ToList();
        foreach (var reminder in due)
        {
            await _database.CompleteReminderAsync(reminder.Id);
            if (ReminderDue != null)
                await ReminderDue.Invoke(reminder);
        }
    }
}
