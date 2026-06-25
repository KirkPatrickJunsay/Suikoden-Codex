using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Models;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;

namespace SuikodenCodex.ViewModels;

public partial class CardsViewModel : ObservableObject
{
    private readonly CardData _data;
    private bool _loaded;

    public ObservableCollection<CardEntry> Results { get; } = new();
    public ObservableCollection<CardPackFilter> Packs { get; } = new();

    [ObservableProperty] private string _query = "";
    [ObservableProperty] private int _count;

    private CardPackFilter? _selectedPack;

    public CardsViewModel(CardData data) => _data = data;

    public async Task InitializeAsync()
    {
        if (_loaded) return;
        await _data.EnsureLoadedAsync();

        Packs.Add(new CardPackFilter(null) { IsSelected = true });
        foreach (var p in _data.Packs)
            Packs.Add(new CardPackFilter(p));
        _selectedPack = Packs[0];

        _loaded = true;
        Apply();
    }

    partial void OnQueryChanged(string value) => Apply();

    [RelayCommand]
    private void SelectPack(CardPackFilter? pack)
    {
        if (pack is null) return;
        _selectedPack = pack;
        foreach (var p in Packs) p.IsSelected = ReferenceEquals(p, pack);
        Apply();
    }

    private void Apply()
    {
        var q = Query?.Trim().ToLowerInvariant() ?? "";
        IEnumerable<CardEntry> items = _data.Cards;

        if (_selectedPack?.Pack is { } pack)
            items = items.Where(c => c.Pack == pack);
        if (!string.IsNullOrEmpty(q))
            items = items.Where(c => c.SearchText.Contains(q));

        Results.Clear();
        foreach (var c in items)
            Results.Add(c);
        Count = Results.Count;
    }

    [RelayCommand]
    private static async Task OpenCard(CardEntry? card)
    {
        if (card is null) return;
        await Shell.Current.GoToAsync($"{nameof(CardDetailPage)}?id={card.Id}");
    }

    [RelayCommand]
    private static async Task OpenDuel() => await Shell.Current.GoToAsync(nameof(DuelPage));
}
