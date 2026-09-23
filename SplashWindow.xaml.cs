using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MediaColor = System.Windows.Media.Color;
using MediaFontFamily = System.Windows.Media.FontFamily;

namespace Jarvis
{
    /// <summary>
    /// Вікно завантаження системи J.A.R.V.I.S
    /// Відповідає за boot sequence: анімації, текст, звук і перехід у MainWindow
    /// </summary>
    public partial class SplashWindow : Window
    {
        // Відтворення фонової музики (тема Iron Man / boot sound)
        private MediaPlayer _backgroundMusic = new MediaPlayer();

        public SplashWindow()
        {
            InitializeComponent(); // Підключення XAML
        }

        /// <summary>
        /// Подія запуску вікна
        /// Тут стартує вся система завантаження
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Завантаження аудіо-файлу (локальний ресурс)
                _backgroundMusic.Open(new Uri("ironman_theme.mp3", UriKind.Relative));

                // Гучність фону (0.0 - 1.0)
                _backgroundMusic.Volume = 0.5;

                // Запуск музики
                _backgroundMusic.Play();
            }
            catch (Exception ex)
            {
                // Якщо аудіо не завантажилось — просто лог у консоль
                Console.WriteLine("Ошибка аудио: " + ex.Message);
            }

            // Запуск послідовності завантаження системи
            await RunBootSequenceAsync();
        }

        /// <summary>
        /// Основна boot-послідовність:
        /// - затримки як у "завантаженні системи"
        /// - анімація реактора
        /// - друк назви J.A.R.V.I.S
        /// - вивід системних повідомлень
        /// - перехід у головне вікно
        /// </summary>
        private async Task RunBootSequenceAsync()
        {
            // Пауза перед стартом (ефект ініціалізації)
            await Task.Delay(1000);

            // Запуск анімації реактора (ядро + кільце)
            AnimateReactor();

            await Task.Delay(1500);

            // Ефект друку тексту (typewriter effect)
            string title = "J.A.R.V.I.S";
            for (int i = 0; i <= title.Length; i++)
            {
                TitleText.Text = title.Substring(0, i);
                await Task.Delay(150); // швидкість "друку"
            }

            await Task.Delay(500);

            // Список системних повідомлень boot-процесу
            string[] messages =
            {
                "> Boot sequence started",
                "> Neural core online",
                "> Voice module online",
                "> Command processor online",
                "> System ready"
            };

            // Поступове виведення логів у UI
            foreach (var msg in messages)
            {
                TextBlock tb = new TextBlock
                {
                    Text = msg,
                    Foreground = new SolidColorBrush(MediaColor.FromRgb(41, 255, 230)),
                    FontFamily = new MediaFontFamily("Consolas"),
                    FontSize = 18,
                    Margin = new Thickness(0, 0, 0, 10),
                    Opacity = 0.8
                };

                // Додавання нового рядка в панель логів
                SystemMessagesPanel.Children.Add(tb);

                await Task.Delay(400); // затримка між повідомленнями
            }

            await Task.Delay(1200);

            // Фінальна анімація + перехід у головний UI
            FinalFlashAndTransition();
        }

        /// <summary>
        /// Анімація запуску реактора:
        /// - поява елементів
        /// - пульсація ядра
        /// </summary>
        private void AnimateReactor()
        {
            // Плавна поява ядра і кільця
            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1));

            ReactorCore.BeginAnimation(OpacityProperty, fadeIn);
            ReactorRing.BeginAnimation(OpacityProperty, fadeIn);

            // Пульсація ядра (ефект "живого реактора")
            DoubleAnimation pulse = new DoubleAnimation(1, 1.4, TimeSpan.FromSeconds(0.8))
            {
                AutoReverse = true, // назад до початкового стану
                RepeatBehavior = RepeatBehavior.Forever, // нескінченна пульсація

                // плавна крива анімації (реалістичніший рух)
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseInOut
                }
            };

            // Масштабування по X і Y
            CoreScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            CoreScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
        }

        /// <summary>
        /// Фінальний вибуховий ефект + перехід у MainWindow
        /// </summary>
        private void FinalFlashAndTransition()
        {
            // Зупинка постійної пульсації перед фінальним ефектом
            CoreScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            CoreScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            // "Розгін" реактора перед стартом (ефект енергії)
            DoubleAnimation flash = new DoubleAnimation(1.4, 10, TimeSpan.FromSeconds(0.6));
            CoreScale.BeginAnimation(ScaleTransform.ScaleXProperty, flash);
            CoreScale.BeginAnimation(ScaleTransform.ScaleYProperty, flash);

            // Плавне зникнення splash-вікна
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.8));

            fadeOut.Completed += (s, e) =>
            {
                // Зупинка музики
                _backgroundMusic.Stop();

                // Створення та показ головного вікна
                MainWindow main = new MainWindow();
                main.Show();

                // Закриття splash
                this.Close();
            };

            // Запуск fade-out анімації всього вікна
            this.BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
