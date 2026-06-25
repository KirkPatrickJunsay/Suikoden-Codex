using System.Text.Json.Serialization;

namespace SuikodenCodex.Models;

/// <summary>
/// A single encyclopedia entry, translated from the Genso Suikoden Kiwami Encyclopedia.
/// </summary>
public class CodexEntry
{
    public string Id { get; set; } = "";

    /// <summary>English (translated) headword.</summary>
    public string Name { get; set; } = "";

    /// <summary>Original Japanese headword, shown alongside the translation.</summary>
    public string? OriginalName { get; set; }

    /// <summary>Reading / romanization shown under the name in the book.</summary>
    public string? Reading { get; set; }

    public EntryCategory Category { get; set; }

    /// <summary>Free-form subtype, e.g. "True Rune", "Star of Destiny", "Unique Rune".</summary>
    public string? Subtype { get; set; }

    /// <summary>Which game(s) this entry belongs to, e.g. "Suikoden I".</summary>
    public string? Game { get; set; }

    /// <summary>Translated body text.</summary>
    public string Description { get; set; } = "";

    /// <summary>Optional flavor quote (the 「熱き言葉」 footer lines in the book).</summary>
    public string? Quote { get; set; }

    /// <summary>Entry Ids referenced via the book's ☞ cross-reference markers.</summary>
    public List<string> CrossRefs { get; set; } = new();

    /// <summary>Source scan page number (file pages/page_{N}.jpg), if bundled.</summary>
    public int? SourcePage { get; set; }

    /// <summary>Bundled artwork file (in Resources/Images), cropped from the source scan. Optional.</summary>
    public string? ImageName { get; set; }

    /// <summary>Structured profile details (Star, Origin, Race, Age…) shown as a small info table.</summary>
    public List<Fact> Facts { get; set; } = new();

    /// <summary>Collapsible narrative sections (Appearance, Personality, History).</summary>
    public List<Section> Sections { get; set; } = new();

    /// <summary>How to recruit this character (shown as a highlighted card).</summary>
    public string? Recruitment { get; set; }

    /// <summary>Trivia / "Did you know?" bullets (shown as a highlighted card).</summary>
    public string? Trivia { get; set; }

    /// <summary>In-universe Solar Year (for War/event entries on the series timeline).</summary>
    public int? Year { get; set; }

    /// <summary>True when <see cref="Year"/> is an estimate (game baseline, not an explicit date).</summary>
    public bool YearApprox { get; set; }

    // ---- Derived / runtime-only ----

    [JsonIgnore]
    public string CategoryDisplay => Category.Display();

    [JsonIgnore]
    public string Glyph => Category.Glyph();

    [JsonIgnore]
    public string AccentColor => Category.Color();

    /// <summary>Index letter used for A–Z grouping in the browse list.</summary>
    [JsonIgnore]
    public string IndexLetter =>
        string.IsNullOrEmpty(Name) ? "#"
        : char.IsLetter(Name[0]) ? Name[0].ToString().ToUpperInvariant()
        : "#";

    [JsonIgnore]
    public bool HasSourcePage => SourcePage.HasValue;

    [JsonIgnore]
    public bool HasFacts => Facts.Count > 0;

    [JsonIgnore]
    public bool HasSections => Sections.Count > 0;

    [JsonIgnore]
    public bool HasRecruitment => !string.IsNullOrWhiteSpace(Recruitment);

    [JsonIgnore]
    public bool HasTrivia => !string.IsNullOrWhiteSpace(Trivia);

    [JsonIgnore]
    public bool HasImage => !string.IsNullOrEmpty(ImageName);

    /// <summary>True when no artwork is bundled, so the emoji glyph should be shown instead.</summary>
    [JsonIgnore]
    public bool ShowGlyph => string.IsNullOrEmpty(ImageName);

    [JsonIgnore]
    public string Subtitle =>
        string.Join("  •  ",
            new[] { CategoryDisplay, Game, Subtype }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}
