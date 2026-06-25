namespace SuikodenCodex.Models;

/// <summary>
/// The entry categories used by the Genso Suikoden Kiwami Encyclopedia.
/// Mirrors the source book's tags (キャラクター / モンスター / アイテム …).
/// </summary>
public enum EntryCategory
{
    Character,
    Monster,
    Item,
    Rune,
    Region,
    Faction,
    War,
    ComboAttack,
    Other
}

public static class EntryCategoryExtensions
{
    public static string Display(this EntryCategory c) => c switch
    {
        EntryCategory.Character => "Character",
        EntryCategory.Monster => "Monster",
        EntryCategory.Item => "Item / Event",
        EntryCategory.Rune => "Rune",
        EntryCategory.Region => "Region",
        EntryCategory.Faction => "Faction / Nation",
        EntryCategory.War => "War / Battle",
        EntryCategory.ComboAttack => "Combo Attack",
        EntryCategory.Other => "Other",
        _ => c.ToString()
    };

    /// <summary>An emoji glyph used as a lightweight icon (no licensed art is bundled).</summary>
    public static string Glyph(this EntryCategory c) => c switch
    {
        EntryCategory.Character => "🧑",
        EntryCategory.Monster => "👹",
        EntryCategory.Item => "🎒",
        EntryCategory.Rune => "🔮",
        EntryCategory.Region => "🗺️",
        EntryCategory.Faction => "⚜️",
        EntryCategory.War => "⚔️",
        EntryCategory.ComboAttack => "✨",
        EntryCategory.Other => "📖",
        _ => "📖"
    };

    /// <summary>Accent color (hex) per category, used for chips and headers.</summary>
    public static string Color(this EntryCategory c) => c switch
    {
        EntryCategory.Character => "#3B6FB6",
        EntryCategory.Monster => "#B6453B",
        EntryCategory.Item => "#B6863B",
        EntryCategory.Rune => "#7A3BB6",
        EntryCategory.Region => "#3BA06E",
        EntryCategory.Faction => "#2E8B8B",
        EntryCategory.War => "#5A5A5A",
        EntryCategory.ComboAttack => "#C28A1E",
        EntryCategory.Other => "#4A6572",
        _ => "#4A6572"
    };
}
