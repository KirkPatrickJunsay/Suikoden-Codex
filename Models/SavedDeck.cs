namespace SuikodenCodex.Models;

/// <summary>A user-built Card Duel deck (card numbers reference cards.json).</summary>
public class SavedDeck
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Deck";
    public List<string> CardNumbers { get; set; } = new();
    public string? MastermindNumber { get; set; }
}
