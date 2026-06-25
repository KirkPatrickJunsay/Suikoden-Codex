using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;

namespace SuikodenCodex.ViewModels;

public partial class MoreViewModel : ObservableObject
{
    private readonly CodexData _data;
    private readonly UserState _state;
    private bool _initialized;

    [ObservableProperty]
    private int _favoriteCount;

    [ObservableProperty]
    private int _recentCount;

    [ObservableProperty]
    private int _entryCount;

    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private bool _isSpoilerSafe;

    public MoreViewModel(CodexData data, UserState state)
    {
        _data = data;
        _state = state;
        _isDarkTheme = Application.Current?.UserAppTheme == AppTheme.Dark
                       || (Application.Current?.UserAppTheme == AppTheme.Unspecified
                           && Application.Current?.RequestedTheme == AppTheme.Dark);
        _isSpoilerSafe = state.SpoilerSafe;
    }

    partial void OnIsSpoilerSafeChanged(bool value) => _state.SpoilerSafe = value;

    public async Task InitializeAsync()
    {
        await _data.EnsureLoadedAsync();
        _initialized = true;
        RefreshCounts();
    }

    public void RefreshCounts()
    {
        if (!_initialized) return;
        FavoriteCount = _state.Favorites.Count;
        RecentCount = _state.Recent.Count;
        EntryCount = _data.Entries.Count;
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (Application.Current is not null)
            Application.Current.UserAppTheme = value ? AppTheme.Dark : AppTheme.Light;
        Preferences.Set("app_theme", value ? "Dark" : "Light");
    }

    [RelayCommand]
    private async Task OpenFavorites() =>
        await Shell.Current.GoToAsync($"{nameof(EntryListPage)}?mode=favorites");

    [RelayCommand]
    private async Task OpenRecent() =>
        await Shell.Current.GoToAsync($"{nameof(EntryListPage)}?mode=recent");

    [RelayCommand]
    private async Task RandomEntry()
    {
        var e = _data.Random();
        if (e is not null)
            await Shell.Current.GoToAsync($"{nameof(EntryDetailPage)}?id={e.Id}");
    }

    [RelayCommand]
    private async Task OpenTimeline() =>
        await Shell.Current.GoToAsync(nameof(TimelinePage));

    [RelayCommand]
    private async Task OpenCompare() =>
        await Shell.Current.GoToAsync(nameof(ComparePage));

    [RelayCommand]
    private async Task OpenWorldMap() =>
        await Shell.Current.GoToAsync(nameof(WorldMapPage));

    [RelayCommand]
    private async Task Backup()
    {
        try
        {
            var json = _state.ExportJson();
            var name = $"suikoden-codex-backup-{DateTime.Now:yyyy-MM-dd}.json";
            var path = Path.Combine(FileSystem.CacheDirectory, name);
            await File.WriteAllTextAsync(path, json);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Suikoden Codex backup",
                File = new ShareFile(path),
            });
        }
        catch (Exception ex)
        {
            await Alert("Backup failed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task Restore()
    {
        try
        {
            var pick = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose a Suikoden Codex backup",
            });
            if (pick is null) return;

            using var stream = await pick.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var (ok, message) = _state.ImportJson(json);
            RefreshCounts();
            IsSpoilerSafe = _state.SpoilerSafe;
            await Alert(ok ? "Restore complete" : "Couldn't restore", message);
        }
        catch (Exception ex)
        {
            await Alert("Restore failed", ex.Message);
        }
    }

    private static Task Alert(string title, string message) =>
        Shell.Current?.DisplayAlert(title, message, "OK") ?? Task.CompletedTask;
}
