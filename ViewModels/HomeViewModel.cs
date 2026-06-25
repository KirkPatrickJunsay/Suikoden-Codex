using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Models;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;

namespace SuikodenCodex.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly CodexData _data;
    private readonly UserState _state;
    private readonly RecruitmentViewModel _recruitment;
    private bool _loaded;

    public ObservableCollection<GameProgress> GameProgress { get; } = new();
    public ObservableCollection<CodexEntry> Recent { get; } = new();

    [ObservableProperty] private int _entryCount;
    [ObservableProperty] private int _favoriteCount;
    [ObservableProperty] private bool _hasRecent;

    // Overall Star of Destiny completion (all games combined)
    [ObservableProperty] private int _completionPercent;
    [ObservableProperty] private double _completionProgress;
    [ObservableProperty] private string _completionLabel = "";

    // Continue where you left off
    [ObservableProperty] private CodexEntry? _lastViewed;
    [ObservableProperty] private bool _hasLastViewed;

    public HomeViewModel(CodexData data, UserState state, RecruitmentViewModel recruitment)
    {
        _data = data;
        _state = state;
        _recruitment = recruitment;
    }

    public async Task RefreshAsync()
    {
        await _data.EnsureLoadedAsync();
        _loaded = true;

        GameProgress.Clear();
        int totalRecruited = 0, totalStars = 0;
        foreach (var g in CodexData.RecruitGames)
        {
            if (!_data.HasRecruits(g)) continue;
            int recruited = _state.RecruitedCount(g);
            int total = _data.GetRecruits(g).Count;
            totalRecruited += recruited;
            totalStars += total;
            GameProgress.Add(new GameProgress { Game = g, Recruited = recruited, Total = total });
        }
        CompletionProgress = totalStars == 0 ? 0 : (double)totalRecruited / totalStars;
        CompletionPercent = (int)System.Math.Round(CompletionProgress * 100);
        CompletionLabel = $"{totalRecruited} / {totalStars} Stars of Destiny recruited";

        // Continue where you left off = most recent; the strip shows the rest.
        var recent = _data.Resolve(_state.Recent).ToList();
        LastViewed = recent.FirstOrDefault();
        HasLastViewed = LastViewed is not null;
        Recent.Clear();
        foreach (var e in recent.Skip(1).Take(12))
            Recent.Add(e);
        HasRecent = Recent.Count > 0;

        EntryCount = _data.Entries.Count;
        FavoriteCount = _state.Favorites.Count;
    }

    [RelayCommand]
    private static async Task OpenEntry(CodexEntry? entry)
    {
        if (entry is null) return;
        await Shell.Current.GoToAsync($"{nameof(EntryDetailPage)}?id={entry.Id}");
    }

    [RelayCommand]
    private async Task OpenRecruitment(GameProgress? gp)
    {
        if (gp is not null) await _recruitment.SelectGameByNameAsync(gp.Game);
        await Shell.Current.GoToAsync("//recruitment");
    }

    [RelayCommand]
    private async Task OpenFavorites() =>
        await Shell.Current.GoToAsync($"{nameof(EntryListPage)}?mode=favorites");

    [RelayCommand]
    private async Task Random()
    {
        var e = _data.Random();
        if (e is not null) await Shell.Current.GoToAsync($"{nameof(EntryDetailPage)}?id={e.Id}");
    }

    [RelayCommand]
    private static async Task OpenDuel() => await Shell.Current.GoToAsync(nameof(DuelPage));
}
