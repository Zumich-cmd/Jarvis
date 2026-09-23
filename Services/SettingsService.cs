using System.IO;
using System.Text.Json;
using Jarvis.Data;
using Jarvis.Models;

namespace Jarvis.Services;

public sealed class SettingsService
{
    private readonly string _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const string GroqSecretName = "GroqApiKey";
    private const string ElevenLabsSecretName = "ElevenLabsApiKey";

    public AppSettings Current { get; private set; } = new();

    public AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            Save(Current);
            return Current;
        }

        try
        {
            Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
        }
        catch
        {
            Current = new AppSettings();
        }

        return Current;
    }

    public async Task LoadSecretsFromDatabaseAsync(DatabaseService database)
    {
        string legacyGroqKey = Current.GroqApiKey;
        string legacyElevenLabsKey = Current.ElevenLabsApiKey;

        string dbGroqKey = await database.GetApiSecretAsync(GroqSecretName);
        string dbElevenLabsKey = await database.GetApiSecretAsync(ElevenLabsSecretName);

        if (string.IsNullOrWhiteSpace(dbGroqKey) && !string.IsNullOrWhiteSpace(legacyGroqKey))
        {
            await database.SaveApiSecretAsync(GroqSecretName, legacyGroqKey);
            dbGroqKey = legacyGroqKey;
        }

        if (string.IsNullOrWhiteSpace(dbElevenLabsKey) && !string.IsNullOrWhiteSpace(legacyElevenLabsKey))
        {
            await database.SaveApiSecretAsync(ElevenLabsSecretName, legacyElevenLabsKey);
            dbElevenLabsKey = legacyElevenLabsKey;
        }

        Current.GroqApiKey = dbGroqKey;
        Current.ElevenLabsApiKey = dbElevenLabsKey;

        if (!string.IsNullOrWhiteSpace(legacyGroqKey) || !string.IsNullOrWhiteSpace(legacyElevenLabsKey))
            Save(Current);
    }

    public void Save(AppSettings settings)
    {
        Current = settings;
        var fileSettings = new AppSettings
        {
            GroqModel = settings.GroqModel,
            ElevenLabsVoiceId = settings.ElevenLabsVoiceId,
            WakeWord = settings.WakeWord,
            Language = settings.Language,
            ResponseStyle = settings.ResponseStyle,
            SpeechRecognitionProvider = settings.SpeechRecognitionProvider,
            DirectVoiceCommands = settings.DirectVoiceCommands,
            VoiceProvider = settings.VoiceProvider,
            VoiceResponses = settings.VoiceResponses,
            MicrophoneGain = settings.MicrophoneGain,
            SaveHistory = settings.SaveHistory,
            Notifications = settings.Notifications,
            AutoStart = settings.AutoStart,
            Animations = settings.Animations
        };

        File.WriteAllText(_path, JsonSerializer.Serialize(fileSettings, JsonOptions));
    }
}
