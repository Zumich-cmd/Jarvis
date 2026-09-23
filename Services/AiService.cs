using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Jarvis.Data;

namespace Jarvis.Services;

public sealed class AiService
{
    private static readonly HttpClient Http = new();
    private static readonly string[] FallbackGroqModels =
    [
        "openai/gpt-oss-20b",
        "openai/gpt-oss-120b",
        "qwen/qwen3.8-27b"
    ];

    private readonly SettingsService _settings;
    private readonly DatabaseService _database;

    public AiService(SettingsService settings, DatabaseService database)
    {
        _settings = settings;
        _database = database;
    }

    public async Task<string> AskAsync(string prompt)
    {
        var settings = _settings.Current;
        if (string.IsNullOrWhiteSpace(settings.GroqApiKey))
            return "Groq API ключ не налаштовано. Відкрийте налаштування та додайте ключ.";

        try
        {
            await _database.AddConversationMessageAsync("user", prompt);
            var recent = await _database.GetRecentMessagesAsync(10);
            var memory = await _database.SearchMemoryAsync(prompt);

            var messages = new List<object>
            {
                new
                {
                    role = "system",
                    content = BuildSystemPrompt(settings.Language, settings.ResponseStyle)
                }
            };

            if (memory.Count > 0)
            {
                messages.Add(new
                {
                    role = "system",
                    content = "Пам'ять JARVIS: " + string.Join("; ", memory)
                });
            }

            messages.AddRange(recent.Select(m => new { role = m.Role, content = m.Content }));

            var modelsToTry = new[] { settings.GroqModel }
                .Concat(FallbackGroqModels)
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (string model in modelsToTry)
            {
                var attempt = await AskGroqModelAsync(settings.GroqApiKey, model, messages);
                if (attempt.Success)
                {
                    await _database.AddConversationMessageAsync("assistant", attempt.Answer);
                    return attempt.Answer;
                }

                if (!string.Equals(attempt.StatusCode, "NotFound", StringComparison.OrdinalIgnoreCase))
                    return $"Помилка Groq API ({attempt.StatusCode}). {attempt.ErrorMessage}";
            }

            return "Не знайшов доступну модель Groq. Перевірте Groq model у налаштуваннях або список моделей у кабінеті Groq.";
        }
        catch (Exception ex)
        {
            return $"Не вдалося отримати AI-відповідь: {ex.Message}";
        }
    }

    private static string BuildSystemPrompt(string language, string responseStyle)
    {
        string languageInstruction = language switch
        {
            "ru-RU" => "Відповідай російською.",
            "en-US" => "Answer in English.",
            _ => "Відповідай українською."
        };

        string styleInstruction = responseStyle switch
        {
            "Detailed" => "Давай розгорнуті відповіді з корисними деталями.",
            "Balanced" => "Відповідай збалансовано: коротко, але без втрати важливих деталей.",
            _ => "Відповідай коротко і по суті."
        };

        return $"Ти голосовий асистент JARVIS. {languageInstruction} {styleInstruction} Враховуй контекст діалогу, пам'ять користувача і обрану форму звертання.";
    }

    private static async Task<GroqAttempt> AskGroqModelAsync(string apiKey, string model, List<object> messages)
    {
        var request = new
        {
            model,
            max_tokens = 1024,
            messages
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(httpRequest);
        string responseString = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return GroqAttempt.Fail(response.StatusCode.ToString(), ExtractGroqError(responseString));

        using var doc = JsonDocument.Parse(responseString);
        string answer = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        return GroqAttempt.Ok(answer);
    }

    private static string ExtractGroqError(string responseString)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseString);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
                return message.GetString() ?? "Спробуйте ще раз пізніше.";
        }
        catch
        {
            // Groq sometimes returns plain text or an empty body for upstream errors.
        }

        return "Спробуйте ще раз пізніше.";
    }

    private sealed record GroqAttempt(bool Success, string Answer, string StatusCode, string ErrorMessage)
    {
        public static GroqAttempt Ok(string answer) => new(true, answer, "", "");
        public static GroqAttempt Fail(string statusCode, string errorMessage) => new(false, "", statusCode, errorMessage);
    }
}
