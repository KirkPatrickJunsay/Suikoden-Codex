using System.Text.Json;
using SuikodenCodex.Models;

namespace SuikodenCodex.Services;

/// <summary>Loads bundled walkthrough guides (one JSON file per game).</summary>
public class WalkthroughData
{
    static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };
    readonly Dictionary<int, WalkGame> _cache = new();

    /// <summary>Loads the walkthrough for the given game (1 = Suikoden I … 5 = Suikoden V).
    /// Returns an empty WalkGame (no chapters) if the guide isn't bundled yet.</summary>
    public async Task<WalkGame> GetAsync(int game)
    {
        if (_cache.TryGetValue(game, out var cached)) return cached;
        WalkGame result;
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync($"walkthrough_suikoden{game}.json");
            using var reader = new StreamReader(stream);
            result = JsonSerializer.Deserialize<WalkGame>(await reader.ReadToEndAsync(), Opts) ?? new();
        }
        catch { result = new(); }
        _cache[game] = result;
        return result;
    }

    // kept for compatibility
    public Task<WalkGame> GetSuikoden1Async() => GetAsync(1);
}
