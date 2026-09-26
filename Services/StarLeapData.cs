using SuikodenCodex.Models;
using SuikodenCodex.StarLeap;

namespace SuikodenCodex.Services;

public sealed class StarLeapData
{
    public static readonly Uri PackUri = new("https://kirkpatrickjunsay.github.io/Suikoden-Codex/starleap/");
    public const string Credit = "Source: Gensopedia STAR LEAP (CC BY-NC-SA 4.0)";
    private const string LastCheckKey = "sl_last_check";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly CodexData _codex;
    private readonly PackUpdater _updater;
    private readonly UpdatePolicy _policy = new(() => DateTimeOffset.UtcNow, TimeSpan.FromHours(24));
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IPackFiles _files = new BundledPackFiles();
    private Dictionary<string, SlUnit> _byId = new();
    private List<ClassicCharacter>? _classics;

    public StarLeapData(CodexData codex)
    {
        _codex = codex;
        _updater = new PackUpdater(Http, PackUri, Path.Combine(FileSystem.AppDataDirectory, "starleap"));
    }

    public SlManifest? Manifest { get; private set; }
    public IReadOnlyList<SlUnit> Units { get; private set; } = Array.Empty<SlUnit>();
    public string Status { get; private set; } = "";
    public event EventHandler? Changed;

    public async Task EnsureLoadedAsync()
    {
        if (Manifest is not null)
            return;
        await _gate.WaitAsync();
        try
        {
            if (Manifest is not null)
                return;
            try
            {
                await Task.Run(_updater.RecoverInterruptedSwap);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
            var bundledFiles = new BundledPackFiles();
            var bundled = await TryLoadAsync(bundledFiles);
            var cachedFiles = new DirectoryPackFiles(_updater.CurrentDirectory);
            var cached = await TryLoadAsync(cachedFiles);
            if (cached is not null && (bundled is null || cached.Manifest.Version > bundled.Manifest.Version))
            {
                _files = cachedFiles;
                Apply(cached);
            }
            else if (bundled is not null)
            {
                _files = bundledFiles;
                Apply(bundled);
            }
            else
            {
                SetStatus("Star Leap data is unavailable");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UpdateResult> CheckForUpdatesAsync(bool force)
    {
        await EnsureLoadedAsync();
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            SetStatus("Offline — showing saved data");
            return new UpdateResult(UpdateOutcome.Failed, "offline");
        }
        if (!force && !_policy.IsDue(LastCheck))
            return new UpdateResult(UpdateOutcome.UpToDate);

        SetStatus("Checking for updates…");
        var result = await Task.Run(() => _updater.CheckAndUpdateAsync(Manifest, _files));
        switch (result.Outcome)
        {
            case UpdateOutcome.Updated:
                _files = new DirectoryPackFiles(_updater.CurrentDirectory);
                Apply(result.Pack!);
                LastCheck = DateTimeOffset.UtcNow;
                SetStatus("Updated to the latest data");
                break;
            case UpdateOutcome.UpToDate:
                LastCheck = DateTimeOffset.UtcNow;
                SetStatus("Up to date");
                break;
            case UpdateOutcome.AppUpdateRequired:
                SetStatus("Update the app for the latest Star Leap data");
                break;
            default:
                SetStatus("Couldn't check for updates");
                break;
        }
        return result;
    }

    public SlUnit? GetUnit(string id) => _byId.GetValueOrDefault(id);

    public IReadOnlyList<SlUnit> OtherVersionsOf(SlUnit unit) =>
        Units.Where(u => u.Id != unit.Id &&
                         string.Equals(u.BasedOn ?? u.Name, unit.BasedOn ?? unit.Name, StringComparison.OrdinalIgnoreCase))
             .ToList();

    public async Task<string?> ClassicEntryIdAsync(SlUnit unit)
    {
        await _codex.EnsureLoadedAsync();
        _classics ??= _codex.Entries
            .Where(e => e.Category == EntryCategory.Character)
            .Select(e => new ClassicCharacter(e.Id, e.Name, e.Game))
            .ToList();
        return ClassicMatcher.Match(unit, _classics);
    }

    public ImageSource Portrait(SlUnit unit)
    {
        var files = _files;
        var path = unit.Portrait;
        return ImageSource.FromStream(async ct => await files.OpenReadAsync(path, ct) ?? Stream.Null);
    }

    private static async Task<SlPack?> TryLoadAsync(IPackFiles files)
    {
        try
        {
            return await PackLoader.LoadAsync(files);
        }
        catch (PackException)
        {
            return null;
        }
    }

    private void Apply(SlPack pack)
    {
        Manifest = pack.Manifest;
        Units = pack.Units;
        var byId = new Dictionary<string, SlUnit>();
        foreach (var unit in pack.Units)
            byId.TryAdd(unit.Id, unit);
        _byId = byId;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SetStatus(string status)
    {
        Status = status;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static DateTimeOffset? LastCheck
    {
        get
        {
            var ticks = Preferences.Get(LastCheckKey, 0L);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
        set => Preferences.Set(LastCheckKey, value?.UtcTicks ?? 0L);
    }

    private sealed class BundledPackFiles : IPackFiles
    {
        public async Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default)
        {
            try
            {
                return await FileSystem.OpenAppPackageFileAsync("starleap/" + path);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                return null;
            }
        }
    }
}
