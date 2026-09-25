using System.Text.RegularExpressions;

namespace SuikodenCodex.StarLeap;

public sealed record ClassicCharacter(string Id, string Name, string? Game);

public static partial class ClassicMatcher
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Viki"] = "Vicky",
    };

    public static string? Match(SlUnit unit, IReadOnlyList<ClassicCharacter> classics)
    {
        var name = unit.BasedOn ?? unit.Name;
        if (Aliases.TryGetValue(name, out var alias))
            name = alias;
        var candidates = classics.Where(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (candidates.Count <= 1)
            return candidates.FirstOrDefault()?.Id;
        var token = unit.IsClassicVersion ? GameToken(unit.Origin!) : "I";
        return (candidates.FirstOrDefault(c => token is not null && GameTokens(c.Game).Contains(token)) ?? candidates[0]).Id;
    }

    private static string? GameToken(string origin)
    {
        var trimmed = origin.Trim();
        if (string.Equals(trimmed, "Suikoden", StringComparison.OrdinalIgnoreCase))
            return "I";
        var match = RomanRegex().Match(trimmed);
        return match.Success ? match.Value : null;
    }

    private static IReadOnlyCollection<string> GameTokens(string? game) =>
        string.IsNullOrEmpty(game) ? Array.Empty<string>() : RomanRegex().Matches(game).Select(m => m.Value).ToHashSet();

    [GeneratedRegex(@"\b(III|II|IV|I|V)\b")]
    private static partial Regex RomanRegex();
}
