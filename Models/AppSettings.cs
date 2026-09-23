namespace Jarvis.Models;

public sealed class AppSettings
{
    public string GroqApiKey { get; set; } = "";
    public string GroqModel { get; set; } = "openai/gpt-oss-20b";
    public string ElevenLabsApiKey { get; set; } = "";
    public string ElevenLabsVoiceId { get; set; } = "";
    public string WakeWord { get; set; } = "jarvis";
    public string Language { get; set; } = "uk-UA";
    public string ResponseStyle { get; set; } = "Short";
    public string SpeechRecognitionProvider { get; set; } = "GroqWhisper";
    public bool DirectVoiceCommands { get; set; } = true;
    public string VoiceProvider { get; set; } = "ElevenLabs";
    public bool VoiceResponses { get; set; } = true;
    public float MicrophoneGain { get; set; } = 2.8f;
    public bool SaveHistory { get; set; } = true;
    public bool Notifications { get; set; } = true;
    public bool AutoStart { get; set; }
    public bool Animations { get; set; } = true;
}
