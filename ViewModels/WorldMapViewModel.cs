using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using SuikodenCodex.Pages;

namespace SuikodenCodex.ViewModels;

public enum MapPinKind { Nation, Region, Mountain, Forest }
public enum MapTier { Nation, Major, Minor }
public enum MapDetail { Nations, Towns, All }

public partial class MapPin
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public MapPinKind Kind { get; init; }
    public MapTier Tier { get; init; }
    public int[] Games { get; init; } = System.Array.Empty<int>();
    public double X { get; init; }
    public double Y { get; init; }
    public double W { get; init; }
    public double H { get; init; }
    public string? Flag { get; init; }

    public Rect Bounds => new(X, Y, W, H);
    public bool IsNation => Kind == MapPinKind.Nation;
    public bool ShowGlyph => Kind != MapPinKind.Nation;

    public string Glyph => Kind switch
    {
        MapPinKind.Mountain => "▲",      // ▲
        MapPinKind.Forest => "\U0001F332",    // 🌲
        _ => "◉",                         // ◉
    };

    public Color BgColor => IsNation
        ? Color.FromArgb("#2B335E")
        : Color.FromArgb("#B3161A2E");

    public Color StrokeColor => Tier switch
    {
        MapTier.Nation => Color.FromArgb("#66D9A636"),
        MapTier.Major => Color.FromArgb("#553C4E8C"),
        _ => Colors.Transparent,
    };

    public Color NameColor => Kind switch
    {
        MapPinKind.Nation => Color.FromArgb("#ECC56A"),
        MapPinKind.Mountain => Color.FromArgb("#B9C4E6"),
        MapPinKind.Forest => Color.FromArgb("#A6D8B8"),
        _ => Tier == MapTier.Major ? Color.FromArgb("#E2E8FB") : Color.FromArgb("#C0CAEC"),
    };

    public Color GlyphColor => Kind switch
    {
        MapPinKind.Forest => Color.FromArgb("#5FB683"),
        MapPinKind.Mountain => Color.FromArgb("#9FB0E6"),
        _ => Color.FromArgb("#D9A636"),
    };

    public double NameSize => Tier switch
    {
        MapTier.Nation => 12.5,
        MapTier.Major => 11,
        _ => 9.5,
    };

    public Microsoft.Maui.Controls.FontAttributes NameBold =>
        Tier == MapTier.Minor ? Microsoft.Maui.Controls.FontAttributes.None
                              : Microsoft.Maui.Controls.FontAttributes.Bold;
}

public partial class WorldMapViewModel : ObservableObject
{
    private readonly List<MapPin> _all = new();

    public ObservableCollection<MapPin> Pins { get; } = new();

    public List<string> GameOptions { get; } = new() { "All", "I", "II", "III", "IV", "V" };
    public List<string> DetailOptions { get; } = new() { "Nations", "Towns", "All" };

    [ObservableProperty] private int _gameIndex;     // 0 = All, 1..5 = game
    [ObservableProperty] private int _detailIndex = 0; // zoom-driven (Nations at fit)

    // counter-scale applied to each pin so labels stay a constant on-screen size
    [ObservableProperty] private double _pinScale = 1;

    partial void OnGameIndexChanged(int value) => ApplyFilters();
    partial void OnDetailIndexChanged(int value) => ApplyFilters();

    public WorldMapViewModel()
    {
        BuildPins();
        ApplyFilters();
    }

    private void Add(string id, string name, MapPinKind kind, MapTier tier, double x, double y, double w, int[] games, string? flag = null)
        => _all.Add(new MapPin { Id = id, Name = name, Kind = kind, Tier = tier, X = x, Y = y, W = w, H = 32, Games = games, Flag = flag });

    private void Nation(string slug, string name, double x, double y, double w, int[] games)
        => Add($"faction-{slug}", name, MapPinKind.Nation, MapTier.Nation, x, y, w, games, $"art_{slug}_faction.jpg");

    private void Town(string id, string name, double x, double y, double w, int game, bool major = false)
        => Add(id, name, MapPinKind.Region, major ? MapTier.Major : MapTier.Minor, x, y, w, new[] { game });

    private void Mtn(string id, string name, double x, double y, double w, params int[] games)
        => Add(id, name, MapPinKind.Mountain, MapTier.Minor, x, y, w, games);

    private void Forest(string id, string name, double x, double y, double w, params int[] games)
        => Add(id, name, MapPinKind.Forest, MapTier.Minor, x, y, w, games);

    private void BuildPins()
    {
        // ===================== Nations =====================
        Nation("holy_kingdom_of_harmonia", "Harmonia", 470, 130, 188, new[] { 2, 3 });
        Nation("zexen_federation", "Zexen", 840, 300, 158, new[] { 3 });
        Nation("city_states_of_jowston", "Jowston", 270, 560, 182, new[] { 1, 2, 3 });
        Nation("highland_kingdom", "Highland", 820, 540, 176, new[] { 2 });
        Nation("muse_principality", "Muse", 140, 630, 158, new[] { 2 });
        Nation("greenhill_principality", "Greenhill", 840, 630, 182, new[] { 2 });
        Nation("south_window_principality", "South Window", 66, 730, 196, new[] { 2 });
        Nation("two_river_principality", "Two River", 840, 730, 178, new[] { 2 });
        Nation("matilda_knightdom", "Matilda", 410, 840, 182, new[] { 2 });
        Nation("dunan_monarchy", "Dunan Monarchy", 86, 860, 200, new[] { 2 });
        Nation("dunan_republic", "Dunan Republic", 820, 860, 200, new[] { 2 });
        Nation("tinto_principality", "Tinto Princ.", 100, 970, 172, new[] { 2 });
        Nation("tinto_republic", "Tinto Rep.", 340, 970, 164, new[] { 2 });
        Nation("scarlet_moon_empire", "Scarlet Moon", 350, 1160, 200, new[] { 1 });
        Nation("kooluk_empire", "Kooluk", 56, 1430, 158, new[] { 4 });
        Nation("island_nations_federation", "Island Nations", 400, 1490, 200, new[] { 4 });
        Nation("queendom_of_falena", "Falena", 640, 1720, 172, new[] { 5 });
        Nation("new_armes_kingdom", "New Armes", 330, 1730, 178, new[] { 5 });

        // ===================== Grasslands (Suikoden III) =====================
        Town("region-vinay_del_zexay", "Vinay del Zexay", 1000, 300, 138, 3, major: true);
        Town("region-brass_castle", "Brass Castle", 1010, 360, 120, 3, major: true);
        Town("region-budehuc_castle", "Budehuc Castle", 1010, 430, 130, 3, major: true);
        Town("region-karaya_village", "Karaya Village", 1010, 250, 132, 3);
        Town("region-chisha_village", "Chisha Village", 1010, 490, 130, 3);
        Town("region-caleria", "Caleria", 760, 250, 92, 3);
        Town("region-amur_plain", "Amur Plain", 1010, 200, 110, 3);
        Mtn("region-mt_hei_tou", "Mt. Hei-Tou", 880, 408, 112, 3);
        Mtn("region-mt_senai", "Mt. Senai", 760, 440, 108, 3);
        Forest("region-kuput_forest", "Kuput Forest", 820, 370, 122, 3);

        // ===================== Dunan / Lake (Suikoden II) =====================
        Town("region-muse_city", "Muse City", 410, 678, 110, 2, major: true);
        Town("region-greenhill_city", "Greenhill City", 656, 690, 122, 2, major: true);
        Town("region-south_window_city", "South Window City", 180, 790, 146, 2, major: true);
        Town("region-two_river_city", "Two River City", 656, 790, 122, 2, major: true);
        Town("region-tinto_city", "Tinto City", 200, 1020, 100, 2, major: true);
        Town("region-l_renouille", "L'Renouille", 700, 470, 110, 2, major: true);
        Town("region-north_window", "North Window", 300, 720, 122, 2, major: true);
        Town("region-radat_town", "Radat Town", 560, 640, 110, 2);
        Town("region-kyaro_town", "Kyaro Town", 560, 560, 110, 2);
        Town("region-toto_village", "Toto Village", 430, 590, 112, 2);
        Town("region-tenzan_pass", "Tenzan Pass", 320, 650, 118, 2);
        Mtn("region-banner_mountains", "Banner Mountains", 470, 612, 152, 1, 2);
        Mtn("region-rakutei_mountain", "Rakutei Mtn.", 620, 560, 122, 2);
        Forest("region-banner_forest", "Banner Forest", 510, 720, 128, 2);

        // ===================== Toran (Suikoden I) =====================
        Town("gregminster", "Gregminster", 380, 1218, 130, 1, major: true);
        Town("region-toran_lake_castle", "Toran Lake Castle", 150, 1218, 154, 1, major: true);
        Town("region-lenankamp", "Lenankamp", 260, 1158, 108, 1);
        Town("region-kaku", "Kaku", 490, 1158, 76, 1);
        Town("region-seika", "Seika", 140, 1158, 80, 1);
        Town("region-rockland", "Rockland", 560, 1218, 100, 1);
        Town("region-seek_valley", "Seek Valley", 596, 1158, 112, 1);
        Mtn("region-mt_seifu", "Mt. Seifu", 218, 1252, 112, 1);
        Mtn("region-mt_tigerwolf", "Mt. Tigerwolf", 500, 1292, 130, 1);
        Mtn("region-lorimar_mountains", "Lorimar Mtns.", 150, 1290, 150, 1);
        Forest("region-great_forest", "Great Forest", 440, 1300, 122, 1);
        Forest("region-forest_of_illusion", "Forest of Illusion", 218, 1312, 142, 1);

        // ===================== Kooluk / Islands (Suikoden IV) =====================
        Town("region-imperial_city_of_graska", "Graska", 56, 1392, 92, 4, major: true);
        Town("region-razril", "Razril", 110, 1500, 88, 4, major: true);
        Town("region-middleport", "Middleport", 268, 1540, 114, 4, major: true);
        Town("region-kingdom_of_obel", "Kingdom of Obel", 446, 1490, 138, 4, major: true);
        Town("region-na_nal_island", "Na-Nal Island", 558, 1556, 124, 4);
        Town("region-mountain_mass_island", "Mountain Mass Is.", 568, 1450, 140, 4);
        Town("region-el_eal_fortress", "El-Eal Fortress", 180, 1452, 130, 4);
        Town("region-terana_plains", "Terana Plains", 320, 1602, 116, 4);

        // ===================== Falena (Suikoden V) =====================
        Town("region-sol_falena", "Sol-Falena", 560, 1758, 100, 5, major: true);
        Town("region-stormfist", "Stormfist", 730, 1758, 98, 5, major: true);
        Town("region-rainwall", "Rainwall", 300, 1818, 96, 5, major: true);
        Town("region-lordlake", "Lordlake", 420, 1758, 94, 5);
        Town("region-lunas", "Lunas", 860, 1758, 86, 5);
        Town("region-estrise", "Estrise", 480, 1860, 88, 5);
        Town("region-raftfleet", "Raftfleet", 620, 1862, 96, 5);
        Town("region-lelcar", "Lelcar", 760, 1862, 84, 5);
        Town("region-yashuna_village", "Yashuna Village", 320, 1880, 134, 5);
        Mtn("region-ashtwal_mountains", "Ashtwal Mtns.", 800, 1700, 150, 5);
        Mtn("region-ranro_mountain", "Ranro Mtn.", 900, 1690, 122, 5);
        Forest("region-deep_twilight_forest", "Deep Twilight Forest", 300, 1900, 168, 5);

        // ===================== Additional minor places (revealed when zoomed in) =====================
        // Grasslands (III)
        Town("region-alma_kinan_village", "Alma Kinan Village", 770, 300, 142, 3);
        Town("region-duck_village", "Duck Village", 920, 250, 110, 3);
        Town("region-great_hollow", "Great Hollow", 770, 520, 112, 3);
        Town("region-iksay_village", "Iksay Village", 920, 560, 116, 3);
        Town("region-le_buque", "Le Buque", 1010, 540, 96, 3);
        Town("region-yaza_plain", "Yaza Plain", 1010, 600, 104, 3);
        Forest("region-zexen_forest", "Zexen Forest", 900, 470, 118, 3);

        // Dunan / Lake (II)
        Town("region-banner_pass", "Banner Pass", 430, 558, 108, 2);
        Town("region-sajah_village", "Sajah Village", 560, 500, 116, 2);
        Town("region-crom_village", "Crom Village", 120, 700, 110, 2);
        Town("region-coronet_town", "Coronet Town", 240, 902, 114, 2);
        Town("region-sindar_ruins", "Sindar Ruins", 60, 944, 112, 2);
        Town("region-tinto_mines", "Tinto Mines", 110, 1078, 108, 2);
        Town("region-ryube_village", "Ryube Village", 740, 560, 116, 2);
        Forest("region-greenhill_forest", "Greenhill Forest", 700, 642, 128, 2);

        // Toran (I)
        Town("region-pannu_yakuta_castle", "Pannu Yakuta Castle", 300, 1100, 160, 1);
        Town("region-moravia_castle", "Moravia", 560, 1100, 90, 1);
        Town("region-kalekka", "Kalekka", 340, 1140, 88, 1);
        Town("region-kouan", "Kouan", 440, 1140, 80, 1);
        Town("region-antei", "Antei", 560, 1272, 80, 1);
        Town("region-qlon_temple", "Qlon Temple", 640, 1290, 116, 1);
        Town("region-village_of_the_dwarves", "Village of the Dwarves", 110, 1342, 168, 1);
        Town("region-magician_s_island", "Magician's Island", 50, 1180, 138, 1);

        // Kooluk / Islands (IV)
        Town("region-deserted_island", "Deserted Island", 340, 1450, 130, 4);
        Town("region-iluya_island", "Iluya Island", 620, 1510, 118, 4);
        Town("region-donut_island", "Donut Island", 430, 1602, 116, 4);
        Town("region-nay_island", "Nay Island", 530, 1610, 104, 4);
        Town("region-canal_town_merseto", "Canal Town Merseto", 150, 1582, 156, 4);

        // Falena (V)
        Town("region-haud_village", "Haud Village", 360, 1700, 116, 5);
        Town("region-beaver_lodge", "Beaver Lodge", 470, 1700, 114, 5);
        Town("region-doraat_fortress", "Doraat", 580, 1700, 84, 5);
        Town("region-sauronix_castle", "Sauronix Castle", 700, 1700, 130, 5);
        Town("region-sable", "Sable", 940, 1810, 86, 5);
        Town("region-hershville_naval_base", "Hershville Naval Base", 980, 1862, 168, 5);
    }

    private void ApplyFilters()
    {
        int game = GameIndex; // 0 = All
        MapDetail detail = (MapDetail)DetailIndex;

        bool TierOk(MapTier t) => detail switch
        {
            MapDetail.Nations => t == MapTier.Nation,
            MapDetail.Towns => t == MapTier.Nation || t == MapTier.Major,
            _ => true,
        };
        bool GameOk(MapPin p) => game == 0 || System.Array.IndexOf(p.Games, game) >= 0;

        Pins.Clear();
        foreach (var p in _all)
            if (TierOk(p.Tier) && GameOk(p))
                Pins.Add(p);
    }

    // Set by the page while a pinch/zoom is in progress so finger contact on a
    // pin doesn't accidentally navigate to its entry.
    public bool InteractionLocked { get; set; }

    [RelayCommand]
    private async Task Open(string? entryId)
    {
        if (InteractionLocked) return;
        if (string.IsNullOrEmpty(entryId)) return;
        await Shell.Current.GoToAsync($"{nameof(EntryDetailPage)}?id={entryId}");
    }
}
