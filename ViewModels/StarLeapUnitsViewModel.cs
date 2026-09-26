using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;
using SuikodenCodex.StarLeap;

namespace SuikodenCodex.ViewModels;

public partial class StarLeapUnitsViewModel : ObservableObject
{
    private static readonly string[] RarityOrder = { "108 Stars", "Guest", "SSR", "SR", "R" };
    private static readonly string[] ElementOrder = { "Fire", "Water", "Wind", "Earth", "Lightning", "Holy", "Dark" };
    private readonly StarLeapData _data;

    public StarLeapUnitsViewModel(StarLeapData data) => _data = data;

    public ObservableCollection<FilterChip> Chips { get; } = new();
    public ObservableCollection<SlUnitRow> Rows { get; } = new();

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _resultSummary = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortLabel))]
    private UnitSort _sort = UnitSort.Name;

    public string SortLabel => Sort == UnitSort.Name ? "Sort: A–Z" : "Sort: Newest";

    public async Task InitializeAsync()
    {
        await _data.EnsureLoadedAsync();
        if (Chips.Count == 0)
            BuildChips();
        Apply();
    }

    partial void OnSearchTextChanged(string value) => Apply();

    partial void OnSortChanged(UnitSort value) => Apply();

    [RelayCommand]
    private void ToggleChip(FilterChip? chip)
    {
        if (chip is null)
            return;
        var select = !chip.IsSelected;
        foreach (var c in Chips.Where(c => c.Dimension == chip.Dimension))
            c.IsSelected = false;
        chip.IsSelected = select;
        Apply();
    }

    [RelayCommand]
    private void ToggleSort() => Sort = Sort == UnitSort.Name ? UnitSort.Newest : UnitSort.Name;

    [RelayCommand]
    private Task OpenUnit(SlUnitRow? row) =>
        row is null ? Task.CompletedTask : Shell.Current.GoToAsync($"{nameof(StarLeapUnitDetailPage)}?id={row.Id}");

    private void BuildChips()
    {
        var units = _data.Units;
        AddChips("rarity", Ordered(units.Select(u => u.Rarity), RarityOrder));
        AddChips("element", Ordered(units.SelectMany(u => u.Elements), ElementOrder));
        AddChips("role", Ordered(units.Select(u => u.Role), Array.Empty<string>()));
        AddChips("weapon", Ordered(units.SelectMany(u => u.Weapons), Array.Empty<string>()));
        Chips.Add(new FilterChip("origin", nameof(OriginFilter.StarLeap), "Star Leap era"));
        Chips.Add(new FilterChip("origin", nameof(OriginFilter.Classic), "Classic-game versions"));
    }

    private void AddChips(string dimension, IEnumerable<string> values)
    {
        foreach (var value in values)
            Chips.Add(new FilterChip(dimension, value, value));
    }

    private static IEnumerable<string> Ordered(IEnumerable<string> values, string[] preferred)
    {
        var present = values.Where(v => !string.IsNullOrEmpty(v)).ToHashSet();
        return preferred.Where(present.Contains)
            .Concat(present.Except(preferred).OrderBy(v => v, StringComparer.OrdinalIgnoreCase));
    }

    private string? Selected(string dimension) =>
        Chips.FirstOrDefault(c => c.Dimension == dimension && c.IsSelected)?.Value;

    private void Apply()
    {
        var origin = Selected("origin") switch
        {
            nameof(OriginFilter.StarLeap) => OriginFilter.StarLeap,
            nameof(OriginFilter.Classic) => OriginFilter.Classic,
            _ => OriginFilter.Any,
        };
        var query = new UnitQuery
        {
            Text = SearchText,
            Rarity = Selected("rarity"),
            Element = Selected("element"),
            Role = Selected("role"),
            Weapon = Selected("weapon"),
            Origin = origin,
            Sort = Sort,
        };
        var result = query.Apply(_data.Units);
        Rows.Clear();
        foreach (var unit in result)
            Rows.Add(new SlUnitRow(unit, _data.Portrait(unit)));
        ResultSummary = $"{result.Count} of {_data.Units.Count} units";
    }
}
