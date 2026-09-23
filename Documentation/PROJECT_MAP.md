# J.A.R.V.I.S Project Map

Цей файл потрібен для швидкого пояснення коду на захисті.

## API підключення

| Функціонал | Де лежить | Точка входу |
| --- | --- | --- |
| Єдина карта API | `Api/ApiConnectionHub.cs` | `GetConnections()` |
| Groq AI | `Services/AiService.cs` | `AskAsync()` |
| ElevenLabs voice | `VoiceAssistant.cs` | `TrySpeakWithElevenLabsAsync()` |
| Piper offline voice | `VoiceAssistant.cs` | `TrySpeakWithPiperAsync()` |
| Vosk speech-to-text | `VoiceAssistant.cs` | `InitSpeech()` |
| Міграція API ключів із JSON у SQLite | `Services/SettingsService.cs` | `LoadSecretsFromDatabaseAsync()` |

## База даних

| Функціонал | Де лежить | Точка входу |
| --- | --- | --- |
| SQLite schema та підключення | `Data/DatabaseService.cs` | `InitializeAsync()` |
| Історія діалогу | `Data/DatabaseService.cs` | `AddConversationMessageAsync()` |
| Історія команд | `Data/DatabaseService.cs` | `AddCommandHistoryAsync()` |
| Пам'ять J.A.R.V.I.S | `Data/DatabaseService.cs` | `AddMemoryAsync()` / `SearchMemoryAsync()` |
| Нагадування | `Data/DatabaseService.cs` | `AddReminderAsync()` / `GetActiveRemindersAsync()` |
| Профіль користувача | `Data/DatabaseService.cs` | `SaveUserProfileAsync()` / `GetUserProfileAsync()` |
| API секрети | `Data/DatabaseService.cs` | `GetApiSecretAsync()` / `SaveApiSecretAsync()` |

## Команди та UI

| Функціонал | Де лежить | Точка входу |
| --- | --- | --- |
| Роутинг текстових і голосових команд | `Commands/CommandRouter.cs` | `ExecuteAsync()` |
| Головний екран | `MainWindow.xaml` | XAML layout |
| Обробка UI подій | `MainWindow.xaml.cs` | `ProcessCommandAsync()` |
| Intro / boot screen | `SplashWindow.xaml.cs` | `RunBootSequenceAsync()` |
| Onboarding після intro | `OnboardingWindow.xaml.cs` | `Save_Click()` |

## Сценарій демонстрації

1. Запустити J.A.R.V.I.S і показати intro.
2. Показати onboarding: ім'я користувача та звертання.
3. Натиснути тест голосу.
4. Сказати або ввести `Jarvis стан системи`.
5. Ввести `Jarvis відкрий блокнот`.
6. Ввести `Jarvis запам'ятай, що ...`.
7. Створити нагадування.
8. Показати `Api/ApiConnectionHub.cs` і `Data/DatabaseService.cs`.
