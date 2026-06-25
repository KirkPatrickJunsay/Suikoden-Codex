namespace SuikodenCodex.Services.CardGame;

/// <summary>Curated, link-synergised 50-card starter decks (card numbers reference cards.json).</summary>
public static class StarterDecks
{
    public record Deck(string Name, string Blurb, string[] Numbers);

    public static readonly Deck DawnRunners = new(
        "Dawn Runners",
        "Liberation heroes — fast, link-rich Leaders that chain into quick Strength missions.",
        new[]{
            "CS2-280","CS2-294","CS2-290","018","CS2-274","CS2-655","CS2-291","024","196","206",
            "398","487","CS2-671","CS2-298","CS2-651","026","195","198","078","499",
            "CS2-348","CS2-342","213","248","CS2-716","CS2-210","CS2-719","CS2-703","CS2-082","037",
            "055","126","146","208","221","226","240","409","149","150",
            "151","152","153","154","155","257","458","460","176","177"
        });

    public static readonly Deck IronFangs = new(
        "Iron Fangs",
        "Hardened veterans — heavier hitters with the same tight link web for reliable chains.",
        new[]{
            "CS2-293","CS2-661","CS2-295","CS2-272","CS2-269","CS2-142","CS2-153","025","202","207",
            "404","CS2-647","CS2-292","CS2-677","CS2-282","193","197","399","209","CS2-316",
            "CS2-345","212","214","CS2-204","CS2-357","CS2-209","CS2-305","CS2-737","033","054",
            "114","131","148","219","223","235","242","418","150","151",
            "152","153","154","155","257","458","460","462","177","178"
        });

    public static readonly Deck[] All = { DawnRunners, IronFangs };
}
