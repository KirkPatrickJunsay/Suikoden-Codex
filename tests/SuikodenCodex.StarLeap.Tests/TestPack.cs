using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SuikodenCodex.StarLeap;

namespace SuikodenCodex.StarLeap.Tests;

public static class TestPack
{
    public const string UnitsJson = """
        [{"id":"flik-blue-thunder","name":"Flik","nameJp":"フリック","title":"Blue Thunder","rarity":"Guest",
          "role":"Attack","elements":["Lightning"],"weapons":["Sword"],"origin":"Suikoden","basedOn":"Flik",
          "released":"2026-08-07","portrait":"portraits/flik-blue-thunder.png","kit":[]}]
        """;

    public static Dictionary<string, byte[]> Files(int version, int schema = 1, string unitsJson = UnitsJson, byte[]? portrait = null)
    {
        var files = new Dictionary<string, byte[]>
        {
            ["units.json"] = Encoding.UTF8.GetBytes(unitsJson),
            ["portraits/flik-blue-thunder.png"] = portrait ?? new byte[] { 1, 2, 3 },
        };
        var manifest = new SlManifest
        {
            Schema = schema,
            Version = version,
            GeneratedAt = new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero),
            Source = "https://starleap.gensopedia.org",
            License = "CC BY-NC-SA 4.0",
            Attribution = "Adapted from Gensopedia STAR LEAP",
            Files = files.Select(f => new SlFileEntry { Path = f.Key, Sha256 = Sha(f.Value), Bytes = f.Value.Length }).ToList(),
        };
        files["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(manifest, PackFormat.Json);
        return files;
    }

    public static SlManifest ManifestOf(Dictionary<string, byte[]> files) =>
        JsonSerializer.Deserialize<SlManifest>(files["manifest.json"], PackFormat.Json)!;

    public static string Sha(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    public static string WriteToTempDir(Dictionary<string, byte[]> files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "slpack-" + Guid.NewGuid().ToString("N"));
        foreach (var (path, bytes) in files)
        {
            var full = Path.Combine(dir, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, bytes);
        }
        return dir;
    }

    public static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "slcache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
