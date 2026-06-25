using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;

namespace SuikodenCodex.ViewModels;

public partial class RecruitmentViewModel : ObservableObject
{
    private readonly CodexData _data;
    private readonly UserState _state;
    private bool _initialized;

    public ObservableCollection<GameTab> Games { get; } = new();
    public ObservableCollection<RecruitItem> Recruits { get; } = new();

    [ObservableProperty] private GameTab? _selectedGame;
    [ObservableProperty] private bool _comingSoon;
    [ObservableProperty] private bool _hasList;
    [ObservableProperty] private int _recruitedCount;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _progressLabel = "";
    [ObservableProperty] private string _comingSoonText = "";
    [ObservableProperty] private bool _hasMissableHints;
    [ObservableProperty] private string _missableLabel = "";

    // Short labels for the selector chips
    private static readonly string[] Labels = { "I", "II", "III", "IV", "V" };

    public RecruitmentViewModel(CodexData data, UserState state)
    {
        _data = data;
        _state = state;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            // Re-sync the visible list with persisted state (e.g. after a Restore).
            RefreshFromState();
            return;
        }
        await _data.EnsureLoadedAsync();

        for (int i = 0; i < CodexData.RecruitGames.Length; i++)
        {
            var g = CodexData.RecruitGames[i];
            Games.Add(new GameTab(g, Labels[i], _data.HasRecruits(g)));
        }
        _initialized = true;

        var first = Games.FirstOrDefault(g => g.Available) ?? Games[0];
        SelectGame(first);
    }

    [RelayCommand]
    private void SelectGame(GameTab tab)
    {
        foreach (var g in Games) g.IsSelected = ReferenceEquals(g, tab);
        SelectedGame = tab;
        LoadList(tab);
    }

    /// <summary>Deep-link entry point from the Home dashboard.</summary>
    public async Task SelectGameByNameAsync(string game)
    {
        await InitializeAsync();
        var tab = Games.FirstOrDefault(g => g.Game == game && g.Available);
        if (tab is not null) SelectGame(tab);
    }

    /// <summary>Re-read recruited flags for the current list from persisted state.</summary>
    private void RefreshFromState()
    {
        if (SelectedGame is null || !HasList) return;
        foreach (var r in Recruits)
            r.Recruited = _state.IsRecruited(SelectedGame.Game, r.Num);
        RecomputeProgress();
    }

    private void LoadList(GameTab tab)
    {
        Recruits.Clear();

        if (!tab.Available)
        {
            ComingSoon = true;
            HasList = false;
            ComingSoonText = $"{tab.Game} recruitment guide coming soon.";
            return;
        }

        ComingSoon = false;
        HasList = true;
        foreach (var r in _data.GetRecruits(tab.Game))
            Recruits.Add(new RecruitItem(r, _state.IsRecruited(tab.Game, r.Num)));

        var missable = Recruits.Count(r => r.MissableHint);
        HasMissableHints = missable > 0;
        MissableLabel = $"⚠ {missable} recruit{(missable == 1 ? "" : "s")} mention timing — auto-detected from the notes, so read the full method before you progress.";

        RecomputeProgress();
    }

    [RelayCommand]
    private void ToggleRecruited(RecruitItem? item)
    {
        if (item is null || SelectedGame is null) return;
        item.Recruited = _state.ToggleRecruited(SelectedGame.Game, item.Num);
        RecomputeProgress();
    }

    [RelayCommand]
    private void Reset()
    {
        if (SelectedGame is null) return;
        _state.ResetRecruited(SelectedGame.Game);
        foreach (var r in Recruits) r.Recruited = false;
        RecomputeProgress();
    }

    [RelayCommand]
    private static async Task OpenEntry(RecruitItem? item)
    {
        if (item?.EntryId is null) return;
        await Shell.Current.GoToAsync($"{nameof(EntryDetailPage)}?id={item.EntryId}");
    }

    private void RecomputeProgress()
    {
        TotalCount = Recruits.Count;
        RecruitedCount = Recruits.Count(r => r.Recruited);
        Progress = TotalCount == 0 ? 0 : (double)RecruitedCount / TotalCount;
        ProgressLabel = $"{RecruitedCount} / {TotalCount} recruited";
    }
}
