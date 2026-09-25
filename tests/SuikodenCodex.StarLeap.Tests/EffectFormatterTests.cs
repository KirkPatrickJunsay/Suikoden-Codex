using SuikodenCodex.StarLeap;

namespace SuikodenCodex.StarLeap.Tests;

public class EffectFormatterTests
{
    [Fact]
    public void Formats_levels_tags_and_hits()
    {
        var effect = new SlEffect { Label = "Physical Power", Values = { "22", "26", "30" }, Tags = { "Sword 1" }, Hits = 3 };
        Assert.Equal("Physical Power 22 → 26 → 30 · Sword 1 ×3", EffectFormatter.Format(effect));
    }

    [Fact]
    public void Formats_note_after_values()
    {
        var effect = new SlEffect { Label = "Heals one ally", Values = { "40", "50" }, Note = "HP" };
        Assert.Equal("Heals one ally 40 → 50 HP", EffectFormatter.Format(effect));
    }

    [Fact]
    public void Formats_plain_text_line()
    {
        Assert.Equal("Fill the special gauge by 100%", EffectFormatter.Format(new SlEffect { Label = "Fill the special gauge by 100%" }));
    }
}
