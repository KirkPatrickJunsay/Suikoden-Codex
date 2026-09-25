using System.Security.Cryptography;
using System.Text.Json;

namespace SuikodenCodex.StarLeap;

public enum UpdateOutcome
{
    UpToDate,
    Updated,
    AppUpdateRequired,
    Failed,
}

public sealed record UpdateResult(UpdateOutcome Outcome, string? Reason = null, SlPack? Pack = null);

public sealed class PackUpdater
{
    private readonly HttpClient _http;
    private readonly Uri _base;
    private readonly string _root;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PackUpdater(HttpClient http, Uri baseUri, string cacheRoot)
    {
        _http = http;
        _base = baseUri.AbsoluteUri.EndsWith('/') ? baseUri : new Uri(baseUri.AbsoluteUri + "/");
        _root = cacheRoot;
    }

    public string CurrentDirectory => Path.Combine(_root, "current");
    private string StagingDirectory => Path.Combine(_root, "staging");
    private string PreviousDirectory => Path.Combine(_root, "previous");

    public void RecoverInterruptedSwap()
    {
        if (!Directory.Exists(CurrentDirectory) && Directory.Exists(PreviousDirectory))
            Directory.Move(PreviousDirectory, CurrentDirectory);
        DeleteIfExists(StagingDirectory);
        DeleteIfExists(PreviousDirectory);
    }

    public async Task<UpdateResult> CheckAndUpdateAsync(SlManifest? currentManifest, IPackFiles? currentFiles, CancellationToken ct = default)
    {
        try
        {
            await _gate.WaitAsync(ct);
        }
        catch (OperationCanceledException e)
        {
            return new UpdateResult(UpdateOutcome.Failed, e.Message);
        }
        try
        {
            return await CheckAndUpdateCoreAsync(currentManifest, currentFiles, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<UpdateResult> CheckAndUpdateCoreAsync(SlManifest? currentManifest, IPackFiles? currentFiles, CancellationToken ct = default)
    {
        try
        {
            var manifestBytes = await _http.GetByteArrayAsync(new Uri(_base, PackFormat.ManifestFile), ct);
            var remote = JsonSerializer.Deserialize<SlManifest>(manifestBytes, PackFormat.Json)
                ?? throw new PackException("Empty manifest");
            if (remote.Schema > PackFormat.SupportedSchema)
                return new UpdateResult(UpdateOutcome.AppUpdateRequired);
            if (currentManifest is not null && remote.Version <= currentManifest.Version)
                return new UpdateResult(UpdateOutcome.UpToDate);

            DeleteIfExists(StagingDirectory);
            Directory.CreateDirectory(StagingDirectory);
            var currentHashes = currentManifest?.Files.ToDictionary(f => f.Path, f => f.Sha256, StringComparer.Ordinal)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var entry in remote.Files)
            {
                var target = StagedPath(entry.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var reused = currentFiles is not null
                    && currentHashes.TryGetValue(entry.Path, out var hash)
                    && string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase)
                    && await CopyFromCurrentAsync(currentFiles, entry.Path, target, ct)
                    && await HashMatchesAsync(target, entry.Sha256, ct);
                if (!reused)
                {
                    await DownloadAsync(entry.Path, target, ct);
                    if (!await HashMatchesAsync(target, entry.Sha256, ct))
                        return Fail($"Checksum mismatch for {entry.Path}");
                }
            }

            await File.WriteAllBytesAsync(Path.Combine(StagingDirectory, PackFormat.ManifestFile), manifestBytes, ct);
            var pack = await PackLoader.LoadAsync(new DirectoryPackFiles(StagingDirectory), ct);
            Swap();
            return new UpdateResult(UpdateOutcome.Updated, Pack: pack);
        }
        catch (Exception e)
        {
            return Fail(e.Message);
        }
    }

    private UpdateResult Fail(string reason)
    {
        TryDelete(StagingDirectory);
        return new UpdateResult(UpdateOutcome.Failed, reason);
    }

    private string StagedPath(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Split('/', '\\').Any(s => s == ".."))
            throw new PackException($"Unsafe path in manifest: {relative}");
        return Path.Combine(StagingDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private static async Task<bool> CopyFromCurrentAsync(IPackFiles files, string path, string target, CancellationToken ct)
    {
        await using var source = await files.OpenReadAsync(path, ct);
        if (source is null)
            return false;
        await using var destination = File.Create(target);
        await source.CopyToAsync(destination, ct);
        return true;
    }

    private async Task DownloadAsync(string path, string target, CancellationToken ct)
    {
        var escaped = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
        await using var source = await _http.GetStreamAsync(new Uri(_base, escaped), ct);
        await using var destination = File.Create(target);
        await source.CopyToAsync(destination, ct);
    }

    private static async Task<string> Sha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
    }

    private static async Task<bool> HashMatchesAsync(string path, string expected, CancellationToken ct) =>
        string.Equals(await Sha256Async(path, ct), expected, StringComparison.OrdinalIgnoreCase);

    private void Swap()
    {
        DeleteIfExists(PreviousDirectory);
        if (Directory.Exists(CurrentDirectory))
            Directory.Move(CurrentDirectory, PreviousDirectory);
        try
        {
            Directory.Move(StagingDirectory, CurrentDirectory);
        }
        catch
        {
            if (!Directory.Exists(CurrentDirectory) && Directory.Exists(PreviousDirectory))
                Directory.Move(PreviousDirectory, CurrentDirectory);
            throw;
        }
        DeleteIfExists(PreviousDirectory);
    }

    private static void DeleteIfExists(string dir)
    {
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    private static void TryDelete(string dir)
    {
        try { DeleteIfExists(dir); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
