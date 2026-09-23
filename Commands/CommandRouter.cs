using System.Globalization;
using System.IO;
using Jarvis.Data;
using Jarvis.Models;
using Jarvis.Services;

namespace Jarvis.Commands;

public sealed class CommandRouter
{
    private readonly AiService _ai;
    private readonly DatabaseService _database;
    private readonly WindowsAppService _apps;
    private readonly FileSearchService _files;
    private readonly ReminderService _reminders;
    private readonly SystemMonitorService _monitor;
    private readonly SettingsService _settings;
    private readonly DiagnosticsService _diagnostics;

    public CommandRouter(
        AiService ai,
        DatabaseService database,
        WindowsAppService apps,
        FileSearchService files,
        ReminderService reminders,
        SystemMonitorService monitor,
        SettingsService settings,
        DiagnosticsService diagnostics)
    {
        _ai = ai;
        _database = database;
        _apps = apps;
        _files = files;
        _reminders = reminders;
        _monitor = monitor;
        _settings = settings;
        _diagnostics = diagnostics;
    }

    public async Task<CommandResult> ExecuteAsync(string rawText, string source)
    {
        string command = StripWakeWord(rawText).Trim();
        if (string.IsNullOrWhiteSpace(command))
            return Result("VOICE", "Слухаю вас, сер.", "Активація", "Ключове слово розпізнано", "mic");

        string lower = NormalizeRecognition(command).ToLower(CultureInfo.GetCultureInfo("uk-UA"));
        CommandResult result;

        if (ContainsAny(lower, "запам'ятай", "запамятай", "запомни", "запомнить", "remember"))
            result = await RememberAsync(command);
        else if (ContainsAny(lower, "нагадай", "напомни", "напомнить", "remind"))
            result = await CreateReminderAsync(command);
        else if (ContainsAny(lower, "нагадування", "напоминания", "задачи", "завдання", "tasks", "reminders"))
            result = await ListRemindersAsync();
        else if (ContainsAny(lower, "статус сервісів", "статус сервисов", "диагностика", "діагностика", "diagnostics", "services"))
            result = ServiceStatus();
        else if (ContainsAny(lower, "стан системи", "состояние системы", "моніторинг", "мониторинг", "cpu", "ram", "пам'ять", "память", "battery", "батарея"))
            result = SystemStatus();
        else if (ContainsAny(lower, "процеси", "процес", "процессы", "нагружает", "навантажує", "process"))
            result = ProcessStatus();
        else if (ContainsAny(lower, "знайди файл", "знайти файл", "найди файл", "поиск файла", "пошук файлу", "file"))
            result = SearchFiles(command);
        else if (ContainsAny(lower, "закрий", "закрити", "закрой", "закрыть", "зупини", "останови", "close"))
            result = CloseApplication(command);
        else if (ContainsAny(lower, "відкр", "откр", "запуст", "зайди", "перейди", "open", "launch")
            || IsLikelyOpenTarget(lower))
            result = OpenApplication(command);
        else if (ContainsAny(lower, "час", "время", "година"))
            result = Result("SYSTEM", $"Зараз {DateTime.Now:HH:mm}.", "Поточний час", DateTime.Now.ToString("f", CultureInfo.GetCultureInfo("uk-UA")), "time");
        else
            result = await AskAiAsync(command);

        await _database.AddCommandHistoryAsync(source, command, result.Response);
        return result;
    }

    public string StripWakeWord(string text)
    {
        string wake = _settings.Current.WakeWord;
        string result = text.Replace("джарвіс", "", StringComparison.OrdinalIgnoreCase)
            .Replace("джарвис", "", StringComparison.OrdinalIgnoreCase)
            .Replace("jarvis", "", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(wake))
            result = result.Replace(wake, "", StringComparison.OrdinalIgnoreCase);

        return result;
    }

    public bool HasWakeWord(string text)
    {
        string wake = _settings.Current.WakeWord;
        return text.Contains("джарвіс", StringComparison.OrdinalIgnoreCase)
            || text.Contains("джарвис", StringComparison.OrdinalIgnoreCase)
            || text.Contains("jarvis", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(wake) && text.Contains(wake, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<CommandResult> RememberAsync(string command)
    {
        string memory = command
            .Replace("запам'ятай", "", StringComparison.OrdinalIgnoreCase)
            .Replace("запамятай", "", StringComparison.OrdinalIgnoreCase)
            .Replace("запомни", "", StringComparison.OrdinalIgnoreCase)
            .Replace("запомнить", "", StringComparison.OrdinalIgnoreCase)
            .Replace("remember", "", StringComparison.OrdinalIgnoreCase)
            .Trim(' ', ':', ',', '.');

        if (string.IsNullOrWhiteSpace(memory))
            return Result("COMMAND", "Скажіть, що саме потрібно запам'ятати.", "Пам'ять", "Порожня команда", "memory", "#FFB020");

        await _database.AddMemoryAsync("user", memory);
        return Result("COMMAND", $"Запам'ятав: {memory}", "Пам'ять оновлено", memory, "memory", "#00FF88");
    }

    private async Task<CommandResult> CreateReminderAsync(string command)
    {
        if (!TryParseReminder(command, out string text, out DateTime dueAt))
            return Result("COMMAND", "Не зрозумів час нагадування. Спробуйте: нагадай через 10 хвилин або нагадай завтра о 8.", "Нагадування", command, "alarm", "#FFB020");

        await _reminders.CreateAsync(text, dueAt);
        return Result("COMMAND", $"Добре, нагадаю {dueAt:g}: {text}", "Нагадування створено", text, "alarm", "#00FF88");
    }

    private async Task<CommandResult> ListRemindersAsync()
    {
        var reminders = await _reminders.GetActiveAsync();
        if (reminders.Count == 0)
            return Result("COMMAND", "Активних нагадувань немає.", "Нагадування", "Список порожній", "alarm");

        string response = string.Join(Environment.NewLine, reminders.Select(r => $"{r.DueAt:g} - {r.Text}"));
        return Result("COMMAND", response, "Активні нагадування", $"{reminders.Count} записів", "alarm");
    }

    private CommandResult SystemStatus()
    {
        var s = _monitor.GetSnapshot();
        string response = $"CPU {s.CpuPercent:0}%, RAM {s.RamPercent:0}%, Disk {s.DiskPercent:0}%, Network {s.NetworkKbps:0} KB/s, Battery {s.BatteryStatus}, Uptime {s.Uptime:dd\\.hh\\:mm}.";
        return Result("SYSTEM", response, "Стан системи", response, "system", "#00FF88");
    }

    private CommandResult ProcessStatus()
    {
        var processes = _monitor.GetTopProcesses(5);
        string response = "Найбільше RAM використовують: " + string.Join("; ", processes.Select(p => $"{p.Name} {p.MemoryDisplay}"));
        return Result("SYSTEM", response, "Процеси", "Топ навантаження RAM", "process", "#29FFE6");
    }

    private CommandResult ServiceStatus()
    {
        string response = _diagnostics.GetServiceStatus();
        return Result("SYSTEM", response, "Статус сервісів", "Діагностику виконано", "system", "#00FF88");
    }

    private CommandResult SearchFiles(string command)
    {
        string query = command
            .Replace("знайди файл", "", StringComparison.OrdinalIgnoreCase)
            .Replace("знайти файл", "", StringComparison.OrdinalIgnoreCase)
            .Replace("найди файл", "", StringComparison.OrdinalIgnoreCase)
            .Replace("поиск файла", "", StringComparison.OrdinalIgnoreCase)
            .Replace("пошук файлу", "", StringComparison.OrdinalIgnoreCase)
            .Replace("file", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        var files = _files.Search(query);
        if (files.Count == 0)
            return Result("COMMAND", $"Файли за запитом '{query}' не знайдено.", "Пошук файлів", query, "file", "#FFB020");

        string response = string.Join(Environment.NewLine, files.Select(Path.GetFileName));
        return Result("COMMAND", response, "Файли знайдено", $"{files.Count} результатів", "file");
    }

    private CommandResult OpenApplication(string command)
    {
        string app = CleanupObject(command, "відкрий", "відкрити", "відкрив", "відкрито", "відкривай", "открой", "открыть", "открыл", "открыто", "запусти", "запустить", "зайди", "перейди", "open", "launch");
        bool ok = _apps.TryOpen(app, out string message);
        return Result("COMMAND", message, ok ? "Застосунок відкрито" : "Помилка запуску", app, "app", ok ? "#00C8FF" : "#FF6B6B");
    }

    private CommandResult CloseApplication(string command)
    {
        string app = CleanupObject(command, "закрий", "закрити", "закрой", "закрыть", "зупини", "останови", "close");
        int closed = _apps.CloseByName(app);
        string response = closed > 0 ? $"Закрито процесів: {closed}." : $"Не знайшов запущений застосунок: {app}.";
        return Result("COMMAND", response, closed > 0 ? "Застосунок закрито" : "Процес не знайдено", app, "app", closed > 0 ? "#FF6B6B" : "#FFB020");
    }

    private async Task<CommandResult> AskAiAsync(string command)
    {
        string response = await _ai.AskAsync(command);
        return Result("AI", response, "AI відповідь", command, "ai", "#B28CFF");
    }

    private static bool TryParseReminder(string command, out string text, out DateTime dueAt)
    {
        string lower = command.ToLower(CultureInfo.GetCultureInfo("uk-UA"));
        text = command
            .Replace("нагадай", "", StringComparison.OrdinalIgnoreCase)
            .Replace("напомни", "", StringComparison.OrdinalIgnoreCase)
            .Replace("напомнить", "", StringComparison.OrdinalIgnoreCase)
            .Replace("remind me", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
        dueAt = DateTime.Now;

        var number = System.Text.RegularExpressions.Regex.Match(lower, @"через\s+(\d+)\s*(хв|мин|минут|минуты|minute|minutes)");
        if (number.Success)
        {
            dueAt = DateTime.Now.AddMinutes(int.Parse(number.Groups[1].Value));
            text = System.Text.RegularExpressions.Regex.Replace(text, @"через\s+\d+\s*(хв|мин|минут|минуты|minute|minutes)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            return !string.IsNullOrWhiteSpace(text);
        }

        var hour = System.Text.RegularExpressions.Regex.Match(lower, @"завтра\s+(о|в)?\s*(\d{1,2})(:(\d{2}))?");
        if (hour.Success)
        {
            int h = int.Parse(hour.Groups[2].Value);
            int m = hour.Groups[4].Success ? int.Parse(hour.Groups[4].Value) : 0;
            dueAt = DateTime.Today.AddDays(1).AddHours(h).AddMinutes(m);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"завтра\s+(о|в)?\s*\d{1,2}(:\d{2})?", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            return !string.IsNullOrWhiteSpace(text);
        }

        return false;
    }

    private static string CleanupObject(string command, params string[] words)
    {
        string result = command;
        foreach (var word in words)
            result = result.Replace(word, "", StringComparison.OrdinalIgnoreCase);
        return result.Trim(' ', ':', ',', '.');
    }

    private static bool ContainsAny(string value, params string[] words) => words.Any(w => value.Contains(w, StringComparison.OrdinalIgnoreCase));

    private static bool IsLikelyOpenTarget(string value)
    {
        return ContainsAny(value,
            "браузер", "browser", "ютуб", "ютюб", "youtube",
            "тікток", "тік ток", "тік-ток", "тик ток", "тик-ток", "тикток", "tiktok", "тук тук", "тук-тук", "тюк тюк", "тюк-тюк",
            "гугл", "google", "github", "гітхаб",
            "spotify", "спотіфай", "спотифай",
            "steam", "стім", "блокнот", "notepad", "калькулятор");
    }

    private static string NormalizeRecognition(string value)
    {
        return value
            .Replace("тік-ток", "тікток", StringComparison.OrdinalIgnoreCase)
            .Replace("тік ток", "тікток", StringComparison.OrdinalIgnoreCase)
            .Replace("тик-ток", "тикток", StringComparison.OrdinalIgnoreCase)
            .Replace("тик ток", "тикток", StringComparison.OrdinalIgnoreCase)
            .Replace("тук-тук", "тикток", StringComparison.OrdinalIgnoreCase)
            .Replace("тук тук", "тикток", StringComparison.OrdinalIgnoreCase)
            .Replace("тюк-тюк", "тикток", StringComparison.OrdinalIgnoreCase)
            .Replace("тюк тюк", "тикток", StringComparison.OrdinalIgnoreCase);
    }

    private static CommandResult Result(string category, string response, string title, string subtitle, string icon, string color = "#00C8FF")
    {
        return new CommandResult
        {
            Handled = true,
            Category = category,
            Response = response,
            Title = title,
            Icon = icon,
            Color = color,
            ShouldSpeak = category != "SYSTEM" || response.Length < 220
        };
    }
}
