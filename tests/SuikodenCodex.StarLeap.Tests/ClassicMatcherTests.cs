using SuikodenCodex.StarLeap;

namespace SuikodenCodex.StarLeap.Tests;

public class ClassicMatcherTests
{
    private static readonly ClassicCharacter[] Codex =
    {
        new("flik", "Flik", "Suikoden I & II"),
        new("vicky", "Vicky", "Series"),
        new("s2-gremio", "Gremio", "Suikoden II"),
        new("s1-gremio", "Gremio", "Suikoden I"),
    };

    [Fact]
    public void Matches_by_based_on_name() =>
        Assert.Equal("flik", ClassicMatcher.Match(new SlUnit { Name = "Flik", BasedOn = "Flik", Origin = "Suikoden" }, Codex));

    [Fact]
    public void Uses_alias_for_known_spelling_difference() =>
        Assert.Equal("vicky", ClassicMatcher.Match(new SlUnit { Name = "Viki", Origin = "Suikoden" }, Codex));

    [Fact]
    public void Prefers_entry_from_the_origin_game() =>
        Assert.Equal("s1-gremio", ClassicMatcher.Match(new SlUnit { Name = "Gremio", Origin = "Suikoden" }, Codex));

    [Fact]
    public void Star_leap_era_version_links_to_its_classic_entry() =>
        Assert.Equal("s1-gremio", ClassicMatcher.Match(new SlUnit { Name = "Gremio", Origin = SlUnit.StarLeapOrigin }, Codex));

    [Fact]
    public void New_star_leap_characters_have_no_classic_entry() =>
        Assert.Null(ClassicMatcher.Match(new SlUnit { Name = "Hisui", Origin = SlUnit.StarLeapOrigin }, Codex));
}
