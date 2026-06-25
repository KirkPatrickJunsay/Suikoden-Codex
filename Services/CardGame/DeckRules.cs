using SuikodenCodex.Models;

namespace SuikodenCodex.Services.CardGame;

public record DeckCheck(bool Valid, List<string> Issues, List<string> Warnings);

/// <summary>Validates a deck against the Card Stories construction rules.</summary>
public static class DeckRules
{
    public const int Size = 50;
    public const int MaxCopies = 4;

    static IEnumerable<string> Camps(CardEntry c)
    {
        if (!string.IsNullOrEmpty(c.Camp)) yield return c.Camp;
        if (!string.IsNullOrEmpty(c.Camp2)) yield return c.Camp2;
    }

    public static DeckCheck Check(List<CardEntry> cards, CardEntry? mastermind)
    {
        var issues = new List<string>();
        var warnings = new List<string>();

        if (cards.Count != Size)
            issues.Add($"Deck must be exactly {Size} cards — currently {cards.Count}.");

        foreach (var g in cards.GroupBy(c => c.Name))
            if (g.Count() > MaxCopies)
                issues.Add($"Max {MaxCopies} copies of a card — you have {g.Count()}× {g.Key}.");

        if (mastermind is not null)
        {
            var mc = Camps(mastermind).ToHashSet();
            if (mc.Count > 0)
            {
                var offender = cards.FirstOrDefault(c =>
                    c.Type is "Leader" or "Commoner" && Camps(c).Any() && !Camps(c).Any(mc.Contains));
                if (offender is not null)
                    issues.Add($"Mastermind {mastermind.Name}: {offender.Name}'s camp must match one of [{string.Join("/", mc)}].");
            }
            if (cards.Any(c => c.Name == mastermind.Name))
                issues.Add($"Unique Rule: remove characters named {mastermind.Name} (your Mastermind).");
        }

        int leaders = cards.Count(c => c.Type == "Leader");
        int goals = cards.Count(c => c.Type is "Mission" or "Facilities");
        if (cards.Count >= Size && leaders < 12)
            warnings.Add($"Only {leaders} Leaders — aim for ~16–20 so you can always open a battle.");
        if (cards.Count >= Size && goals < 8)
            warnings.Add($"Only {goals} Missions/Facilities — aim for ~10 to have objectives to play.");

        return new DeckCheck(issues.Count == 0, issues, warnings);
    }
}
