using System.Text.Json;
using SuikodenCodex.Models;

namespace SuikodenCodex.Services;

/// <summary>
/// Loads the bundled, translated dataset and exposes query helpers.
/// Entry/star content is read-only; user state lives in <see cref="UserState"/>.
/// </summary>
public class CodexData
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private List<CodexEntry> _entries = new();
    private Dictionary<string, List<RecruitChar>> _recruits = new();
    private Dictionary<string, CodexEntry> _byId = new();
    private bool _loaded;

    public IReadOnlyList<CodexEntry> Entries => _entries;

    /// <summary>Games shown in the Recruitment tab. Only those with data are selectable.</summary>
    public static readonly string[] RecruitGames =
        { "Suikoden I", "Suikoden II", "Suikoden III", "Suikoden IV", "Suikoden V" };

    public bool HasRecruits(string game) => _recruits.ContainsKey(game) && _recruits[game].Count > 0;

    public IReadOnlyList<RecruitChar> GetRecruits(string game) =>
        _recruits.TryGetValue(game, out var l) ? l : new();

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;

        _entries = await LoadAssetAsync<List<CodexEntry>>("entries.json") ?? new();
        _recruits = await LoadAssetAsync<Dictionary<string, List<RecruitChar>>>("recruitment.json") ?? new();

        _entries = _entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        _byId = _entries.GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var l in _recruits.Values)
            l.Sort((a, b) => a.Num.CompareTo(b.Num));
        _loaded = true;
    }

    private static async Task<T?> LoadAssetAsync<T>(string fileName)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOpts);
    }

    public CodexEntry? GetById(string id) =>
        _byId.TryGetValue(id, out var e) ? e : null;

    public IEnumerable<CodexEntry> Resolve(IEnumerable<string> ids) =>
        ids.Select(GetById).Where(e => e is not null)!.Cast<CodexEntry>();

    /// <summary>
    /// Cross-reference graph: explicit crossRefs first, then other entries whose name
    /// is mentioned (whole-word) in this entry's description.
    /// </summary>
    public IReadOnlyList<CodexEntry> RelatedTo(CodexEntry entry, int max = 12)
    {
        var result = new List<CodexEntry>();
        var seen = new HashSet<string> { entry.Id };

        foreach (var r in Resolve(entry.CrossRefs))
            if (seen.Add(r.Id)) result.Add(r);

        var desc = entry.Description ?? "";
        if (desc.Length > 0)
        {
            foreach (var e in _entries)
            {
                if (result.Count >= max) break;
                if (e.Name.Length < 3 || seen.Contains(e.Id)) continue;
                if (System.Text.RegularExpressions.Regex.IsMatch(
                        desc, $@"\b{System.Text.RegularExpressions.Regex.Escape(e.Name)}\b"))
                {
                    seen.Add(e.Id);
                    result.Add(e);
                }
            }
        }
        return result;
    }

    /// <summary>Roman-numeral game tokens (e.g. "I","II") an entry belongs to, parsed from its Game string.</summary>
    public static IReadOnlyList<string> GameTokens(string? game)
    {
        if (string.IsNullOrEmpty(game)) return Array.Empty<string>();
        return System.Text.RegularExpressions.Regex
            .Matches(game, @"\b(III|II|IV|I|V)\b")
            .Select(m => m.Value)
            .Distinct()
            .ToList();
    }

    /// <summary>Game tokens that have at least one entry, in I–V order, with counts.</summary>
    public IReadOnlyList<string> AvailableGameTokens()
    {
        var order = new[] { "I", "II", "III", "IV", "V" };
        var present = _entries.SelectMany(e => GameTokens(e.Game)).ToHashSet();
        return order.Where(present.Contains).ToList();
    }

    /// <summary>Free-text search across name, original name, reading, subtype and body, with optional category + game filters.</summary>
    public IEnumerable<CodexEntry> Search(string? query, EntryCategory? category, string? gameToken = null)
    {
        IEnumerable<CodexEntry> result = _entries;

        if (category.HasValue)
            result = result.Where(e => e.Category == category.Value);

        if (!string.IsNullOrEmpty(gameToken))
            result = result.Where(e => GameTokens(e.Game).Contains(gameToken));

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            result = result.Where(e =>
                Contains(e.Name, q) ||
                Contains(e.OriginalName, q) ||
                Contains(e.Reading, q) ||
                Contains(e.Subtype, q) ||
                Contains(e.Game, q) ||
                Contains(e.Description, q));
        }

        return result;
    }

    private static bool Contains(string? haystack, string needle) =>
        !string.IsNullOrEmpty(haystack) &&
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    public IReadOnlyDictionary<EntryCategory, int> CountByCategory() =>
        _entries.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.Count());

    private readonly Random _rng = new();
    public CodexEntry? Random() =>
        _entries.Count == 0 ? null : _entries[_rng.Next(_entries.Count)];
}
