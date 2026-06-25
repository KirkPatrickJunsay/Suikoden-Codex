using System.Text.Json;
using SuikodenCodex.Models;

namespace SuikodenCodex.Services;

/// <summary>Loads and persists the player's custom Card Duel decks to a JSON file in app data.</summary>
public class DeckStore
{
    static string FilePath => Path.Combine(FileSystem.AppDataDirectory, "custom_decks.json");

    readonly List<SavedDeck> _decks = new();
    bool _loaded;

    public IReadOnlyList<SavedDeck> Decks => _decks;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        try
        {
            if (File.Exists(FilePath))
            {
                var json = await File.ReadAllTextAsync(FilePath);
                var list = JsonSerializer.Deserialize<List<SavedDeck>>(json);
                if (list is not null) { _decks.Clear(); _decks.AddRange(list); }
            }
        }
        catch { /* start empty on any read/parse error */ }
        _loaded = true;
    }

    public SavedDeck? Get(string id) => _decks.FirstOrDefault(d => d.Id == id);

    public async Task UpsertAsync(SavedDeck deck)
    {
        var i = _decks.FindIndex(d => d.Id == deck.Id);
        if (i >= 0) _decks[i] = deck; else _decks.Add(deck);
        await SaveAsync();
    }

    public async Task DeleteAsync(string id)
    {
        _decks.RemoveAll(d => d.Id == id);
        await SaveAsync();
    }

    async Task SaveAsync()
    {
        try { await File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(_decks)); }
        catch { /* ignore write failures */ }
    }
}
