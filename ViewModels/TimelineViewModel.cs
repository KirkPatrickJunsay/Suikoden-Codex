using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Models;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;

namespace SuikodenCodex.ViewModels;

/// <summary>One event on the series timeline.</summary>
public class TimelineItem
{
    public TimelineItem(CodexEntry e)
    {
        Entry = e;
        YearLabel = (e.YearApprox ? "SY ~" : "SY ") + e.Year;
        Glyph = e.Category == EntryCategory.War ? "⚔️" : "📜";
    }

    public CodexEntry Entry { get; }
    public string YearLabel { get; }
    public string Glyph { get; }
    public string Name => Entry.Name;
    public string? Game => Entry.Game;
}

/// <summary>A chronological era grouping of timeline events.</summary>
public class TimelineGroup : List<TimelineItem>
{
    public TimelineGroup(string title, IEnumerable<TimelineItem> items) : base(items) => Title = title;
    public string Title { get; }
}

public partial class TimelineViewModel : ObservableObject
{
    private readonly CodexData _data;
    private bool _loaded;

    public ObservableCollection<TimelineGroup> Groups { get; } = new();

    public TimelineViewModel(CodexData data) => _data = data;

    public async Task InitializeAsync()
    {
        if (_loaded) return;
        await _data.EnsureLoadedAsync();

        var items = _data.Entries
            .Where(e => e.Year is not null)
            .OrderBy(e => e.Year)
            .ThenBy(e => e.Name, StringComparer.Ordinal)
            .Select(e => new TimelineItem(e))
            .ToList();

        foreach (var g in items.GroupBy(i => EraOrder(i.Entry.Year!.Value)).OrderBy(g => g.Key))
            Groups.Add(new TimelineGroup(EraTitle(g.Key), g));

        _loaded = true;
    }

    // Chronological era buckets (in-universe Solar Year).
    private static int EraOrder(int year) => year switch
    {
        <= 320 => 0,
        <= 439 => 1,
        <= 456 => 2,
        <= 459 => 3,
        <= 474 => 4,
        _ => 5,
    };

    private static string EraTitle(int bucket) => bucket switch
    {
        0 => "Suikoden IV · Island Liberation War (≈SY 307)",
        1 => "Between the wars",
        2 => "Suikoden V · The Sun Rune War (SY 440s)",
        3 => "Suikoden I · The Gate Rune War (SY 457)",
        4 => "Suikoden II · The Dunan Unification War (SY 460)",
        _ => "Suikoden III · War of the Champions (SY 475)",
    };

    [RelayCommand]
    private async Task OpenEntry(TimelineItem? item)
    {
        if (item is null) return;
        await Shell.Current.GoToAsync($"{nameof(EntryDetailPage)}?id={item.Entry.Id}");
    }
}
