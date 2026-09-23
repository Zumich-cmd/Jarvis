using System.IO;
using System.Net.NetworkInformation;

namespace Jarvis.Services;

public sealed class DiagnosticsService
{
    private readonly SettingsService _settings;

    public DiagnosticsService(SettingsService settings)
    {
        _settings = settings;
    }

    public string GetServiceStatus()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        bool piperReady = File.Exists(Path.Combine(baseDir, "piper", "piper.exe"))
            && File.Exists(Path.Combine(baseDir, "piper", "uk_UA-mykyta-high.onnx"));
        bool voskReady = Directory.Exists(Path.Combine(baseDir, "model"))
            && File.Exists(Path.Combine(baseDir, "model", "am", "final.mdl"));
        bool internet = NetworkInterface.GetIsNetworkAvailable();
        bool elevenLabsConfigured = !string.IsNullOrWhiteSpace(_settings.Current.ElevenLabsApiKey)
            && _settings.Current.ElevenLabsApiKey.StartsWith("sk_", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(_settings.Current.ElevenLabsVoiceId);

        return string.Join(Environment.NewLine, new[]
        {
            $"VOICE        {(voskReady ? "ONLINE" : "MODEL MISSING")}",
            $"GROQ         {(internet && !string.IsNullOrWhiteSpace(_settings.Current.GroqApiKey) ? "ONLINE" : "NOT CONFIGURED")}",
            $"ELEVENLABS   {(internet && elevenLabsConfigured ? "CONFIGURED" : "CHECK KEY")}",
            $"PIPER        {(piperReady ? "READY" : "MISSING")}",
            "DATABASE     ONLINE",
            $"MICROPHONE   {(NAudio.Wave.WaveInEvent.DeviceCount > 0 ? "ACTIVE" : "NOT FOUND")}",
            $"INTERNET     {(internet ? "ONLINE" : "OFFLINE")}"
        });
    }
}
