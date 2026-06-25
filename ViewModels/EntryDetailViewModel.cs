using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Models;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;

namespace SuikodenCodex.ViewModels;

[QueryProperty(nameof(EntryId), "id")]
public partial class EntryDetailViewModel : ObservableObject
{
    private readonly CodexData _data;
    private readonly UserState _state;

    public ObservableCollection<CodexEntry> RelatedEntries { get; } = new();
    public ObservableCollection<CodexEntry> Unites { get; } = new();
    public ObservableCollection<SectionVM> Sections { get; } = new();

    [ObservableProperty]
    private bool _hasSections;

    [ObservableProperty]
    private bool _hasUnites;

    [ObservableProperty]
    private string _unitesTitle = "Unite attacks";

    [ObservableProperty]
    private CodexEntry? _entry;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _hasRelated;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDescription))]
    private bool _descriptionHidden;

    public bool ShowDescription => !DescriptionHidden;

    private string? _entryId;
    public string? EntryId
    {
        get => _entryId;
        set
        {
            _entryId = value;
            Load();
        }
    }

    public EntryDetailViewModel(CodexData data, UserState state)
    {
        _data = data;
        _state = state;
    }

    private async void Load()
    {
        await _data.EnsureLoadedAsync();
        if (string.IsNullOrEmpty(_entryId)) return;

        Entry = _data.GetById(_entryId);
        if (Entry is null) return;

        IsFavorite = _state.IsFavorite(Entry.Id);
        _state.PushRecent(Entry.Id);

        // Spoiler-safe: hide the description until tapped to reveal.
        DescriptionHidden = _state.SpoilerSafe;

        // Split related entries: Unite attacks get their own labeled section.
        RelatedEntries.Clear();
        Unites.Clear();
        foreach (var r in _data.RelatedTo(Entry, max: 40))
        {
            if (r.Category == Models.EntryCategory.ComboAttack && Entry.Category != Models.EntryCategory.ComboAttack)
                Unites.Add(r);
            else if (RelatedEntries.Count < 12)
                RelatedEntries.Add(r);
        }
        HasUnites = Unites.Count > 0;
        UnitesTitle = Unites.Count == 1 ? "Unite attack" : "Unite attacks";
        HasRelated = RelatedEntries.Count > 0;

        Sections.Clear();
        foreach (var s in Entry.Sections)
            Sections.Add(new SectionVM(s));
        HasSections = Sections.Count > 0;
    }

    [RelayCommand]
    private void RevealDescription() => DescriptionHidden = false;

    [RelayCommand]
    private async Task OpenImage()
    {
        if (Entry?.ImageName is { } img)
            await Shell.Current.GoToAsync($"{nameof(ImageViewerPage)}?img={img}");
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        if (Entry is null) return;
        IsFavorite = _state.ToggleFavorite(Entry.Id);
    }

    [RelayCommand]
    private static async Task OpenRelated(CodexEntry? entry)
    {
        if (entry is null) return;
        await Shell.Current.GoToAsync($"{nameof(EntryDetailPage)}?id={entry.Id}");
    }

    [RelayCommand]
    private async Task ViewSource()
    {
        if (Entry?.SourcePage is not int page) return;
        await Shell.Current.GoToAsync($"{nameof(PageImagePage)}?page={page}");
    }
}
