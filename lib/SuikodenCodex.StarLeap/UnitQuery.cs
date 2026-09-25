namespace SuikodenCodex.StarLeap;

public enum OriginFilter
{
    Any,
    StarLeap,
    Classic,
}

public enum UnitSort
{
    Name,
    Newest,
}

public sealed class UnitQuery
{
    public string? Text { get; init; }
    public string? Rarity { get; init; }
    public string? Element { get; init; }
    public string? Role { get; init; }
    public string? Weapon { get; init; }
    public OriginFilter Origin { get; init; }
    public UnitSort Sort { get; init; }

    public IReadOnlyList<SlUnit> Apply(IEnumerable<SlUnit> units)
    {
        var text = Text?.Trim();
        var result = units.Where(u =>
            (string.IsNullOrEmpty(text) || Matches(u, text)) &&
            (Rarity is null || u.Rarity == Rarity) &&
            (Element is null || u.Elements.Contains(Element)) &&
            (Role is null || u.Role == Role) &&
            (Weapon is null || u.Weapons.Contains(Weapon)) &&
            (Origin == OriginFilter.Any
                || (Origin == OriginFilter.StarLeap && u.IsStarLeapEra)
                || (Origin == OriginFilter.Classic && u.IsClassicVersion)));

        var ordered = Sort == UnitSort.Newest
            ? result.OrderByDescending(u => u.Released ?? "", StringComparer.Ordinal)
                .ThenBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
            : result.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(u => u.Title, StringComparer.OrdinalIgnoreCase);
        return ordered.ToList();
    }

    private static bool Matches(SlUnit u, string text) =>
        Contains(u.Name, text) || Contains(u.NameJp, text) || Contains(u.Title, text) || Contains(u.TitleJp, text);

    private static bool Contains(string? haystack, string needle) =>
        !string.IsNullOrEmpty(haystack) && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
