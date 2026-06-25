using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Models;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;

namespace SuikodenCodex.ViewModels;

public partial class BrowseViewModel : ObservableObject
{
    private readonly CodexData _data;

    public ObservableCollection<EntryGroup> Groups { get; } = new();
    public ObservableCollection<CategoryFilter> Filters { get; } = new();
    public ObservableCollection<GameFilter> GameFilters { get; } = new();

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private string _resultSummary = "";

    [ObservableProperty]
    private bool _showGameFilter;

    private CategoryFilter? _selectedFilter;
    private GameFilter? _selectedGame;
    private bool _initialized;

    public BrowseViewModel(CodexData data)
    {
        _data = data;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        await _data.EnsureLoadedAsync();

        Filters.Add(new CategoryFilter(null) { IsSelected = true });
        foreach (var cat in Enum.GetValues<EntryCategory>())
            Filters.Add(new CategoryFilter(cat));
        _selectedFilter = Filters[0];

        GameFilters.Add(new GameFilter(null) { IsSelected = true });
        foreach (var token in _data.AvailableGameTokens())
            GameFilters.Add(new GameFilter(token));
        _selectedGame = GameFilters[0];
        ShowGameFilter = GameFilters.Count > 2; // only worth showing if >1 game present

        _initialized = true;
        Refresh();
    }

    partial void OnSearchTextChanged(string value) => Refresh();

    [RelayCommand]
    private void SelectFilter(CategoryFilter filter)
    {
        // Clicking the already-active category pill toggles it off, reverting to "All".
        // ("All" itself can't be toggled off — a filter is always active.)
        var target = (_selectedFilter == filter && filter.Category is not null)
            ? Filters[0]   // index 0 is the "All" chip
            : filter;

        // Set every chip's state in one pass so exactly one is ever highlighted.
        foreach (var f in Filters)
            f.IsSelected = ReferenceEquals(f, target);

        _selectedFilter = target;
        Refresh();
    }

    [RelayCommand]
    private void SelectGame(GameFilter game)
    {
        var target = (_selectedGame == game && game.Token is not null)
            ? GameFilters[0]   // "All games"
            : game;

        foreach (var g in GameFilters)
            g.IsSelected = ReferenceEquals(g, target);

        _selectedGame = target;
        Refresh();
    }

    private void Refresh()
    {
        var matches = _data.Search(SearchText, _selectedFilter?.Category, _selectedGame?.Token).ToList();

        Groups.Clear();
        foreach (var g in matches
                     .GroupBy(e => e.IndexLetter)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            Groups.Add(new EntryGroup(g.Key, g.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)));
        }

        IsEmpty = matches.Count == 0;
        ResultSummary = matches.Count == 1 ? "1 entry" : $"{matches.Count} entries";
    }

    [RelayCommand]
    private static async Task OpenEntry(CodexEntry? entry)
    {
        if (entry is null) return;
        await Shell.Current.GoToAsync($"{nameof(EntryDetailPage)}?id={entry.Id}");
    }

    [RelayCommand]
    private async Task RandomEntry()
    {
        var e = _data.Random();
        if (e is not null)
            await Shell.Current.GoToAsync($"{nameof(EntryDetailPage)}?id={e.Id}");
    }
}
