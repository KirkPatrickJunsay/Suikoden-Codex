namespace SuikodenCodex.Pages;

public record OnboardSlide(string Emoji, string Title, string Body);

public partial class OnboardingPage : ContentPage
{
    public const string SeenKey = "onboarded";

    public OnboardingPage()
    {
        InitializeComponent();
        Carousel.ItemsSource = new List<OnboardSlide>
        {
            new("⚜", "Welcome to Suikoden Codex",
                "A free, offline fan encyclopedia for Suikoden I–V — characters, monsters, items, runes, factions and more."),
            new("📖", "Browse the Codex",
                "1,800+ entries with art and lore. Search instantly, and filter by category or game."),
            new("⭐", "Track the 108 Stars",
                "Tick off every Star of Destiny per game, see your completion %, and back up your progress to a file."),
            new("🎴", "Cards, timeline & sharing",
                "Explore 1,346 Card Stories cards, follow the series timeline, and share any card or character as an image."),
        };
    }

    private async void OnFinish(object? sender, EventArgs e)
    {
        Preferences.Set(SeenKey, true);
        await Navigation.PopModalAsync();
    }
}
