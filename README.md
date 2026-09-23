# J.A.R.V.I.S Desktop Assistant

Український голосовий AI-асистент для Windows на WPF. J.A.R.V.I.S приймає голосові та текстові команди, відповідає через Groq AI, розпізнає мовлення через Groq Whisper або Vosk, озвучує відповіді через ElevenLabs / Piper / Windows TTS, відкриває застосунки та браузерні вкладки, веде історію, пам'ять і нагадування.

## Можливості

- Сучасний WPF-інтерфейс у стилі J.A.R.V.I.S.
- Голосове керування через Groq Whisper або offline Vosk.
- Команди можна говорити як зі словом `Jarvis`, так і без нього.
- Текстовий чат із тим самим роутером команд.
- Groq AI для звичайних питань, фактів і діалогу.
- ElevenLabs TTS як основний голос, Piper/Windows як fallback.
- Відкриття програм: Notepad, Calculator, браузер, Spotify, Steam та інші.
- Відкриття сайтів і вкладок: YouTube, TikTok, Google, GitHub, довільні URL.
- Пам'ять асистента через команду `запам'ятай`.
- Нагадування через голос або текст.
- SQLite-база для історії, команд, пам'яті, профілю користувача і нагадувань.
- Onboarding після першого запуску: ім'я користувача і форма звертання.
- Моніторинг CPU, RAM, диска, мережі, uptime і процесів.
- Окреме меню налаштувань: API, голос, розпізнавання, автозапуск, мікрофон.
- Журнал подій із фільтрами `SYSTEM`, `VOICE`, `AI`, `COMMAND`, `ERROR`.

## Технології

- C# / .NET 10
- WPF
- SQLite
- Groq Chat Completions
- Groq Whisper
- ElevenLabs Text-to-Speech
- Vosk speech recognition
- Piper offline TTS
- NAudio
- System.Speech

## Структура проекту

```text
Jarvis-main
|-- Api
|   |-- ApiConnectionHub.cs       # карта API-підключень для швидкого пояснення
|   `-- ApiConnectionInfo.cs
|-- Commands
|   `-- CommandRouter.cs          # роутинг голосових і текстових команд
|-- Data
|   `-- DatabaseService.cs        # SQLite: історія, пам'ять, нагадування, профіль
|-- Documentation
|   `-- PROJECT_MAP.md            # коротка карта проекту для захисту
|-- Models
|   |-- AppSettings.cs
|   |-- CommandResult.cs
|   |-- LogEntry.cs
|   |-- Reminder.cs
|   `-- UserProfile.cs
|-- Services
|   |-- AiService.cs              # Groq AI
|   |-- AutoStartService.cs
|   |-- DiagnosticsService.cs
|   |-- FileSearchService.cs
|   |-- ReminderService.cs
|   |-- SettingsService.cs
|   |-- SystemMonitorService.cs
|   `-- WindowsAppService.cs      # запуск програм і сайтів
|-- MainWindow.xaml               # головний інтерфейс
|-- MainWindow.xaml.cs
|-- SettingsWindow.xaml           # меню налаштувань
|-- OnboardingWindow.xaml         # перше налаштування користувача
|-- VoiceAssistant.cs             # мікрофон, STT, TTS
|-- SplashWindow.xaml
`-- Jarvis.csproj
```

## Архітектура

```text
MainWindow / SettingsWindow
        |
        v
CommandRouter
        |
        |-- AiService ---------- Groq AI
        |-- VoiceAssistant ----- Groq Whisper / Vosk / ElevenLabs / Piper
        |-- WindowsAppService -- програми, сайти, браузерні вкладки
        |-- DatabaseService ---- SQLite
        |-- ReminderService ---- нагадування
        |-- SystemMonitor ------ CPU / RAM / Disk / Network
        `-- FileSearchService -- пошук файлів
```

## Налаштування

Загальні параметри зберігаються в `appsettings.json`, а API-ключі зберігаються локально в SQLite-БД `jarvis.db`.

```json
{
  "GroqApiKey": "",
  "GroqModel": "openai/gpt-oss-20b",
  "ElevenLabsApiKey": "",
  "ElevenLabsVoiceId": "",
  "WakeWord": "jarvis",
  "Language": "uk-UA",
  "ResponseStyle": "Short",
  "SpeechRecognitionProvider": "GroqWhisper",
  "DirectVoiceCommands": true,
  "VoiceProvider": "ElevenLabs",
  "VoiceResponses": true,
  "MicrophoneGain": 2.8,
  "SaveHistory": true,
  "Notifications": true,
  "AutoStart": false,
  "Animations": true
}
```

Важливо:
- `GroqApiKey` і `ElevenLabsApiKey` залишені в JSON тільки для одноразової міграції старих конфігів; після запуску вони переносяться в SQLite і очищаються з файлу.
- `ResponseStyle` може бути `Short`, `Balanced` або `Detailed`.
- `SpeechRecognitionProvider` може бути `GroqWhisper` або `Vosk`.
- `DirectVoiceCommands: true` дозволяє говорити команди без слова `Jarvis`.
- `MicrophoneGain` керує програмним підсиленням мікрофона.

## Запуск

```powershell
dotnet restore
dotnet build .\Jarvis.csproj
dotnet run --project .\Jarvis.csproj
```

Якщо застосунок уже запущений і блокує файли збірки, можна перевірити білд в окрему папку:

```powershell
dotnet build .\Jarvis.csproj -p:UseAppHost=false -o .\voice-check
```

## Приклади команд

Голосом або в чаті:

```text
відкрий браузер
відкрий YouTube
відкрий TikTok
відкрий сайт github.com
відкрий вкладку з пошуком погода Київ
відкрий блокнот
відкрий калькулятор
стан системи
процеси
котра година
запам'ятай, що я люблю український голос Mykyta
нагадай через 10 хвилин перевірити проект
```

Також працює формат із wake word:

```text
Jarvis відкрий YouTube
Jarvis стан системи
```

## Демонстраційний сценарій

1. Запустити J.A.R.V.I.S і показати splash screen.
2. Пройти onboarding: ім'я користувача і форма звертання.
3. Відкрити меню налаштувань і показати Groq Whisper, Direct Voice Commands, ElevenLabs.
4. Натиснути `Тест голосу`.
5. Сказати `відкрий YouTube`.
6. Сказати `відкрий TikTok`.
7. Сказати `стан системи` і показати моніторинг.
8. Поставити нагадування.
9. Показати `Api/ApiConnectionHub.cs`, `Data/DatabaseService.cs`, `Commands/CommandRouter.cs`.

## Безпека ключів

Не публікуй реальні API-ключі в GitHub. Програма зберігає їх у локальній SQLite-БД і не показує поля введення ключів у вікні налаштувань.

## Примітки

- SQLite база створюється локально для історії, пам'яті, нагадувань і API-секретів.
- Groq Whisper працює через інтернет, але дає кращу якість розпізнавання, ніж Vosk.
- Vosk залишений як offline fallback.
- ElevenLabs потребує коректний Secret Key і Voice ID.
- Закриття процесів підтверджується окремим діалогом, щоб випадково не завершити потрібну програму.
