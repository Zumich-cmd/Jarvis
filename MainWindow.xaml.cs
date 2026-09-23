using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Jarvis.Commands;
using Jarvis.Api;
using Jarvis.Data;
using Jarvis.Models;
using Jarvis.Services;
using Jarvis.ViewModels;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using Forms = System.Windows.Forms;

namespace Jarvis
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel = new MainViewModel();
        private readonly SettingsService _settings = new SettingsService();
        private readonly DatabaseService _database = new DatabaseService();
        private readonly SystemMonitorService _monitor = new SystemMonitorService();
        private readonly WindowsAppService _apps = new WindowsAppService();
        private readonly FileSearchService _files = new FileSearchService();
        private readonly AutoStartService _autoStart = new AutoStartService();
        private readonly DiagnosticsService _diagnostics;
        private readonly ReminderService _reminders;
        private readonly AiService _ai;
        private readonly CommandRouter _router;
        private readonly VoiceAssistant _voice;
        private readonly ApiConnectionHub _apiHub;
        private readonly Forms.NotifyIcon _trayIcon;
        private readonly ObservableCollection<LogEntry> _visibleLogs = new ObservableCollection<LogEntry>();
        private bool _awaitingVoiceCommand;
        private DateTime _voiceCommandDeadline = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();
            _settings.Load();
            _diagnostics = new DiagnosticsService(_settings);
            _reminders = new ReminderService(_database);
            _ai = new AiService(_settings, _database);
            _router = new CommandRouter(_ai, _database, _apps, _files, _reminders, _monitor, _settings, _diagnostics);
            _voice = new VoiceAssistant(_settings);
            _voice.VoiceStatus += (category, title, subtitle) =>
                Dispatcher.Invoke(() => AddLog(category, "VOX", title, subtitle, "#FF6B6B"));
            _apiHub = new ApiConnectionHub(_ai, _voice, _settings);
            DataContext = _viewModel;
            LogListBox.ItemsSource = _visibleLogs;
            Opacity = 0;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;

            _trayIcon = CreateTrayIcon();
            _reminders.ReminderDue += OnReminderDueAsync;

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (s, e) =>
            {
                ClockText.Text = DateTime.Now.ToString("HH:mm");
                DateText.Text = DateTime.Now.ToString("d MMMM yyyy", new System.Globalization.CultureInfo("uk-UA"));

                var snapshot = _monitor.GetSnapshot();
                CpuText.Text = $"{snapshot.CpuPercent:0}%";
                RamText.Text = $"{snapshot.RamPercent:0}%";
                DiskText.Text = $"{snapshot.DiskPercent:0}%";
                NetworkText.Text = $"{snapshot.NetworkKbps:0} KB/s";
                BatteryText.Text = $"{snapshot.BatteryStatus} / uptime {snapshot.Uptime:dd\\.hh\\:mm}";
                ProcessListBox.ItemsSource = _monitor.GetTopProcesses(5)
                    .Select(p => $"{p.Name}  {p.MemoryDisplay}")
                    .ToList();
            };
            timer.Start();

            bool notepadWasOpen = false;
            bool calcWasOpen = false;
            bool chromeWasOpen = false;

            var processTimer = new System.Windows.Threading.DispatcherTimer();
            processTimer.Interval = TimeSpan.FromSeconds(2);
            processTimer.Tick += (s, e) =>
            {
                bool notepadOpen = System.Diagnostics.Process.GetProcessesByName("notepad").Length > 0;
                bool calcOpen = System.Diagnostics.Process.GetProcessesByName("CalculatorApp").Length > 0;
                bool chromeOpen = System.Diagnostics.Process.GetProcessesByName("chrome").Length > 0;

                if (notepadWasOpen && !notepadOpen)
                    AddLog("SYSTEM", "app", "Блокнот закрито", "Notepad завершено", "#FF6B6B");
                if (calcWasOpen && !calcOpen)
                    AddLog("SYSTEM", "app", "Калькулятор закрито", "Calculator завершено", "#FF6B6B");
                if (chromeWasOpen && !chromeOpen)
                    AddLog("SYSTEM", "app", "Браузер закрито", "Chrome завершено", "#FF6B6B");

                notepadWasOpen = notepadOpen;
                calcWasOpen = calcOpen;
                chromeWasOpen = chromeOpen;
            };
            processTimer.Start();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StartFadeIn();
            StartMicAnimation();
            StartCoreAnimation();
            await _database.InitializeAsync();
            await _settings.LoadSecretsFromDatabaseAsync(_database);
            _reminders.Start();
            _trayIcon.Visible = true;
            _settings.Current.AutoStart = _autoStart.IsEnabled();
            ShowWelcomeDiagnosticsIfNeeded();

            // Спочатку завантажуємо модель.
            await EnsureModelDownloaded();

            UserProfile? profile = await EnsureUserProfileAsync();

            // Тільки після завантаження ініціалізуємо Vosk.
            _voice.InitSpeech(async (text) =>
            {
                bool hasWakeWord = _router.HasWakeWord(text);
                bool acceptsFollowUp = _awaitingVoiceCommand && DateTime.Now <= _voiceCommandDeadline;
                bool acceptsDirectCommand = _settings.Current.DirectVoiceCommands;

                if (!hasWakeWord && !acceptsFollowUp && !acceptsDirectCommand)
                    return;

                string command = hasWakeWord ? _router.StripWakeWord(text).Trim() : text.Trim();

                if (string.IsNullOrWhiteSpace(command))
                {
                    _awaitingVoiceCommand = true;
                    _voiceCommandDeadline = DateTime.Now.AddSeconds(8);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        AiStateText.Text = "Слухаю команду...";
                        AiSubStateText.Text = "Скажіть команду після слова Jarvis";
                        AddLog("VOICE", "mic", "Активовано голосом", "Очікую команду 8 секунд", "#00C8FF");
                    });
                    return;
                }

                _awaitingVoiceCommand = false;
                _voiceCommandDeadline = DateTime.MinValue;

                await Dispatcher.InvokeAsync(() =>
                {
                    AiStateText.Text = "Слухаю команду...";
                    string title = hasWakeWord
                        ? "Активовано голосом"
                        : acceptsFollowUp
                            ? "Команда після активації"
                            : "Голосова команда";
                    AddLog("VOICE", "mic", title, $"Команда: {command}", "#00C8FF");
                });

                await Application.Current.Dispatcher.InvokeAsync(async () => await ProcessCommandAsync(command, "voice", false));
            });

            AddLog("SYSTEM", "shield", "Програму запущено", "J.A.R.V.I.S активний", "#00FF88");

            await Task.Delay(1000);
            await _voice.GreetUserAsync(profile);
        }

        // ========================================================================
        // ЗАВАНТАЖЕННЯ МОДЕЛІ
        // ========================================================================

        private async Task EnsureModelDownloaded()
        {
            // HEAD version: downloads from R2 CDN into "model" folder
            string modelDirHead = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "model");
            string checkFileHead = System.IO.Path.Combine(modelDirHead, "am", "final.mdl");

            bool headReady = File.Exists(checkFileHead) && new FileInfo(checkFileHead).Length > 1024 * 1024;

            AiStateText.Text = "Завантаження моделі...";
            AiSubStateText.Text = "Перший запуск, зачекайте (~1 GB)";

            // ---- HEAD sources (R2 CDN) ----
            string baseUrl = "https://pub-891b2194b1004c19bbc501b7262a6c22.r2.dev";

            var filesHead = new Dictionary<string, string>
            {
                ["am/final.mdl"] = $"{baseUrl}/am/final.mdl",
                ["conf/mfcc.conf"] = $"{baseUrl}/conf/mfcc.conf",
                ["conf/model.conf"] = $"{baseUrl}/conf/model.conf",
                ["graph/HCLG.fst"] = $"{baseUrl}/graph/HCLG.fst",
                ["graph/words.txt"] = $"{baseUrl}/graph/words.txt",
                ["graph/phones/word_boundary.int"] = $"{baseUrl}/graph/phones/word_boundary.int",
                ["ivector/final.dubm"] = $"{baseUrl}/ivector/final.dubm",
                ["ivector/final.ie"] = $"{baseUrl}/ivector/final.ie",
                ["ivector/final.mat"] = $"{baseUrl}/ivector/final.mat",
                ["ivector/global_cmvn.stats"] = $"{baseUrl}/ivector/global_cmvn.stats",
                ["ivector/online_cmvn.conf"] = $"{baseUrl}/ivector/online_cmvn.conf",
                ["ivector/splice.conf"] = $"{baseUrl}/ivector/splice.conf",
                ["ivector/splice_opts"] = $"{baseUrl}/ivector/splice_opts",
            };

            var minSizes = new Dictionary<string, long>
            {
                ["am/final.mdl"] = 20_000_000,
                ["conf/mfcc.conf"] = 100,
                ["conf/model.conf"] = 100,
                ["graph/HCLG.fst"] = 800_000_000,
                ["graph/words.txt"] = 100_000_000,
                ["graph/phones/word_boundary.int"] = 1_000,
                ["ivector/final.dubm"] = 100_000,
                ["ivector/final.ie"] = 10_000_000,
                ["ivector/final.mat"] = 10_000,
                ["ivector/global_cmvn.stats"] = 100,
                ["ivector/online_cmvn.conf"] = 10,
                ["ivector/splice.conf"] = 10,
                ["ivector/splice_opts"] = 10,
            };

            // ---- front sources (HuggingFace / VASYA) ----
            var filesFront = new Dictionary<string, string>
            {
                ["am/final.mdl"] = "https://huggingface.co/Zumich312/VASYA/resolve/main/am/final.mdl",
                ["conf/mfcc.conf"] = "https://huggingface.co/Zumich312/VASYA/resolve/main/conf/mfcc.conf",
                ["conf/model.conf"] = "https://huggingface.co/Zumich312/VASYA/resolve/main/conf/model.conf",
                ["graph/HCLG.fst"] = "https://huggingface.co/Zumich312/VASYA/resolve/main/graph/HCLG.fst",
                ["graph/words.txt"] = "https://huggingface.co/Zumich312/VASYA/resolve/main/graph/words.txt",
                ["graph/phones/word_boundary.int"] = "https://huggingface.co/Zumich312/VASYA/resolve/main/graph/phones/word_boundary.int",
                ["ivector/final.dubm"] = "https://huggingface.co/Zumich312/VASYA/resolve/main/ivector/final.dubm",
                ["ivector/final.ie"] = "https://huggingface.co/Zumich312/VASYA/resolve/main/ivector/final.ie",
                ["ivector/final.mat"] = "https://huggingface.co/Zumich312/VASYA/resolve/main/ivector/final.mat",
                ["ivector/global_cmvn.stats"] = "https://huggingface.co/Zumich312/VASYA/resolve/main/ivector/global_cmvn.stats",
                ["ivector/online_cmvn.conf"] = "https://huggingface.co/Zumich312/VASYA/resolve/main/ivector/online_cmvn.conf",
                ["ivector/splice.conf"] = "https://huggingface.co/Zumich312/VASYA/resolve/main/ivector/splice.conf",
                ["ivector/splice_opts"] = "https://huggingface.co/Zumich312/VASYA/resolve/main/ivector/splice_opts",
            };

            using (HttpClient client = new HttpClient())
            {
                client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

                // Download HEAD model (R2)
                if (!headReady)
                {
                    int current = 0;
                    int total = filesHead.Count;
                    foreach (var file in filesHead)
                    {
                        current++;
                        string localPath = System.IO.Path.Combine(modelDirHead, file.Key.Replace('/', System.IO.Path.DirectorySeparatorChar));
                        string? localDirectory = System.IO.Path.GetDirectoryName(localPath);
                        if (!string.IsNullOrWhiteSpace(localDirectory))
                            Directory.CreateDirectory(localDirectory);

                        long minSize = minSizes.ContainsKey(file.Key) ? minSizes[file.Key] : 10;
                        if (File.Exists(localPath) && new FileInfo(localPath).Length >= minSize)
                            continue;

                        AiStateText.Text = $"[model] Файл {current}/{total}";
                        AiSubStateText.Text = file.Key;

                        try
                        {
                            var response = await client.GetAsync(file.Value, HttpCompletionOption.ResponseHeadersRead);
                            long? totalBytes = response.Content.Headers.ContentLength;

                            using (var stream = await response.Content.ReadAsStreamAsync())
                            using (var fileStream = File.Create(localPath))
                            {
                                byte[] buffer = new byte[81920];
                                long downloaded = 0;
                                int bytesRead;
                                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                                    downloaded += bytesRead;
                                    if (totalBytes.HasValue)
                                    {
                                        int percent = (int)(downloaded * 100 / totalBytes.Value);
                                        AiStateText.Text = $"[model] Файл {current}/{total}: {percent}%";
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Помилка завантаження {file.Key}:\n{ex.Message}");
                        }
                    }
                }
            }

            AiStateText.Text = "Очікування";
            AiSubStateText.Text = "Готовий до нових команд";
        }

        // ========================================================================
        // АНІМАЦІЇ ІНТЕРФЕЙСУ
        // ========================================================================

        private void StartFadeIn()
        {
            DoubleAnimation fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(1)
            };
            BeginAnimation(Window.OpacityProperty, fade);
        }

        private void StartMicAnimation()
        {
            MicButton.RenderTransform = null;
        }

        private void StartCoreAnimation()
        {
            RotateTransform rotate = new RotateTransform();
            OuterRing.RenderTransform = rotate;
            OuterRing.RenderTransformOrigin = new Point(0.5, 0.5);

            DoubleAnimation rotation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(15),
                RepeatBehavior = RepeatBehavior.Forever
            };

            rotate.BeginAnimation(RotateTransform.AngleProperty, rotation);
        }

        // ========================================================================
        // КЕРУВАННЯ ВІКНОМ
        // ========================================================================

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
                this.WindowState = WindowState.Maximized;
            else
                this.WindowState = WindowState.Normal;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private async Task<UserProfile?> EnsureUserProfileAsync()
        {
            UserProfile? profile = await _database.GetUserProfileAsync();
            if (profile?.IsOnboardingComplete == true)
                return profile;

            var onboarding = new OnboardingWindow(_database)
            {
                Owner = this
            };
            onboarding.ShowDialog();
            AddLog("SYSTEM", "profile", "Профіль користувача", $"Звертання: {onboarding.Profile.GreetingName}", "#00FF88");
            return onboarding.Profile;
        }

        // ========================================================================
        // ЖУРНАЛ, ЧАТ, НАЛАШТУВАННЯ
        // ========================================================================

        private void AddLog(string category, string icon, string title, string subtitle, string iconColor)
        {
            _viewModel.Logs.Insert(0, new LogEntry
            {
                Category = category,
                Icon = icon,
                Title = $"[{category}] {title}",
                Subtitle = subtitle,
                IconColor = iconColor,
                Time = DateTime.Now.ToString("HH:mm:ss")
            });
            ApplyLogFilter();
        }

        private void ApplyLogFilter()
        {
            string selected = (LogFilterBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ALL";
            _visibleLogs.Clear();
            foreach (var entry in _viewModel.Logs.Where(l => selected == "ALL" || l.Category == selected).Take(80))
                _visibleLogs.Add(entry);
        }

        private void LogFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyLogFilter();

        private void LogListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LogListBox.SelectedItem is LogEntry entry)
            {
                _viewModel.Logs.Remove(entry);
                ApplyLogFilter();
            }
        }

        private void ChatInput_GotFocus(object sender, RoutedEventArgs e)
        {
            VoiceCorePanel.Visibility = Visibility.Collapsed;
            TextChatPanel.Visibility = Visibility.Visible;
        }

        private void ChatInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            ChatPlaceholder.Visibility = string.IsNullOrEmpty(ChatInput.Text) ? Visibility.Visible : Visibility.Hidden;
        }

        private async void ChatInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            string command = ChatInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(command))
                return;

            AddChatMessage(command, true);
            ChatInput.Text = "";
            await ProcessCommandAsync(command, "text", true);
        }

        private async Task ProcessCommandAsync(string command, string source, bool showAssistantMessage)
        {
            if (RequiresCloseConfirmation(command) &&
                MessageBox.Show("J.A.R.V.I.S збирається завершити процес. Продовжити?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                AddLog("COMMAND", "app", "Закриття скасовано", command, "#FFB020");
                return;
            }

            AiStateText.Text = "Обробка...";
            AiSubStateText.Text = command;

            try
            {
                CommandResult result = await _router.ExecuteAsync(command, source);
                if (showAssistantMessage)
                    AddChatMessage(result.Response, false);

                AddLog(result.Category, result.Icon, result.Title, result.Response, result.Color);
                if (result.ShouldSpeak)
                    _ = _voice.SpeakAsync(result.Response);
            }
            catch (Exception ex)
            {
                string message = $"Помилка виконання команди: {ex.Message}";
                AddLog("ERROR", "error", "Помилка команди", message, "#FF6B6B");
                if (showAssistantMessage)
                    AddChatMessage(message, false);
            }
            finally
            {
                AiStateText.Text = "Очікування";
                AiSubStateText.Text = "Готовий до нових команд";
            }
        }

        private void AddChatMessage(string text, bool isUser)
        {
            Border bubble = new Border
            {
                CornerRadius = isUser ? new CornerRadius(15, 15, 0, 15) : new CornerRadius(15, 15, 15, 0),
                Background = isUser ? new SolidColorBrush(MediaColor.FromRgb(0, 97, 255)) : new SolidColorBrush(MediaColor.FromRgb(26, 36, 56)),
                Padding = new Thickness(15, 10, 15, 10),
                Margin = isUser ? new Thickness(50, 0, 0, 15) : new Thickness(0, 0, 50, 15),
                HorizontalAlignment = isUser ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left
            };

            TextBlock messageText = new TextBlock
            {
                Text = text,
                Foreground = MediaBrushes.White,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            };

            bubble.Child = messageText;
            ChatHistoryPanel.Children.Add(bubble);
            ChatScroll.ScrollToBottom();
        }

        // ========================================================================
        // КНОПКА МІКРОФОНА
        // ========================================================================

        private void MicButton_Click(object sender, RoutedEventArgs e)
        {
            // front: toggle voice panel visibility
            TextChatPanel.Visibility = Visibility.Collapsed;
            VoiceCorePanel.Visibility = Visibility.Visible;
            Keyboard.ClearFocus();

            // HEAD: toggle VoiceAssistant listening state
            if (_voice.IsListening)
            {
                _voice.StopListening();
                AiStateText.Text = "Мікрофон вимкнено";
                AiSubStateText.Text = "Натисніть мікрофон для старту";
                OuterRing.Stroke = new SolidColorBrush(MediaColor.FromRgb(0, 97, 255));
                AddLog("VOICE", "mic", "Мікрофон вимкнено", "Очікування команди", "#FF6B6B");
            }
            else
            {
                _voice.StartListening();
                AiStateText.Text = "Слухаю...";
                AiSubStateText.Text = "Говоріть зараз...";
                OuterRing.Stroke = new SolidColorBrush(MediaColor.FromRgb(255, 69, 0));
                AddLog("VOICE", "mic", "Мікрофон увімкнено", "Очікування слова Jarvis", "#00FF88");
            }
        }

        // ========================================================================
        // ШВИДКІ ДІЇ
        // ========================================================================

        private async void BtnOpenNotepad_Click(object sender, RoutedEventArgs e) => await ProcessCommandAsync("відкрий блокнот", "quick", false);

        private async void BtnOpenCalc_Click(object sender, RoutedEventArgs e) => await ProcessCommandAsync("відкрий калькулятор", "quick", false);

        private async void BtnOpenBrowser_Click(object sender, RoutedEventArgs e) => await ProcessCommandAsync("відкрий браузер", "quick", false);

        private async void BtnTestVoice_Click(object sender, RoutedEventArgs e)
        {
            string text = "Голосова система JARVIS активна.";
            AddLog("VOICE", "VOX", "Тест голосу", text, "#F2B84B");
            await _voice.SpeakAsync(text);
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Logs.Clear();
            ApplyLogFilter();
            AddLog("SYSTEM", "clear", "Журнал очищено", "Усі записи видалено", "#FF6B6B");
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var window = new SettingsWindow(_settings, _diagnostics, _autoStart)
            {
                Owner = this
            };

            if (window.ShowDialog() == true)
                AddLog("SYSTEM", "settings", "Налаштування оновлено", "Конфігурацію збережено, ключі залишаються в SQLite", "#00C8FF");
        }

        private void ShowWelcomeDiagnosticsIfNeeded()
        {
            if (!string.IsNullOrWhiteSpace(_settings.Current.GroqApiKey) &&
                !string.IsNullOrWhiteSpace(_settings.Current.ElevenLabsApiKey))
                return;

            AddLog("SYSTEM", "system", "Welcome diagnostics", _diagnostics.GetServiceStatus(), "#00FF88");
        }

        private static bool RequiresCloseConfirmation(string command)
        {
            return command.Contains("закрий", StringComparison.OrdinalIgnoreCase)
                || command.Contains("закрити", StringComparison.OrdinalIgnoreCase)
                || command.Contains("закрой", StringComparison.OrdinalIgnoreCase)
                || command.Contains("закрыть", StringComparison.OrdinalIgnoreCase)
                || command.Contains("зупини", StringComparison.OrdinalIgnoreCase)
                || command.Contains("останови", StringComparison.OrdinalIgnoreCase)
                || command.Contains("close", StringComparison.OrdinalIgnoreCase);
        }

        private Forms.NotifyIcon CreateTrayIcon()
        {
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Відкрити інтерфейс", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
            menu.Items.Add("Увімкнути/вимкнути мікрофон", null, (_, _) => Dispatcher.Invoke(() => MicButton_Click(this, new RoutedEventArgs())));
            menu.Items.Add("Автозапуск", null, (_, _) => Dispatcher.Invoke(ToggleAutoStart));
            menu.Items.Add("Вихід", null, (_, _) => Dispatcher.Invoke(Application.Current.Shutdown));

            var notifyIcon = new Forms.NotifyIcon
            {
                Text = "J.A.R.V.I.S",
                Icon = System.Drawing.SystemIcons.Application,
                ContextMenuStrip = menu
            };
            notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
            return notifyIcon;
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ToggleAutoStart()
        {
            bool next = !_autoStart.IsEnabled();
            _autoStart.SetEnabled(next);
            _settings.Current.AutoStart = next;
            _settings.Save(_settings.Current);
            AddLog("SYSTEM", "settings", "Автозапуск оновлено", next ? "Увімкнено" : "Вимкнено", "#00C8FF");
        }

        private async Task OnReminderDueAsync(Reminder reminder)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                AddLog("COMMAND", "alarm", "Нагадування", reminder.Text, "#FFB020");
                if (_settings.Current.Notifications)
                    _trayIcon.ShowBalloonTip(5000, "J.A.R.V.I.S нагадує", reminder.Text, Forms.ToolTipIcon.Info);
            });
            await _voice.SpeakAsync($"Нагадування: {reminder.Text}");
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _monitor.Dispose();
        }
    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        internal static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
    }
}
