using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Speech.Synthesis;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Jarvis.Services;
using Vosk;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Jarvis
{
    public class VoiceAssistant
    {
        private static readonly HttpClient Http = new HttpClient();
        private readonly List<MediaPlayer> _activePlayers = new List<MediaPlayer>();
        private readonly SettingsService _settings;
        private readonly SpeechSynthesizer _windowsVoice = new SpeechSynthesizer();
        private readonly object _groqAudioLock = new object();
        private MemoryStream _groqAudioBuffer = new MemoryStream();
        private bool _isListening = false;
        private bool _groqTranscribing;
        private DateTime _lastAudibleAt = DateTime.MinValue;
        private VoskRecognizer? _voskRecognizer;
        private WaveInEvent? _waveIn;
        public bool IsListening => _isListening;
        public event Action<string, string, string>? VoiceStatus;

        public VoiceAssistant(SettingsService settings)
        {
            _settings = settings;
        }

        public async Task SpeakAsync(string text)
        {
            if (!_settings.Current.VoiceResponses || string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                if (await TrySpeakWithElevenLabsAsync(text))
                    return;
            }
            catch
            {
            }

            if (await TrySpeakWithPiperAsync(text))
                return;

            await SpeakWithWindowsAsync(text);
        }

        private async Task<bool> TrySpeakWithElevenLabsAsync(string text)
        {
            string apiKey = _settings.Current.ElevenLabsApiKey;
            string voiceId = _settings.Current.ElevenLabsVoiceId;
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(voiceId))
                return false;

            if (!apiKey.StartsWith("sk_", StringComparison.OrdinalIgnoreCase))
            {
                VoiceStatus?.Invoke("ERROR", "ElevenLabs ключ некоректний", "У локальній SQLite-БД потрібен Secret Key, який починається з sk_");
                return false;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}");
            request.Headers.Add("xi-api-key", apiKey);
            request.Headers.Add("User-Agent", "JarvisApp/1.0");
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("audio/mpeg"));
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                text,
                model_id = "eleven_multilingual_v2",
                voice_settings = new
                {
                    stability = 0.5,
                    similarity_boost = 0.75
                }
            }), Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                VoiceStatus?.Invoke("ERROR", "ElevenLabs не озвучив текст", GetElevenLabsError(error, response.StatusCode.ToString()));
                return false;
            }

            byte[] audioBytes = await response.Content.ReadAsByteArrayAsync();
            string tempFile = Path.Combine(Path.GetTempPath(), $"jarvis_{Guid.NewGuid()}.mp3");
            await File.WriteAllBytesAsync(tempFile, audioBytes);
            PlayAudio(tempFile);
            return true;
        }

        private static string GetElevenLabsError(string responseBody, string fallback)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("detail", out var detail))
                {
                    if (detail.ValueKind == JsonValueKind.Object &&
                        detail.TryGetProperty("message", out var message))
                        return message.GetString() ?? fallback;

                    if (detail.ValueKind == JsonValueKind.String)
                        return detail.GetString() ?? fallback;
                }
            }
            catch
            {
            }

            return fallback;
        }

        private async Task<bool> TrySpeakWithPiperAsync(string text)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string piperExe = Path.Combine(baseDir, "piper", "piper.exe");
            string model = Path.Combine(baseDir, "piper", "uk_UA-mykyta-high.onnx");
            if (!File.Exists(piperExe) || !File.Exists(model))
                return false;

            string tempFile = Path.Combine(Path.GetTempPath(), $"jarvis_{Guid.NewGuid()}.wav");
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = piperExe,
                    Arguments = $"-m \"{model}\" -f \"{tempFile}\"",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                process.Start();
                await process.StandardInput.WriteLineAsync(text);
                process.StandardInput.Close();
                await process.WaitForExitAsync();

                if (File.Exists(tempFile))
                {
                    PlayAudio(tempFile);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private void PlayAudio(string tempFile)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MediaPlayer player = new MediaPlayer();
                player.MediaEnded += (s, e) =>
                {
                    _activePlayers.Remove(player);
                    try { File.Delete(tempFile); } catch { }
                };
                _activePlayers.Add(player);
                player.Open(new Uri(tempFile));
                player.Volume = 1.0;
                player.Play();
            });
        }

        private Task SpeakWithWindowsAsync(string text)
        {
            return Task.Run(() =>
            {
                try
                {
                    _windowsVoice.Volume = 100;
                    _windowsVoice.Rate = 0;
                    _windowsVoice.Speak(text);
                }
                catch
                {
                }
            });
        }

        public async Task GreetUserAsync(Models.UserProfile? profile = null)
        {
            int hour = DateTime.Now.Hour;
            string greeting;
            string address = profile?.GreetingName ?? "сер";

            if (hour >= 5 && hour < 12)
                greeting = $"Доброго ранку, {address}. Система готова до роботи.";
            else if (hour >= 12 && hour < 18)
                greeting = $"Добрий день, {address}. Усі системи працюють штатно.";
            else
                greeting = $"Добрий вечір, {address}. Радий вас бачити.";

            await SpeakAsync(greeting);
        }

        public void InitSpeech(Action<string> onCommandRecognized)
        {
            try
            {
                if (string.Equals(_settings.Current.SpeechRecognitionProvider, "GroqWhisper", StringComparison.OrdinalIgnoreCase))
                {
                    InitGroqWhisper(onCommandRecognized);
                    return;
                }

                string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "model");

                if (!Directory.Exists(modelPath))
                {
                    MessageBox.Show($"Папку моделі не знайдено: {modelPath}", "Помилка");
                    return;
                }

                Vosk.Vosk.SetLogLevel(-1);
                var model = new Model(modelPath);
                _voskRecognizer = new VoskRecognizer(model, 16000.0f);

                _waveIn = new WaveInEvent();
                _waveIn.WaveFormat = new WaveFormat(16000, 1);
                _waveIn.BufferMilliseconds = 120;
                _waveIn.NumberOfBuffers = 3;

                _waveIn.DataAvailable += (s, e) =>
                {
                    byte[] audio = ApplyMicrophoneGain(e.Buffer, e.BytesRecorded, _settings.Current.MicrophoneGain);
                    if (_voskRecognizer.AcceptWaveform(audio, audio.Length))
                    {
                        var result = _voskRecognizer.Result();
                        var text = System.Text.Json.JsonDocument.Parse(result)
                                   .RootElement.GetProperty("text").GetString()?.ToLower() ?? "";

                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            onCommandRecognized(text);
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка ініціалізації мікрофона: {ex.Message}", "Помилка");
            }
        }

        private void InitGroqWhisper(Action<string> onCommandRecognized)
        {
            if (string.IsNullOrWhiteSpace(_settings.Current.GroqApiKey))
            {
                VoiceStatus?.Invoke("ERROR", "Groq Whisper не налаштовано", "Для хмарного розпізнавання потрібен GroqApiKey");
                InitVoskFallback(onCommandRecognized);
                return;
            }

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 120,
                NumberOfBuffers = 3
            };

            _waveIn.DataAvailable += (s, e) =>
            {
                byte[] audio = ApplyMicrophoneGain(e.Buffer, e.BytesRecorded, _settings.Current.MicrophoneGain);
                if (!HasAudibleSignal(audio))
                {
                    byte[]? silenceChunk = TryTakeGroqChunk(forceOnPause: true);
                    if (silenceChunk != null)
                        _ = TranscribeGroqChunkAsync(silenceChunk, onCommandRecognized);
                    return;
                }

                _lastAudibleAt = DateTime.Now;

                byte[]? chunk = null;
                lock (_groqAudioLock)
                {
                    _groqAudioBuffer.Write(audio, 0, audio.Length);
                    int targetBytes = _waveIn.WaveFormat.AverageBytesPerSecond * 3;
                    if (_groqAudioBuffer.Length >= targetBytes && !_groqTranscribing)
                    {
                        chunk = TakeGroqBufferUnsafe();
                    }
                }

                if (chunk != null)
                    _ = TranscribeGroqChunkAsync(chunk, onCommandRecognized);
            };

            VoiceStatus?.Invoke("VOICE", "Groq Whisper активний", "Розпізнавання мовлення працює через Groq");
        }

        private byte[]? TryTakeGroqChunk(bool forceOnPause)
        {
            lock (_groqAudioLock)
            {
                if (_groqTranscribing || _groqAudioBuffer.Length == 0)
                    return null;

                int minBytes = 16000 * 2 / 2;
                bool hasEnoughSpeech = _groqAudioBuffer.Length >= minBytes;
                bool pauseDetected = DateTime.Now - _lastAudibleAt > TimeSpan.FromMilliseconds(750);
                if (!hasEnoughSpeech || (forceOnPause && !pauseDetected))
                    return null;

                return TakeGroqBufferUnsafe();
            }
        }

        private byte[] TakeGroqBufferUnsafe()
        {
            byte[] chunk = _groqAudioBuffer.ToArray();
            _groqAudioBuffer.Dispose();
            _groqAudioBuffer = new MemoryStream();
            _groqTranscribing = true;
            return chunk;
        }

        private void InitVoskFallback(Action<string> onCommandRecognized)
        {
            string originalProvider = _settings.Current.SpeechRecognitionProvider;
            _settings.Current.SpeechRecognitionProvider = "Vosk";
            InitSpeech(onCommandRecognized);
            _settings.Current.SpeechRecognitionProvider = originalProvider;
        }

        private async Task TranscribeGroqChunkAsync(byte[] pcmBytes, Action<string> onCommandRecognized)
        {
            try
            {
                byte[] wavBytes = BuildWavBytes(pcmBytes);
                using var form = new MultipartFormDataContent();
                using var file = new ByteArrayContent(wavBytes);
                file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                form.Add(file, "file", "speech.wav");
                form.Add(new StringContent("whisper-large-v3-turbo"), "model");
                form.Add(new StringContent("uk"), "language");
                form.Add(new StringContent("json"), "response_format");

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.Current.GroqApiKey);
                request.Content = form;

                using HttpResponseMessage response = await Http.SendAsync(request);
                string responseString = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    VoiceStatus?.Invoke("ERROR", "Groq Whisper помилка", response.StatusCode.ToString());
                    return;
                }

                using var doc = JsonDocument.Parse(responseString);
                string text = doc.RootElement.GetProperty("text").GetString()?.Trim().ToLowerInvariant() ?? "";
                if (!string.IsNullOrWhiteSpace(text))
                    onCommandRecognized(text);
            }
            catch (Exception ex)
            {
                VoiceStatus?.Invoke("ERROR", "Groq Whisper помилка", ex.Message);
            }
            finally
            {
                lock (_groqAudioLock)
                    _groqTranscribing = false;
            }
        }

        private static byte[] BuildWavBytes(byte[] pcmBytes)
        {
            using var stream = new MemoryStream();
            using (var writer = new WaveFileWriter(stream, new WaveFormat(16000, 16, 1)))
            {
                writer.Write(pcmBytes, 0, pcmBytes.Length);
            }

            return stream.ToArray();
        }

        private static bool HasAudibleSignal(byte[] audio)
        {
            int peak = 0;
            for (int i = 0; i + 1 < audio.Length; i += 2)
            {
                int rawSample = BitConverter.ToInt16(audio, i);
                int sample = rawSample == short.MinValue ? short.MaxValue : Math.Abs(rawSample);
                if (sample > peak)
                    peak = sample;
            }

            return peak > 550;
        }

        private static byte[] ApplyMicrophoneGain(byte[] buffer, int bytesRecorded, float gain)
        {
            if (gain <= 1.01f)
                return buffer.Take(bytesRecorded).ToArray();

            byte[] amplified = new byte[bytesRecorded];
            for (int i = 0; i + 1 < bytesRecorded; i += 2)
            {
                short sample = BitConverter.ToInt16(buffer, i);
                int boosted = (int)(sample * gain);
                boosted = Math.Clamp(boosted, short.MinValue, short.MaxValue);
                amplified[i] = (byte)(boosted & 0xFF);
                amplified[i + 1] = (byte)((boosted >> 8) & 0xFF);
            }

            return amplified;
        }

        public void StartListening()
        {
            if (_waveIn != null && !_isListening)
            {
                _waveIn.StartRecording();
                _isListening = true;
            }
        }

        public void StopListening()
        {
            if (_waveIn != null && _isListening)
            {
                _waveIn.StopRecording();
                _isListening = false;
            }
        }
    }
}
