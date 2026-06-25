using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Models;
using SuikodenCodex.Services;
using SuikodenCodex.Services.CardGame;

namespace SuikodenCodex.ViewModels;

public partial class BuilderCardVM : ObservableObject
{
    public CardEntry Card { get; init; } = null!;
    [ObservableProperty] private int _copies;
    [ObservableProperty] private bool _isMastermind;

    public bool IsMastermindType => Card.Type == "Mastermind";
    public string Stat => Card.Type switch
    {
        "Mission" => $"Clear {Card.Cp} {Card.Cbtype}",
        "Facilities" => $"Build {Card.Cp}/Blk {Card.Bp}",
        _ => string.Join(" ", new[]
        {
            Card.Str is > 0 ? $"S{Card.Str}" : null,
            Card.Mil is > 0 ? $"M{Card.Mil}" : null,
            Card.Con is > 0 ? $"C{Card.Con}" : null,
            Card.Links.Count > 0 ? string.Concat(Card.Links) : null,
        }.Where(s => s is not null)),
    };

    public string Badge => IsMastermind ? "★ MM" : (Copies > 0 ? $"×{Copies}" : "");
    public bool ShowBadge => IsMastermind || Copies > 0;

    partial void OnCopiesChanged(int v) { OnPropertyChanged(nameof(Badge)); OnPropertyChanged(nameof(ShowBadge)); }
    partial void OnIsMastermindChanged(bool v) { OnPropertyChanged(nameof(Badge)); OnPropertyChanged(nameof(ShowBadge)); }
}

public partial class DeckBuilderViewModel : ObservableObject, IQueryAttributable
{
    readonly CardData _data;
    readonly DeckStore _store;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        DeckId = query.TryGetValue("id", out var v) ? v?.ToString() ?? "" : "";
    }

    string _id = "";
    readonly List<string> _numbers = new();
    string? _mastermind;

    public DeckBuilderViewModel(CardData data, DeckStore store) { _data = data; _store = store; }

    [ObservableProperty] private string _deckId = "";   // query param ("" = new)
    [ObservableProperty] private string _deckName = "New Deck";
    [ObservableProperty] private string _countLabel = "0 / 50";
    [ObservableProperty] private bool _isValid;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _mastermindLabel = "Mastermind: none";
    [ObservableProperty] private bool _hasMastermind;
    [ObservableProperty] private string _query = "";
    [ObservableProperty] private bool _showDeckOnly;

    public ObservableCollection<BuilderCardVM> Cards { get; } = new();
    public List<string> TypeFilters { get; } = new() { "All", "Leader", "Commoner", "Free", "Craftman", "Mission", "Facilities", "Mastermind" };
    [ObservableProperty] private int _typeIndex;

    Dictionary<string, BuilderCardVM> _byNumber = new();

    public async Task InitializeAsync()
    {
        await _data.EnsureLoadedAsync();
        await _store.EnsureLoadedAsync();

        _id = string.IsNullOrEmpty(DeckId) ? Guid.NewGuid().ToString("N") : DeckId;
        var existing = _store.Get(_id);
        _numbers.Clear();
        if (existing is not null)
        {
            DeckName = existing.Name;
            _numbers.AddRange(existing.CardNumbers);
            _mastermind = existing.MastermindNumber;
        }
        else { DeckName = "New Deck"; _mastermind = null; }

        // one VM per card (reused; filtered into Cards view)
        _byNumber = _data.Cards.ToDictionary(c => c.Number, c => new BuilderCardVM { Card = c });
        SyncCounts();
        ApplyFilter();
        Recompute();
    }

    void SyncCounts()
    {
        foreach (var vm in _byNumber.Values) { vm.Copies = 0; vm.IsMastermind = false; }
        foreach (var n in _numbers) if (_byNumber.TryGetValue(n, out var vm)) vm.Copies++;
        if (_mastermind is not null && _byNumber.TryGetValue(_mastermind, out var mm)) mm.IsMastermind = true;
    }

    partial void OnQueryChanged(string v) => ApplyFilter();
    partial void OnShowDeckOnlyChanged(bool v) => ApplyFilter();
    partial void OnTypeIndexChanged(int v) => ApplyFilter();

    [RelayCommand]
    private void SelectType(string t) => TypeIndex = TypeFilters.IndexOf(t);

    void ApplyFilter()
    {
        string q = (Query ?? "").Trim().ToLowerInvariant();
        string type = TypeFilters[Math.Clamp(TypeIndex, 0, TypeFilters.Count - 1)];

        IEnumerable<BuilderCardVM> src = _data.Cards.Select(c => _byNumber[c.Number]);
        if (type != "All") src = src.Where(v => v.Card.Type == type);
        if (ShowDeckOnly) src = src.Where(v => v.Copies > 0 || v.IsMastermind);
        if (q.Length > 0) src = src.Where(v => v.Card.SearchText.Contains(q));

        Cards.Clear();
        foreach (var v in src.Take(400)) Cards.Add(v);
    }

    [RelayCommand]
    private void TapCard(BuilderCardVM vm)
    {
        if (vm is null) return;
        if (vm.IsMastermindType)
        {
            // set / toggle the single Mastermind slot
            if (_mastermind == vm.Card.Number) { _mastermind = null; }
            else
            {
                if (_mastermind is not null && _byNumber.TryGetValue(_mastermind, out var prev)) prev.IsMastermind = false;
                _mastermind = vm.Card.Number;
            }
            SyncCounts();
            Recompute();
            return;
        }
        // add a copy (≤4 per name, ≤50 total)
        if (_numbers.Count >= DeckRules.Size) { Flash("Deck is full (50)."); return; }
        if (_numbers.Count(n => n == vm.Card.Number) >= DeckRules.MaxCopies) { Flash($"Max {DeckRules.MaxCopies} copies."); return; }
        _numbers.Add(vm.Card.Number);
        vm.Copies++;
        Recompute();
    }

    [RelayCommand]
    private void RemoveCard(BuilderCardVM vm)
    {
        if (vm is null) return;
        if (vm.IsMastermind) { _mastermind = null; vm.IsMastermind = false; Recompute(); return; }
        if (_numbers.Remove(vm.Card.Number)) { vm.Copies--; if (ShowDeckOnly && vm.Copies == 0) ApplyFilter(); Recompute(); }
    }

    [RelayCommand]
    private void ClearMastermind()
    {
        if (_mastermind is null) return;
        if (_byNumber.TryGetValue(_mastermind, out var vm)) vm.IsMastermind = false;
        _mastermind = null;
        Recompute();
    }

    [RelayCommand]
    private async Task Save()
    {
        var deck = new SavedDeck { Id = _id, Name = string.IsNullOrWhiteSpace(DeckName) ? "Unnamed Deck" : DeckName.Trim(), CardNumbers = new(_numbers), MastermindNumber = _mastermind };
        await _store.UpsertAsync(deck);
        await Shell.Current.GoToAsync("..");
    }

    List<CardEntry> Resolve() => _numbers.Select(n => _data.Cards.FirstOrDefault(c => c.Number == n)).Where(c => c is not null).Select(c => c!).ToList();

    void Recompute()
    {
        CountLabel = $"{_numbers.Count} / {DeckRules.Size}";
        var mm = _mastermind is null ? null : _data.Cards.FirstOrDefault(c => c.Number == _mastermind);
        MastermindLabel = mm is null ? "Mastermind: none (optional)" : $"Mastermind: {mm.Name} ✕";
        HasMastermind = mm is not null;

        var check = DeckRules.Check(Resolve(), mm);
        IsValid = check.Valid;
        if (!check.Valid) StatusText = "⚠ " + check.Issues[0];
        else if (check.Warnings.Count > 0) StatusText = "✓ Legal · " + check.Warnings[0];
        else StatusText = "✓ Legal deck — ready to play.";
    }

    void Flash(string msg) => StatusText = "⚠ " + msg;
}
