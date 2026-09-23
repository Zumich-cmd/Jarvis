namespace Jarvis.Models;

public sealed class UserProfile
{
    public string DisplayName { get; set; } = "";
    public string AddressForm { get; set; } = "сер";
    public bool IsOnboardingComplete { get; set; }

    public string GreetingName => string.IsNullOrWhiteSpace(DisplayName)
        ? AddressForm
        : $"{AddressForm} {DisplayName}";
}
