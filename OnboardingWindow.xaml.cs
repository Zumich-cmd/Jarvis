using System.Windows;
using System.Windows.Controls;
using Jarvis.Data;
using Jarvis.Models;

namespace Jarvis;

public partial class OnboardingWindow : Window
{
    private readonly DatabaseService _database;

    public UserProfile Profile { get; private set; } = new();

    public OnboardingWindow(DatabaseService database)
    {
        InitializeComponent();
        _database = database;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        string address = (AddressCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "сер";
        Profile = new UserProfile
        {
            DisplayName = NameInput.Text.Trim(),
            AddressForm = address,
            IsOnboardingComplete = true
        };

        await _database.SaveUserProfileAsync(Profile);
        DialogResult = true;
        Close();
    }

    private async void Skip_Click(object sender, RoutedEventArgs e)
    {
        Profile = new UserProfile
        {
            DisplayName = "",
            AddressForm = "сер",
            IsOnboardingComplete = true
        };

        await _database.SaveUserProfileAsync(Profile);
        DialogResult = false;
        Close();
    }
}
