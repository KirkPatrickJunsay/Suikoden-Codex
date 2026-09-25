using SuikodenCodex.StarLeap;

namespace SuikodenCodex.StarLeap.Tests;

public class UnitQueryTests
{
    private static readonly SlUnit[] Units =
    {
        new() { Id = "a", Name = "Aegir", NameJp = "エギル", Title = "Night Lightning's Shadow", Rarity = "SSR", Role = "Attack",
                Elements = { "Lightning" }, Weapons = { "Sword" }, Origin = SlUnit.StarLeapOrigin, Released = "2026-07-08" },
        new() { Id = "f", Name = "Flik", NameJp = "フリック", Title = "Blue Thunder", Rarity = "Guest", Role = "Attack",
                Elements = { "Lightning" }, Weapons = { "Sword" }, Origin = "Suikoden", Released = "2026-08-26" },
        new() { Id = "h", Name = "Hisui", NameJp = "ヒスイ", Title = "", Rarity = "108 Stars", Role = "Recover",
                Elements = { "Wind" }, Weapons = { "Wisdom" }, Origin = SlUnit.StarLeapOrigin, Released = "2026-08-07" },
    };

    [Fact]
    public void Matches_japanese_names() =>
        Assert.Equal(new[] { "f" }, new UnitQuery { Text = "フリック" }.Apply(Units).Select(u => u.Id));

    [Fact]
    public void Filters_by_rarity_and_element() =>
        Assert.Equal(new[] { "a" }, new UnitQuery { Rarity = "SSR", Element = "Lightning" }.Apply(Units).Select(u => u.Id));

    [Fact]
    public void Filters_classic_origin() =>
        Assert.Equal(new[] { "f" }, new UnitQuery { Origin = OriginFilter.Classic }.Apply(Units).Select(u => u.Id));

    [Fact]
    public void Sorts_newest_first() =>
        Assert.Equal(new[] { "f", "h", "a" }, new UnitQuery { Sort = UnitSort.Newest }.Apply(Units).Select(u => u.Id));
}
