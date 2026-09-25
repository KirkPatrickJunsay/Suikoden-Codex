namespace SuikodenCodex.StarLeap;

public sealed class SlFileEntry
{
    public string Path { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Bytes { get; set; }
}

public sealed class SlManifest
{
    public int Schema { get; set; }
    public int Version { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public string Source { get; set; } = "";
    public string License { get; set; } = "";
    public string Attribution { get; set; } = "";
    public List<SlFileEntry> Files { get; set; } = new();
}

public sealed class SlEffect
{
    public string Label { get; set; } = "";
    public List<string> Values { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public int? Hits { get; set; }
    public string? Note { get; set; }
}

public sealed class SlSkill
{
    public string Slot { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Target { get; set; }
    public string? Uses { get; set; }
    public string? Rune { get; set; }
    public string? Description { get; set; }
    public string? Trigger { get; set; }
    public List<SlEffect> Effects { get; set; } = new();
}

public sealed class SlStats
{
    public List<int>? Hp { get; set; }
    public List<int>? Patk { get; set; }
    public List<int>? Matk { get; set; }
    public List<int>? Pdef { get; set; }
    public List<int>? Mdef { get; set; }
    public int? Agi { get; set; }
    public int? Hit { get; set; }
    public int? Dodge { get; set; }
    public Dictionary<string, int> Training { get; set; } = new();
}

public sealed class SlWeapon
{
    public string Type { get; set; } = "";
    public string? Growth { get; set; }
    public List<string> Names { get; set; } = new();
}

public sealed class SlUnit
{
    public const string StarLeapOrigin = "Suikoden STAR LEAP";

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? NameJp { get; set; }
    public string Title { get; set; } = "";
    public string? TitleJp { get; set; }
    public string Rarity { get; set; } = "";
    public string Role { get; set; } = "";
    public List<string> Elements { get; set; } = new();
    public List<string> Weapons { get; set; } = new();
    public string? Obtained { get; set; }
    public string? Origin { get; set; }
    public string? BasedOn { get; set; }
    public string? Released { get; set; }
    public string? Voice { get; set; }
    public string? Illustration { get; set; }
    public string Portrait { get; set; } = "";
    public SlStats? Stats { get; set; }
    public SlWeapon? Weapon { get; set; }
    public List<SlSkill> Kit { get; set; } = new();
    public string? WikiUrl { get; set; }

    public bool IsStarLeapEra => Origin == StarLeapOrigin;
    public bool IsClassicVersion => !string.IsNullOrEmpty(Origin) && !IsStarLeapEra;
    public string DisplayName => string.IsNullOrEmpty(Title) ? Name : $"{Name} — {Title}";
}

public sealed record SlPack(SlManifest Manifest, IReadOnlyList<SlUnit> Units);
