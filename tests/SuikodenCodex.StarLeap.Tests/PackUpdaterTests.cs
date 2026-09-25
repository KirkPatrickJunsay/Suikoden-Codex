using SuikodenCodex.StarLeap;

namespace SuikodenCodex.StarLeap.Tests;

public class PackUpdaterTests
{
    private static readonly Uri Base = new("https://example.test/pack/");

    private static (PackUpdater updater, FakeServer server, string cache) Create(Dictionary<string, byte[]> remote)
    {
        var server = new FakeServer(remote);
        var cache = TestPack.NewTempDir();
        return (new PackUpdater(new HttpClient(server), Base, cache), server, cache);
    }

    [Fact]
    public async Task Up_to_date_when_remote_version_is_not_newer()
    {
        var (updater, _, _) = Create(TestPack.Files(version: 5));
        var current = TestPack.ManifestOf(TestPack.Files(version: 5));

        var result = await updater.CheckAndUpdateAsync(current, null);

        Assert.Equal(UpdateOutcome.UpToDate, result.Outcome);
    }

    [Fact]
    public async Task Requires_app_update_for_newer_schema()
    {
        var (updater, _, _) = Create(TestPack.Files(version: 9, schema: 2));

        var result = await updater.CheckAndUpdateAsync(null, null);

        Assert.Equal(UpdateOutcome.AppUpdateRequired, result.Outcome);
    }

    [Fact]
    public async Task Downloads_and_installs_a_newer_pack()
    {
        var (updater, _, _) = Create(TestPack.Files(version: 2));

        var result = await updater.CheckAndUpdateAsync(null, null);

        Assert.Equal(UpdateOutcome.Updated, result.Outcome);
        Assert.Equal(2, result.Pack!.Manifest.Version);
        var reloaded = await PackLoader.LoadAsync(new DirectoryPackFiles(updater.CurrentDirectory));
        Assert.Equal(2, reloaded.Manifest.Version);
    }

    [Fact]
    public async Task Downloads_only_files_whose_checksum_changed()
    {
        var old = TestPack.Files(version: 1);
        var oldDir = TestPack.WriteToTempDir(old);
        var (updater, server, _) = Create(TestPack.Files(version: 2, portrait: new byte[] { 9, 9, 9 }));

        var result = await updater.CheckAndUpdateAsync(TestPack.ManifestOf(old), new DirectoryPackFiles(oldDir));

        Assert.Equal(UpdateOutcome.Updated, result.Outcome);
        Assert.Contains("portraits/flik-blue-thunder.png", server.Requested);
        Assert.DoesNotContain("units.json", server.Requested);
    }

    [Fact]
    public async Task Checksum_mismatch_fails_and_keeps_current_pack()
    {
        var first = Create(TestPack.Files(version: 1));
        await first.updater.CheckAndUpdateAsync(null, null);
        var remote = TestPack.Files(version: 2);
        var server = new FakeServer(remote);
        server.Overrides["units.json"] = new byte[] { 0 };
        var updater = new PackUpdater(new HttpClient(server), Base, first.cache);

        var result = await updater.CheckAndUpdateAsync(TestPack.ManifestOf(TestPack.Files(version: 1)), null);

        Assert.Equal(UpdateOutcome.Failed, result.Outcome);
        var kept = await PackLoader.LoadAsync(new DirectoryPackFiles(updater.CurrentDirectory));
        Assert.Equal(1, kept.Manifest.Version);
        Assert.False(Directory.Exists(Path.Combine(first.cache, "staging")));
    }

    [Fact]
    public async Task Network_failure_returns_failed_without_throwing()
    {
        var (updater, server, _) = Create(TestPack.Files(version: 2));
        server.Offline = true;

        var result = await updater.CheckAndUpdateAsync(null, null);

        Assert.Equal(UpdateOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task Unsafe_manifest_path_is_rejected()
    {
        var remote = TestPack.Files(version: 2);
        var manifest = TestPack.ManifestOf(remote);
        manifest.Files.Add(new SlFileEntry { Path = "../escape.txt", Sha256 = "00", Bytes = 1 });
        remote["manifest.json"] = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(manifest, PackFormat.Json);
        var (updater, _, cache) = Create(remote);

        var result = await updater.CheckAndUpdateAsync(null, null);

        Assert.Equal(UpdateOutcome.Failed, result.Outcome);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(cache)!, "escape.txt")));
    }

    [Fact]
    public void Recovers_previous_pack_after_interrupted_swap()
    {
        var cache = TestPack.NewTempDir();
        var previous = Path.Combine(cache, "previous");
        Directory.CreateDirectory(previous);
        File.WriteAllText(Path.Combine(previous, "marker.txt"), "ok");
        Directory.CreateDirectory(Path.Combine(cache, "staging"));
        var updater = new PackUpdater(new HttpClient(new FakeServer(new())), Base, cache);

        updater.RecoverInterruptedSwap();

        Assert.True(File.Exists(Path.Combine(updater.CurrentDirectory, "marker.txt")));
        Assert.False(Directory.Exists(previous));
        Assert.False(Directory.Exists(Path.Combine(cache, "staging")));
    }
}
