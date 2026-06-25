using System.Text.Json;
using SuikodenCodex.Models;

namespace SuikodenCodex.Services;

/// <summary>Loads and serves the Genso Suikoden Card Stories dataset (bundled cards.json).</summary>
public class CardData
{
    private List<CardEntry> _cards = new();
    private bool _loaded;

    public IReadOnlyList<CardEntry> Cards => _cards;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("cards.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            _cards = JsonSerializer.Deserialize<List<CardEntry>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch
        {
            _cards = new();
        }
        _loaded = true;
    }

    public CardEntry? GetById(string id) => _cards.FirstOrDefault(c => c.Id == id);

    /// <summary>Distinct booster packs, in first-seen order.</summary>
    public IReadOnlyList<string> Packs =>
        _cards.Select(c => c.Pack).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
}
