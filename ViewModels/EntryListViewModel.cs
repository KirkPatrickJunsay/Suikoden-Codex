using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Models;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;

namespace SuikodenCodex.ViewModels;

[QueryProperty(nameof(Mode), "mode")]
public partial class EntryListViewModel : ObservableObject
{
    private readonly CodexData _data;
    private readonly UserState _state;

    public ObservableCollection<CodexEntry> Entries { get; } = new();

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private string _emptyMessage = "";

    private string? _mode;
    public string? Mode
    {
        get => _mode;
        set { _mode = value; Load(); }
    }

    public EntryListViewModel(CodexData data, UserState state)
    {
        _data = data;
        _state = state;
    }

    private async void Load()
    {
        await _data.EnsureLoadedAsync();
        Entries.Clear();

        if (_mode == "favorites")
        {
            Title = "Favorites";
            EmptyMessage = "No favorites yet.\nTap the ☆ on any entry to save it here.";
            foreach (var e in _data.Resolve(_state.Favorites)
                         .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
                Entries.Add(e);
        }
        else // recent
        {
            Title = "Recently Viewed";
            EmptyMessage = "Nothing viewed yet.";
            foreach (var e in _data.Resolve(_state.Recent)) // already most-recent-first
                Entries.Add(e);
        }

        IsEmpty = Entries.Count == 0;
    }

    [RelayCommand]
    private static async Task OpenEntry(CodexEntry? entry)
    {
        if (entry is null) return;
        await Shell.Current.GoToAsync($"{nameof(EntryDetailPage)}?id={entry.Id}");
    }
}
