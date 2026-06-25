using System.Text.Json;
using SuikodenCodex.Models;

namespace SuikodenCodex.Services;

/// <summary>
/// Persists user state (favorites, recruited stars, recently viewed) via Preferences.
/// Kept deliberately simple — no database needed for this scope.
/// </summary>
public class UserState
{
    private const string FavoritesKey = "favorites";
    private const string RecruitedKey = "recruited_stars";
    private const string RecentKey = "recent";
    private const int RecentLimit = 30;

    private const string SpoilerKey = "spoiler_safe";

    private HashSet<string> _favorites;
    private HashSet<string> _recruited;   // keys: "{game}#{num}"
    private List<string> _recent;

    /// <summary>When true, entry descriptions are hidden behind a tap on the detail page.</summary>
    public bool SpoilerSafe
    {
        get => Preferences.Get(SpoilerKey, false);
        set => Preferences.Set(SpoilerKey, value);
    }

    public UserState()
    {
        _favorites = Load<HashSet<string>>(FavoritesKey) ?? new();
        _recruited = Load<HashSet<string>>(RecruitedKey) ?? new();
        _recent = Load<List<string>>(RecentKey) ?? new();
    }

    private static string RecruitKey(string game, int num) => $"{game}#{num}";

    // ---- Favorites ----
    public bool IsFavorite(string entryId) => _favorites.Contains(entryId);

    public bool ToggleFavorite(string entryId)
    {
        bool nowFav;
        if (!_favorites.Remove(entryId))
        {
            _favorites.Add(entryId);
            nowFav = true;
        }
        else nowFav = false;
        Save(FavoritesKey, _favorites);
        return nowFav;
    }

    public IReadOnlyCollection<string> Favorites => _favorites;

    // ---- Per-game recruitment tracking ----
    public bool IsRecruited(string game, int num) => _recruited.Contains(RecruitKey(game, num));

    public bool ToggleRecruited(string game, int num)
    {
        var key = RecruitKey(game, num);
        bool nowRecruited;
        if (!_recruited.Remove(key))
        {
            _recruited.Add(key);
            nowRecruited = true;
        }
        else nowRecruited = false;
        Save(RecruitedKey, _recruited);
        RefreshWidget();
        return nowRecruited;
    }

    public int RecruitedCount(string game) =>
        _recruited.Count(k => k.StartsWith(game + "#", StringComparison.Ordinal));

    public void ResetRecruited(string game)
    {
        _recruited.RemoveWhere(k => k.StartsWith(game + "#", StringComparison.Ordinal));
        Save(RecruitedKey, _recruited);
        RefreshWidget();
    }

    private static void RefreshWidget()
    {
#if ANDROID
        SuikodenCodex.Platforms.Android.WidgetUpdater.Refresh();
#endif
    }

    // ---- Recently viewed ----
    public void PushRecent(string entryId)
    {
        _recent.Remove(entryId);
        _recent.Insert(0, entryId);
        if (_recent.Count > RecentLimit)
            _recent.RemoveRange(RecentLimit, _recent.Count - RecentLimit);
        Save(RecentKey, _recent);
    }

    public IReadOnlyList<string> Recent => _recent;

    public void ClearRecent()
    {
        _recent.Clear();
        Save(RecentKey, _recent);
    }

    // ---- Backup / restore ----
    /// <summary>Serialize all user progress to a portable JSON backup.</summary>
    public string ExportJson()
    {
        var backup = new BackupData
        {
            ExportedAt = DateTime.UtcNow.ToString("o"),
            Favorites = _favorites.ToList(),
            Recruited = _recruited.ToList(),
            Recent = _recent.ToList(),
            SpoilerSafe = SpoilerSafe,
        };
        return JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Replace all progress from a backup file. Returns (success, message).</summary>
    public (bool ok, string message) ImportJson(string json)
    {
        BackupData? b;
        try { b = JsonSerializer.Deserialize<BackupData>(json); }
        catch { return (false, "That file couldn't be read as a backup."); }

        if (b is null || !string.Equals(b.App, "SuikodenCodex", StringComparison.Ordinal))
            return (false, "That file isn't a Suikoden Codex backup.");

        _favorites = new HashSet<string>(b.Favorites ?? new());
        _recruited = new HashSet<string>(b.Recruited ?? new());
        _recent = (b.Recent ?? new()).Take(RecentLimit).ToList();
        Save(FavoritesKey, _favorites);
        Save(RecruitedKey, _recruited);
        Save(RecentKey, _recent);
        SpoilerSafe = b.SpoilerSafe;
        RefreshWidget();

        return (true, $"Restored {_favorites.Count} favorites and {_recruited.Count} recruited stars.");
    }

    // ---- helpers ----
    private static T? Load<T>(string key)
    {
        var raw = Preferences.Get(key, "");
        if (string.IsNullOrEmpty(raw)) return default;
        try { return JsonSerializer.Deserialize<T>(raw); }
        catch { return default; }
    }

    private static void Save<T>(string key, T value) =>
        Preferences.Set(key, JsonSerializer.Serialize(value));
}
