using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Models;
using SuikodenCodex.Services;

namespace SuikodenCodex.ViewModels;

/// <summary>One row of the side-by-side comparison.</summary>
public class CompareRow
{
    public CompareRow(string label, string a, string b)
    {
        Label = label;
        A = string.IsNullOrWhiteSpace(a) ? "—" : a;
        B = string.IsNullOrWhiteSpace(b) ? "—" : b;
        Differs = A != B && A != "—" && B != "—";
    }
    public string Label { get; }
    public string A { get; }
    public string B { get; }
    public bool Differs { get; }
}

public partial class CompareViewModel : ObservableObject
{
    private readonly CodexData _data;
    private bool _loaded;
    private int _activeSlot; // 1 = A, 2 = B

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasA))] private CodexEntry? _slotA;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasB))] private CodexEntry? _slotB;
    [ObservableProperty] private bool _picking;
    [ObservableProperty] private string _pickTitle = "";
    [ObservableProperty] private string _query = "";
    [ObservableProperty] private bool _hasBoth;

    public bool HasA => SlotA is not null;
    public bool HasB => SlotB is not null;

    public ObservableCollection<CodexEntry> Results { get; } = new();
    public ObservableCollection<CompareRow> Rows { get; } = new();

    public CompareViewModel(CodexData data) => _data = data;

    public async Task InitializeAsync()
    {
        if (_loaded) return;
        await _data.EnsureLoadedAsync();
        _loaded = true;
    }

    [RelayCommand]
    private void PickSlot(string which)
    {
        _activeSlot = which == "A" ? 1 : 2;
        PickTitle = $"Choose {(which == "A" ? "first" : "second")} entry";
        Query = "";
        Picking = true;
        UpdateResults();
    }

    partial void OnQueryChanged(string value)
    {
        if (Picking) UpdateResults();
    }

    private void UpdateResults()
    {
        var q = Query?.Trim().ToLowerInvariant() ?? "";
        Results.Clear();
        IEnumerable<CodexEntry> items = _data.Entries.Where(e => e.HasFacts);
        if (!string.IsNullOrEmpty(q))
            items = items.Where(e => e.Name.ToLowerInvariant().Contains(q));
        else
            items = items.Take(0); // require a query to avoid dumping everything
        foreach (var e in items.Take(40))
            Results.Add(e);
    }

    [RelayCommand]
    private void Choose(CodexEntry? entry)
    {
        if (entry is null) return;
        if (_activeSlot == 1) SlotA = entry; else SlotB = entry;
        Picking = false;
        Query = "";
        Rebuild();
    }

    [RelayCommand]
    private void CancelPick() => Picking = false;

    [RelayCommand]
    private void Swap()
    {
        (SlotA, SlotB) = (SlotB, SlotA);
        Rebuild();
    }

    private void Rebuild()
    {
        Rows.Clear();
        HasBoth = SlotA is not null && SlotB is not null;
        if (!HasBoth) return;

        AddRow("Game", SlotA!.Game, SlotB!.Game);
        AddRow("Category", SlotA.CategoryDisplay, SlotB.CategoryDisplay);
        AddRow("Type", SlotA.Subtype, SlotB.Subtype);

        // Union of fact labels, A's order first then any extras from B.
        var labels = new List<string>();
        foreach (var f in SlotA.Facts) if (!labels.Contains(f.Label)) labels.Add(f.Label);
        foreach (var f in SlotB.Facts) if (!labels.Contains(f.Label)) labels.Add(f.Label);

        foreach (var label in labels)
        {
            var a = SlotA.Facts.FirstOrDefault(f => f.Label == label)?.Value ?? "";
            var b = SlotB.Facts.FirstOrDefault(f => f.Label == label)?.Value ?? "";
            AddRow(label, a, b);
        }
    }

    private void AddRow(string label, string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b)) return;
        Rows.Add(new CompareRow(label, a ?? "", b ?? ""));
    }
}
