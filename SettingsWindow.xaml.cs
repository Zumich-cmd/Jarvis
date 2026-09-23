using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Jarvis.Services;

namespace Jarvis;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private readonly DiagnosticsService _diagnostics;
    private readonly AutoStartService _autoStart;

    public SettingsWindow(SettingsService settings, DiagnosticsService diagnostics, AutoStartService autoStart)
    {
        InitializeComponent();
        _settings = settings;
        _diagnostics = diagnostics;
        _autoStart = autoStart;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var current = _settings.Current;
        WakeWordInput.Text = current.WakeWord;
        SelectComboItem(SpeechProviderBox, current.SpeechRecognitionProvider);
        DirectVoiceCommandsCheck.IsChecked = current.DirectVoiceCommands;
        MicGainSlider.Value = current.MicrophoneGain;

        SelectComboItem(VoiceProviderBox, current.VoiceProvider);
        VoiceResponsesCheck.IsChecked = current.VoiceResponses;
        ElevenVoiceIdInput.Text = current.ElevenLabsVoiceId;

        GroqModelInput.Text = current.GroqModel;
        SelectComboItem(LanguageBox, current.Language);
        SelectComboItem(ResponseStyleBox, current.ResponseStyle);

        SaveHistoryCheck.IsChecked = current.SaveHistory;
        NotificationsCheck.IsChecked = current.Notifications;
        AutoStartCheck.IsChecked = _autoStart.IsEnabled();
        AnimationsCheck.IsChecked = current.Animations;

        UpdateServiceStatus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var current = _settings.Current;
        current.WakeWord = WakeWordInput.Text.Trim();
        current.SpeechRecognitionProvider = GetComboText(SpeechProviderBox, "GroqWhisper");
        current.DirectVoiceCommands = DirectVoiceCommandsCheck.IsChecked == true;
        current.MicrophoneGain = (float)MicGainSlider.Value;

        current.VoiceProvider = GetComboText(VoiceProviderBox, "ElevenLabs");
        current.VoiceResponses = VoiceResponsesCheck.IsChecked == true;
        current.ElevenLabsVoiceId = ElevenVoiceIdInput.Text.Trim();

        current.GroqModel = GroqModelInput.Text.Trim();
        current.Language = GetComboText(LanguageBox, "uk-UA");
        current.ResponseStyle = GetComboText(ResponseStyleBox, "Short");

        current.SaveHistory = SaveHistoryCheck.IsChecked == true;
        current.Notifications = NotificationsCheck.IsChecked == true;
        current.AutoStart = AutoStartCheck.IsChecked == true;
        current.Animations = AnimationsCheck.IsChecked == true;

        _autoStart.SetEnabled(current.AutoStart);
        _settings.Save(current);

        SaveStatusText.Text = "Налаштування збережено.";
        UpdateServiceStatus();
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void MicGainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MicGainText != null)
            MicGainText.Text = $"{e.NewValue:0.0}x";
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void UpdateServiceStatus()
    {
        ServiceStatusText.Text = _diagnostics.GetServiceStatus();
    }

    private static void SelectComboItem(System.Windows.Controls.ComboBox comboBox, string value)
    {
        foreach (ComboBoxItem item in comboBox.Items)
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private static string GetComboText(System.Windows.Controls.ComboBox comboBox, string fallback)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? fallback;
    }
}
