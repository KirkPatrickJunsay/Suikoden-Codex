using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using SuikodenCodex.Models;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;

namespace SuikodenCodex.ViewModels;

public partial class WalkStepVM : ObservableObject
{
    public WalkStep Step { get; init; } = null!;
    [ObservableProperty] private bool _done;

    public bool Checkable => Step.Kind != "tip";
    public bool HasLink => !string.IsNullOrEmpty(Step.Link);
    public bool Missable => Step.Missable;
    public string Text => Step.Text;
    public string Icon => Step.Kind switch
    {
        "recruit" => "★",
        "boss" => "⚔",
        "tip" => "ℹ",
        _ => "▸",
    };
    public Microsoft.Maui.Graphics.Color IconColor => Step.Kind switch
    {
        "recruit" => Microsoft.Maui.Graphics.Color.FromArgb("#ECC56A"),
        "boss" => Microsoft.Maui.Graphics.Color.FromArgb("#D98A8A"),
        "tip" => Microsoft.Maui.Graphics.Color.FromArgb("#7FB8E0"),
        _ => Microsoft.Maui.Graphics.Color.FromArgb("#8C97C8"),
    };
    public string CheckGlyph => Done ? "✓" : "";
    public double TextOpacity => Done ? 0.5 : 1.0;

    partial void OnDoneChanged(bool value)
    {
        OnPropertyChanged(nameof(CheckGlyph));
        OnPropertyChanged(nameof(TextOpacity));
    }
}

public class WalkRecruitVM
{
    public RecruitChar Recruit { get; init; } = null!;
    public string Name => Recruit.Name;
    public string Star => Recruit.Star ?? "";
    public string Method => Recruit.Method;
    public bool HasStar => !string.IsNullOrEmpty(Recruit.Star);
    public bool Missable => Recruit.MissableHint;
    public bool HasLink => !string.IsNullOrEmpty(Recruit.EntryId);
    public string? EntryId => Recruit.EntryId;

    // "Automatic" = joins through the story; otherwise the player must seek them out (optional, with requirements).
    static readonly string[] AutoCues =
        { "automatic", "joins with", "joins along", "joins alongside", "beginning of the game", "very beginning" };
    public bool Optional => !AutoCues.Any(c => Method.ToLowerInvariant().Contains(c));
    public bool Auto => !Optional;
}

public partial class WalkChapterVM : ObservableObject
{
    public WalkChapter Chapter { get; init; } = null!;
    public ObservableCollection<WalkStepVM> Steps { get; } = new();
    public ObservableCollection<WalkRecruitVM> Recruits { get; } = new();
    public string Context => Chapter.Context;
    public bool HasContext => !string.IsNullOrEmpty(Chapter.Context);
    public bool HasRecruits => Recruits.Count > 0;
    public string RecruitsHeader => $"Recruit here · {Recruits.Count}";
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private string _progressLabel = "";
    [ObservableProperty] private bool _complete;

    public string ExpandGlyph => IsExpanded ? "▾" : "▸";
    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ExpandGlyph));

    public void Recompute()
    {
        int total = Steps.Count(s => s.Checkable);
        int done = Steps.Count(s => s.Checkable && s.Done);
        ProgressLabel = total > 0 ? $"{done}/{total}"
                      : (Recruits.Count > 0 ? $"{Recruits.Count} ★" : "");
        Complete = total > 0 && done == total;
    }
}

public partial class GamePickVM : ObservableObject
{
    public int Game { get; init; }
    public string Roman { get; init; } = "";
    [ObservableProperty] private bool _isSelected;

    public Microsoft.Maui.Graphics.Color TextColor =>
        IsSelected ? Microsoft.Maui.Graphics.Color.FromArgb("#161A2E")
                   : Microsoft.Maui.Graphics.Color.FromArgb("#9AA6D8");
    public Microsoft.Maui.Graphics.Color BackColor =>
        IsSelected ? Microsoft.Maui.Graphics.Color.FromArgb("#ECC56A")
                   : Microsoft.Maui.Graphics.Color.FromArgb("#20264A");

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(TextColor));
        OnPropertyChanged(nameof(BackColor));
    }
}

public partial class WalkthroughViewModel : ObservableObject
{
    static readonly string[] Roman = { "", "I", "II", "III", "IV", "V" };
    readonly WalkthroughData _data;
    readonly CodexData _codex;
    HashSet<string> _done = new();
    int _game = 1;
    bool _codexReady;

    public WalkthroughViewModel(WalkthroughData data, CodexData codex)
    {
        _data = data; _codex = codex;
        for (int g = 1; g <= 5; g++)
            Games.Add(new GamePickVM { Game = g, Roman = Roman[g], IsSelected = g == 1 });
    }

    [ObservableProperty] private string _title = "Walkthrough";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string _intro = "";
    [ObservableProperty] private int _percent;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _progressLabel = "";
    [ObservableProperty] private bool _hasChapters = true;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _emptyMessage = "";

    public ObservableCollection<GamePickVM> Games { get; } = new();
    public ObservableCollection<WalkChapterVM> Chapters { get; } = new();

    string ProgressKey => $"wt_done_s{_game}";

    public async Task InitializeAsync()
    {
        if (!_codexReady) { await _codex.EnsureLoadedAsync(); _codexReady = true; }
        if (Chapters.Count == 0) await LoadGameAsync(_game);
    }

    [RelayCommand]
    private async Task SelectGame(GamePickVM? pick)
    {
        if (pick is null || pick.Game == _game) return;
        await LoadGameAsync(pick.Game);
    }

    async Task LoadGameAsync(int gameNum)
    {
        _game = gameNum;
        foreach (var g in Games) g.IsSelected = g.Game == gameNum;
        _done = Load();

        var recruitsByNum = _codex.GetRecruits($"Suikoden {Roman[gameNum]}").ToDictionary(r => r.Num);
        var game = await _data.GetAsync(gameNum);

        Title = string.IsNullOrEmpty(game.Game) ? $"Walkthrough — Suikoden {Roman[gameNum]}" : $"Walkthrough — {game.Game}";
        Subtitle = game.Subtitle;
        Intro = game.Intro;

        Chapters.Clear();
        foreach (var c in game.Chapters)
        {
            var cvm = new WalkChapterVM { Chapter = c };
            foreach (var s in c.Steps)
                cvm.Steps.Add(new WalkStepVM { Step = s, Done = _done.Contains(s.Id) });
            var rvms = c.Recruits.Where(recruitsByNum.ContainsKey)
                                 .Select(n => new WalkRecruitVM { Recruit = recruitsByNum[n] })
                                 .OrderBy(r => r.Optional)   // required (story) first, optional after
                                 .ThenBy(r => r.Recruit.Num);
            foreach (var r in rvms) cvm.Recruits.Add(r);
            cvm.Recompute();
            Chapters.Add(cvm);
        }

        HasChapters = Chapters.Count > 0;
        IsEmpty = !HasChapters;
        EmptyMessage = HasChapters ? "" : $"The Suikoden {Roman[gameNum]} walkthrough is on the way.";

        // expand the first not-yet-complete chapter so the player lands where they are
        var current = Chapters.FirstOrDefault(c => !c.Complete) ?? Chapters.FirstOrDefault();
        if (current is not null) current.IsExpanded = true;
        RecomputeOverall();
    }

    [RelayCommand]
    private void ToggleStep(WalkStepVM? s)
    {
        if (s is null || !s.Checkable) return;
        s.Done = !s.Done;
        if (s.Done) _done.Add(s.Step.Id); else _done.Remove(s.Step.Id);
        Save();
        foreach (var c in Chapters) if (c.Steps.Contains(s)) { c.Recompute(); break; }
        RecomputeOverall();
    }

    [RelayCommand]
    private void ToggleChapter(WalkChapterVM? c)
    {
        if (c is not null) c.IsExpanded = !c.IsExpanded;
    }

    [RelayCommand]
    private async Task OpenLink(WalkStepVM? s)
    {
        if (s?.Step.Link is { Length: > 0 } id)
            await Shell.Current.GoToAsync($"{nameof(EntryDetailPage)}?id={id}");
    }

    [RelayCommand]
    private async Task OpenRecruit(WalkRecruitVM? r)
    {
        if (r?.EntryId is { Length: > 0 } id)
            await Shell.Current.GoToAsync($"{nameof(EntryDetailPage)}?id={id}");
    }

    [RelayCommand]
    private async Task Reset()
    {
        bool ok = await (Shell.Current?.DisplayAlert("Reset progress",
            "Clear all checked steps for this walkthrough?", "Reset", "Cancel") ?? Task.FromResult(false));
        if (!ok) return;
        _done.Clear();
        Save();
        foreach (var c in Chapters) { foreach (var s in c.Steps) s.Done = false; c.Recompute(); }
        RecomputeOverall();
    }

    void RecomputeOverall()
    {
        var all = Chapters.SelectMany(c => c.Steps).Where(s => s.Checkable).ToList();
        int done = all.Count(s => s.Done);
        Progress = all.Count == 0 ? 0 : (double)done / all.Count;
        Percent = (int)System.Math.Round(Progress * 100);
        ProgressLabel = $"{done} / {all.Count} steps done";
    }

    HashSet<string> Load()
    {
        var json = Preferences.Get(ProgressKey, "");
        if (string.IsNullOrEmpty(json)) return new();
        try { return JsonSerializer.Deserialize<HashSet<string>>(json) ?? new(); }
        catch { return new(); }
    }

    void Save() => Preferences.Set(ProgressKey, JsonSerializer.Serialize(_done));
}
