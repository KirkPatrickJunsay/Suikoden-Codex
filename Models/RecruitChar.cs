using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SuikodenCodex.Models;

/// <summary>A recruitable character in a game's recruitment guide (sourced from the Fandom wiki).</summary>
public class RecruitChar
{
    public int Num { get; set; }

    /// <summary>Canonical character name (links to a CodexEntry by name).</summary>
    public string Character { get; set; } = "";

    /// <summary>Display name as shown in the guide.</summary>
    public string Display { get; set; } = "";

    /// <summary>How to recruit this character.</summary>
    public string Method { get; set; } = "";

    /// <summary>Linked CodexEntry.Id, if this character exists in the codex.</summary>
    public string? EntryId { get; set; }

    /// <summary>Star of Destiny title, e.g. "Tenkai Star".</summary>
    public string? Star { get; set; }

    [JsonIgnore]
    public string Name => string.IsNullOrWhiteSpace(Display) ? Character : Display;

    // Auto-detects timing/missable language in the recruit method. This is a HINT only,
    // not an authoritative missable list — always read the full method text.
    private static readonly Regex MissableCues = new(
        @"(if\s+(you\s+)?miss(ed)?\b|missable|miss\s+your\s+chance|permanently|fail\s+to\b" +
        @"|only\s+(chance|opportunity)|last\s+chance|point\s+of\s+no\s+return" +
        @"|no\s+longer\s+(be\s+)?(able|recruit)|can\s?no?t?\s+be\s+recruited\s+(later|after|again|once)" +
        @"|before\s+(you\s+)?(leave|leaving|advanc|proceed|complet|defeat|the\s+battle|the\s+final|the\s+last|the\s+fall|the\s+end)" +
        @"|time\s+limit|limited\s+time|only\s+(recruitable|available)\s+(during|before|in)" +
        @"|during\s+this\s+(battle|chapter|event|visit))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [JsonIgnore]
    public bool MissableHint => !string.IsNullOrEmpty(Method) && MissableCues.IsMatch(Method);
}
