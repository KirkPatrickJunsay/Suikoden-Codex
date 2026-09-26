using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;

namespace SuikodenCodex.ViewModels;

public partial class StarLeapHubViewModel : ObservableObject
{
    private readonly StarLeapData _data;

    public StarLeapHubViewModel(StarLeapData data)
    {
        _data = data;
        _data.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
    }

    public ObservableCollection<SlUnitRow> Newest { get; } = new();
    public string Credit => StarLeapData.Credit;

    [ObservableProperty]
    private string _dataAsOf = "";

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    private string _unitCountText = "";

    [ObservableProperty]
    private bool _isChecking;

    public async Task InitializeAsync()
    {
        await _data.EnsureLoadedAsync();
        Refresh();
        await CheckAsync(force: false);
    }

    [RelayCommand]
    private Task CheckForUpdates() => CheckAsync(force: true);

    [RelayCommand]
    private Task OpenCharacters() => Shell.Current.GoToAsync(nameof(StarLeapUnitsPage));

    [RelayCommand]
    private Task OpenUnit(SlUnitRow? row) =>
        row is null ? Task.CompletedTask : Shell.Current.GoToAsync($"{nameof(StarLeapUnitDetailPage)}?id={row.Id}");

    [RelayCommand]
    private Task OpenAbout() => Shell.Current.GoToAsync($"{nameof(EntryDetailPage)}?id=game-suikoden_star_leap");

    private async Task CheckAsync(bool force)
    {
        if (IsChecking)
            return;
        IsChecking = true;
        try
        {
            await _data.CheckForUpdatesAsync(force);
        }
        finally
        {
            IsChecking = false;
            Refresh();
        }
    }

    private void Refresh()
    {
        DataAsOf = _data.Manifest is { } m ? $"Data as of {m.GeneratedAt.ToLocalTime():d MMM yyyy}" : "";
        Status = _data.Status;
        UnitCountText = $"{_data.Units.Count} units";
        Newest.Clear();
        foreach (var unit in _data.Units
                     .OrderByDescending(u => u.Released ?? "", StringComparer.Ordinal)
                     .ThenBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
                     .Take(12))
            Newest.Add(new SlUnitRow(unit, _data.Portrait(unit)));
    }
}
