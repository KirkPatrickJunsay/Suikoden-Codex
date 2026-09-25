using System.Text.Json;

namespace SuikodenCodex.StarLeap;

public static class PackLoader
{
    public static async Task<SlPack> LoadAsync(IPackFiles files, CancellationToken ct = default)
    {
        var manifest = await ReadAsync<SlManifest>(files, PackFormat.ManifestFile, ct);
        if (manifest.Schema < 1 || manifest.Schema > PackFormat.SupportedSchema)
            throw new PackException($"Unsupported pack schema {manifest.Schema}");
        var units = await ReadAsync<List<SlUnit>>(files, PackFormat.UnitsFile, ct);
        return new SlPack(manifest, units);
    }

    private static async Task<T> ReadAsync<T>(IPackFiles files, string path, CancellationToken ct)
    {
        await using var stream = await files.OpenReadAsync(path, ct)
            ?? throw new PackException($"Missing {path}");
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(stream, PackFormat.Json, ct)
                ?? throw new PackException($"Empty {path}");
        }
        catch (JsonException e)
        {
            throw new PackException($"Invalid {path}: {e.Message}", e);
        }
    }
}
