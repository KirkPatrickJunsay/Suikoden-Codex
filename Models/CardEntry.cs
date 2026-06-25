namespace SuikodenCodex.Models;

/// <summary>A card from Genso Suikoden Card Stories (the GBA trading-card spin-off).</summary>
public class CardEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Number { get; set; } = "";
    public string Pack { get; set; } = "";
    public string Type { get; set; } = "";
    public string Camp { get; set; } = "";
    public string Gender { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Special { get; set; } = "";
    public string EffectEn { get; set; } = "";
    public string EffectJp { get; set; } = "";
    public string? OriginalName { get; set; }
    public string? Reading { get; set; }
    public string? ImageName { get; set; }

    // ---- Card Stories game stats (from gensopedia {{Card}} infoboxes) ----
    public List<string> Links { get; set; } = new();
    public int? Str { get; set; }
    public int? Mil { get; set; }
    public int? Con { get; set; }
    public int? Cp { get; set; }       // mission/facility clear points
    public string? Cbtype { get; set; } // "STR" or "MIL" clear-by
    public string? Vp { get; set; }     // mission VP, e.g. "1" or "1/2"
    public int? Bp { get; set; }        // facility block points
    public string? Camp2 { get; set; }

    public bool HasImage => !string.IsNullOrEmpty(ImageName);
    public bool HasEffectEn => !string.IsNullOrWhiteSpace(EffectEn);
    public bool HasEffectJp => !string.IsNullOrWhiteSpace(EffectJp);
    public bool HasArtist => !string.IsNullOrWhiteSpace(Artist);
    public string NumberLabel => string.IsNullOrEmpty(Number) ? "" : $"#{Number}";

    /// <summary>One-line subtitle used in lists.</summary>
    public string Subtitle =>
        string.Join(" · ", new[] { NumberLabel, Type, Camp }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string SearchText => $"{Name} {Number} {Type} {Camp} {Pack} {Artist} {OriginalName}".ToLowerInvariant();
}
