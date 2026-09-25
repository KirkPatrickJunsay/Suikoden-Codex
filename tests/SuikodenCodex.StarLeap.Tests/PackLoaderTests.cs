using System.Text;
using SuikodenCodex.StarLeap;

namespace SuikodenCodex.StarLeap.Tests;

public class PackLoaderTests
{
    [Fact]
    public async Task Loads_manifest_and_units()
    {
        var dir = TestPack.WriteToTempDir(TestPack.Files(version: 3));

        var pack = await PackLoader.LoadAsync(new DirectoryPackFiles(dir));

        Assert.Equal(3, pack.Manifest.Version);
        var unit = Assert.Single(pack.Units);
        Assert.Equal("Flik — Blue Thunder", unit.DisplayName);
        Assert.True(unit.IsClassicVersion);
    }

    [Fact]
    public async Task Rejects_unsupported_schema()
    {
        var dir = TestPack.WriteToTempDir(TestPack.Files(version: 1, schema: 2));

        var ex = await Assert.ThrowsAsync<PackException>(() => PackLoader.LoadAsync(new DirectoryPackFiles(dir)));
        Assert.Contains("schema", ex.Message);
    }

    [Fact]
    public async Task Rejects_missing_units_file()
    {
        var files = TestPack.Files(version: 1);
        files.Remove("units.json");
        var dir = TestPack.WriteToTempDir(files);

        var ex = await Assert.ThrowsAsync<PackException>(() => PackLoader.LoadAsync(new DirectoryPackFiles(dir)));
        Assert.Contains("units.json", ex.Message);
    }

    [Fact]
    public async Task Rejects_malformed_json()
    {
        var files = TestPack.Files(version: 1);
        files["units.json"] = Encoding.UTF8.GetBytes("[{\"id\":");
        var dir = TestPack.WriteToTempDir(files);

        await Assert.ThrowsAsync<PackException>(() => PackLoader.LoadAsync(new DirectoryPackFiles(dir)));
    }

    [Fact]
    public async Task Wraps_io_failures_in_pack_exception()
    {
        var ex = await Assert.ThrowsAsync<PackException>(() => PackLoader.LoadAsync(new ThrowingPackFiles()));
        Assert.Contains("manifest.json", ex.Message);
    }

    private sealed class ThrowingPackFiles : IPackFiles
    {
        public Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default) =>
            throw new IOException("disk error");
    }
}
