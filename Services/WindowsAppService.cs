using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Jarvis.Services;

public sealed class WindowsAppService
{
    private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["блокнот"] = "notepad.exe",
        ["notepad"] = "notepad.exe",
        ["калькулятор"] = "calc.exe",
        ["calculator"] = "calc.exe",
        ["calc"] = "calc.exe",
        ["браузер"] = "https://www.google.com",
        ["browser"] = "https://www.google.com",
        ["youtube"] = "https://www.youtube.com",
        ["ютуб"] = "https://www.youtube.com",
        ["ютюб"] = "https://www.youtube.com",
        ["tiktok"] = "https://www.tiktok.com",
        ["тікток"] = "https://www.tiktok.com",
        ["тік ток"] = "https://www.tiktok.com",
        ["тик ток"] = "https://www.tiktok.com",
        ["тикток"] = "https://www.tiktok.com",
        ["тік-ток"] = "https://www.tiktok.com",
        ["тик-ток"] = "https://www.tiktok.com",
        ["куток"] = "https://www.tiktok.com",
        ["тук тук"] = "https://www.tiktok.com",
        ["тук-тук"] = "https://www.tiktok.com",
        ["тюк тюк"] = "https://www.tiktok.com",
        ["тюк-тюк"] = "https://www.tiktok.com",
        ["тіктоку"] = "https://www.tiktok.com",
        ["тиктоку"] = "https://www.tiktok.com",
        ["google"] = "https://www.google.com",
        ["гугл"] = "https://www.google.com",
        ["github"] = "https://github.com",
        ["гітхаб"] = "https://github.com",
        ["chrome"] = "chrome",
        ["хром"] = "chrome",
        ["telegram"] = "telegram",
        ["телеграм"] = "telegram",
        ["discord"] = "discord",
        ["дискорд"] = "discord",
        ["діскорд"] = "discord",
        ["visual studio"] = "devenv",
        ["studio"] = "devenv",
        ["spotify"] = "spotify:",
        ["спотіфай"] = "spotify:",
        ["спотифай"] = "spotify:",
        ["steam"] = "steam://open/games",
        ["стім"] = "steam://open/games"
    };

    public bool TryOpen(string requestedName, out string message)
    {
        string name = Normalize(requestedName);
        if (TryResolveWebTarget(requestedName, out string webTarget))
        {
            return TryStart(webTarget, $"Відкриваю вкладку: {webTarget}", out message);
        }

        string? alias = _aliases.FirstOrDefault(a => name.Contains(a.Key, StringComparison.OrdinalIgnoreCase)).Value;
        string target = string.IsNullOrWhiteSpace(alias) ? requestedName : alias;

        try
        {
            if (target.Contains("://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase))
                return TryStart(target, $"Відкриваю {requestedName}.", out message);

            var existing = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(target)).FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
            if (existing != null)
            {
                SetForegroundWindow(existing.MainWindowHandle);
                message = $"Відкриваю вже запущений застосунок: {existing.ProcessName}.";
                return true;
            }

            return TryStart(target, $"Відкриваю {requestedName}.", out message);
        }
        catch (Exception ex)
        {
            message = $"Не вдалося відкрити {requestedName}: {ex.Message}";
            return false;
        }
    }

    private static bool TryStart(string target, string successMessage, out string message)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            message = successMessage;
            return true;
        }
        catch (Exception ex)
        {
            message = $"Не вдалося відкрити {target}: {ex.Message}";
            return false;
        }
    }

    private bool TryResolveWebTarget(string requestedName, out string url)
    {
        string text = NormalizeRecognition(requestedName.Trim());
        string lower = text.ToLowerInvariant();
        url = "";

        foreach (var alias in _aliases)
        {
            if ((alias.Value.StartsWith("http", StringComparison.OrdinalIgnoreCase) || alias.Value.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase))
                && lower.Contains(alias.Key.ToLowerInvariant()))
            {
                url = alias.Value;
                return true;
            }
        }

        string cleaned = CleanupWebQuery(text);
        if (Regex.IsMatch(cleaned, @"^[a-z0-9.-]+\.[a-z]{2,}(/.*)?$", RegexOptions.IgnoreCase))
        {
            url = cleaned.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? cleaned : $"https://{cleaned}";
            return true;
        }

        if (ContainsAny(lower, "пошук", "поиск", "знайди в браузері", "найди в браузере", "гугл", "google"))
        {
            string query = CleanupSearchQuery(text);
            if (!string.IsNullOrWhiteSpace(query))
            {
                url = "https://www.google.com/search?q=" + Uri.EscapeDataString(query);
                return true;
            }
        }

        return false;
    }

    private static string CleanupWebQuery(string text)
    {
        string[] words =
        {
            "відкрий", "відкрити", "відкрив", "відкрито", "відкривай", "открой", "открыть", "открыл", "открыто", "запусти", "запустить", "зайди", "перейди",
            "браузер", "browser", "вкладку", "вкладка", "сайт", "сторінку", "страницу",
            "на", "з", "с", "в", "у"
        };

        string result = text;
        foreach (string word in words)
            result = Regex.Replace(result, $@"\b{Regex.Escape(word)}\b", "", RegexOptions.IgnoreCase);
        return result.Trim(' ', '.', ',', ':', ';');
    }

    private static string CleanupSearchQuery(string text)
    {
        string result = text;
        string[] words =
        {
            "відкрий", "відкрити", "відкрив", "відкрито", "відкривай", "открой", "открыть", "открыл", "открыто", "вкладку", "браузер",
            "пошук", "поиск", "знайди", "найди", "в браузері", "в браузере",
            "google", "гугл"
        };

        foreach (string word in words)
            result = result.Replace(word, "", StringComparison.OrdinalIgnoreCase);
        return result.Trim(' ', '.', ',', ':', ';');
    }

    public int CloseByName(string requestedName)
    {
        string name = Normalize(requestedName);
        var candidates = _aliases
            .Where(a => name.Contains(a.Key, StringComparison.OrdinalIgnoreCase))
            .Select(a => Path.GetFileNameWithoutExtension(a.Value))
            .Append(requestedName)
            .Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        int closed = 0;
        foreach (var candidate in candidates)
        {
            foreach (var process in Process.GetProcessesByName(candidate))
            {
                try
                {
                    process.Kill();
                    closed++;
                }
                catch
                {
                }
            }
        }

        return closed;
    }

    private static string Normalize(string value) => NormalizeRecognition(value).Trim().Replace(".exe", "", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeRecognition(string value)
    {
        return value
            .Replace("тик ток", "тикток", StringComparison.OrdinalIgnoreCase)
            .Replace("тік ток", "тікток", StringComparison.OrdinalIgnoreCase)
            .Replace("тік-ток", "тікток", StringComparison.OrdinalIgnoreCase)
            .Replace("тик-ток", "тикток", StringComparison.OrdinalIgnoreCase)
            .Replace("тук тук", "тикток", StringComparison.OrdinalIgnoreCase)
            .Replace("тук-тук", "тикток", StringComparison.OrdinalIgnoreCase)
            .Replace("тюк тюк", "тикток", StringComparison.OrdinalIgnoreCase)
            .Replace("тюк-тюк", "тикток", StringComparison.OrdinalIgnoreCase)
            .Replace("ютюб", "ютуб", StringComparison.OrdinalIgnoreCase)
            .Replace("браузері", "браузер", StringComparison.OrdinalIgnoreCase)
            .Replace("браузере", "браузер", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string value, params string[] words) => words.Any(w => value.Contains(w, StringComparison.OrdinalIgnoreCase));

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
