using System.IO;
using Jarvis.Models;
using Jarvis.Data;
using Jarvis.Services;

namespace Jarvis.Api;

/// <summary>
/// Центральная точка API-подключений J.A.R.V.I.S.
///
/// Навигация для защиты проекта:
/// - Groq AI chat: Services/AiService.cs, метод AskAsync.
/// - ElevenLabs TTS: VoiceAssistant.cs, метод TrySpeakWithElevenLabsAsync.
/// - Piper offline TTS: VoiceAssistant.cs, метод TrySpeakWithPiperAsync.
/// - Vosk speech-to-text: VoiceAssistant.cs, метод InitSpeech.
/// - API-ключи: SQLite ApiSecrets через Data/DatabaseService.cs.
/// - Общие настройки: Services/SettingsService.cs + appsettings.json.
///
/// Этот класс не дублирует бизнес-логику, а собирает все API-сервисы в одном месте,
/// чтобы на демонстрации быстро показать, какие внешние и локальные API использует проект.
/// </summary>
public sealed class ApiConnectionHub
{
    public AiService Ai { get; }
    public VoiceAssistant Voice { get; }
    public SettingsService Settings { get; }

    public ApiConnectionHub(AiService ai, VoiceAssistant voice, SettingsService settings)
    {
        Ai = ai;
        Voice = voice;
        Settings = settings;
    }

    public IReadOnlyList<ApiConnectionInfo> GetConnections()
    {
        AppSettings settings = Settings.Current;
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;

        return new List<ApiConnectionInfo>
        {
            new(
                "Groq AI",
                "Онлайн AI-відповіді та контекст діалогу",
                "Services/AiService.cs",
                "AskAsync",
                string.IsNullOrWhiteSpace(settings.GroqApiKey) ? "NOT CONFIGURED" : "CONFIGURED"),
            new(
                "ElevenLabs",
                "Основний онлайн голос J.A.R.V.I.S",
                "VoiceAssistant.cs",
                "TrySpeakWithElevenLabsAsync",
                string.IsNullOrWhiteSpace(settings.ElevenLabsApiKey) ? "NOT CONFIGURED" : "CONFIGURED"),
            new(
                "Piper",
                "Offline fallback голосу",
                "VoiceAssistant.cs",
                "TrySpeakWithPiperAsync",
                File.Exists(Path.Combine(baseDir, "piper", "piper.exe")) ? "READY" : "MISSING"),
            new(
                "Vosk",
                "Offline розпізнавання голосу",
                "VoiceAssistant.cs",
                "InitSpeech",
                Directory.Exists(Path.Combine(baseDir, "model")) ? "READY" : "MODEL MISSING"),
            new(
                "SQLite",
                "Історія діалогу, команд, пам'ять і нагадування",
                "Data/DatabaseService.cs",
                "InitializeAsync",
                "LOCAL")
        };
    }
}
