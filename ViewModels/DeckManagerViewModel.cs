using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Models;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;
using SuikodenCodex.Services.CardGame;

namespace SuikodenCodex.ViewModels;

public class DeckSummaryVM
{
    public SavedDeck Deck { get; init; } = null!;
    public string Name => Deck.Name;
    public string Subtitle { get; init; } = "";
    public string MastermindLabel { get; init; } = "";
    public bool Valid { get; init; }
    public Microsoft.Maui.Graphics.Color DotColor =>
        Valid ? Microsoft.Maui.Graphics.Color.FromArgb("#6FCF7A") : Microsoft.Maui.Graphics.Color.FromArgb("#C98A3A");
}

public partial class DeckManagerViewModel : ObservableObject
{
    readonly CardData _data;
    readonly DeckStore _store;

    public DeckManagerViewModel(CardData data, DeckStore store) { _data = data; _store = store; }

    public ObservableCollection<DeckSummaryVM> Decks { get; } = new();
    [ObservableProperty] private bool _hasDecks;

    public async Task RefreshAsync()
    {
        await _data.EnsureLoadedAsync();
        await _store.EnsureLoadedAsync();

        Decks.Clear();
        foreach (var d in _store.Decks)
        {
            var cards = d.CardNumbers.Select(n => _data.Cards.FirstOrDefault(c => c.Number == n))
                                     .Where(c => c is not null).Select(c => c!).ToList();
            var mm = d.MastermindNumber is null ? null : _data.Cards.FirstOrDefault(c => c.Number == d.MastermindNumber);
            var check = DeckRules.Check(cards, mm);
            Decks.Add(new DeckSummaryVM
            {
                Deck = d,
                Valid = check.Valid,
                Subtitle = $"{cards.Count}/50 · " + (check.Valid ? "ready to play" : (check.Issues.FirstOrDefault() ?? "incomplete")),
                MastermindLabel = mm is null ? "" : $"Mastermind: {mm.Name}",
            });
        }
        HasDecks = Decks.Count > 0;
    }

    [RelayCommand]
    private async Task NewDeck() => await Shell.Current.GoToAsync(nameof(DeckBuilderPage));

    [RelayCommand]
    private async Task EditDeck(DeckSummaryVM? s)
    {
        if (s is null) return;
        await Shell.Current.GoToAsync($"{nameof(DeckBuilderPage)}?id={s.Deck.Id}");
    }

    [RelayCommand]
    private async Task DeleteDeck(DeckSummaryVM? s)
    {
        if (s is null) return;
        bool ok = await (Shell.Current?.DisplayAlert("Delete deck", $"Delete “{s.Name}”?", "Delete", "Cancel") ?? Task.FromResult(false));
        if (!ok) return;
        await _store.DeleteAsync(s.Deck.Id);
        await RefreshAsync();
    }
}
