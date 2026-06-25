using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using SuikodenCodex.Models;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;
using SuikodenCodex.Services.CardGame;

namespace SuikodenCodex.ViewModels;

public partial class DuelCardVM : ObservableObject
{
    public CardEntry Card { get; init; } = null!;
    public string Line1 { get; init; } = "";   // primary stat (context-aware)
    public string Line2 { get; init; } = "";   // links / secondary
    public bool Actionable { get; init; }
    public double Dim => Actionable ? 1.0 : 0.5;
}

public record HelpStep(string Icon, string Title, string Body);
public record RuleSection(string Title, string Body);

public partial class DuelViewModel : ObservableObject
{
    readonly CardData _data;
    readonly DeckStore _store;
    Duel? _duel;
    bool _dataReady;

    public DuelViewModel(CardData data, DeckStore store) { _data = data; _store = store; }

    public ObservableCollection<DeckSummaryVM> MyDecks { get; } = new();
    [ObservableProperty] private bool _hasMyDecks;

    // screen mode
    [ObservableProperty] private bool _showSetup = true;
    [ObservableProperty] private bool _showBoard;
    [ObservableProperty] private bool _showResult;

    // ---------------- onboarding / how-to-play ----------------
    public List<HelpStep> HelpSteps { get; } = new()
    {
        new("🎮", "Welcome to Card Duel",
            "A faithful take on the Genso Suikoden Card Stories TCG. You and the AI each have a 50-card deck. First to 5 Victory Points (VP) wins."),
        new("🗂️", "Your cards",
            "Leaders & Commoners are your forces — each has a Strength number (S) and link letters (A–I). Missions are goals worth VP. Facilities are structures you build."),
        new("🎯", "Play a mission",
            "On your turn, tap a glowing Mission in your hand to start a battle. It shows a clear value, e.g. “clear 6 STR”. Dimmed cards can't be played right now."),
        new("🤝", "Deploy & the Link rule",
            "Add characters to push toward the clear value. Your FIRST card must be a Leader. Every card after it must share at least one link letter with the cards already deployed — so links keep your chain going."),
        new("🏅", "Clear it — or get robbed",
            "The first side to reach the clear value wins the mission's VP. But the opponent deploys first and can steal it! VP shown as “1/2” means you get 1 if you clear it, they get 2 if they do."),
        new("🔁", "Turns & passing",
            "Players take turns deploying one card. Tap Pass when you can't or don't want to add a card. When both pass (or someone clears it), you each draw back up to 6 and it's the other player's turn."),
        new("⚔️", "Two more battle types",
            "Some missions clear by Military (MIL) — the bigger army wins the clash. Facilities are built with Construction (CON); every 2 you build = 1 VP. Watch the log to see what the opponent does. Good luck!"),
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentHelp))]
    [NotifyPropertyChangedFor(nameof(HelpCounter))]
    [NotifyPropertyChangedFor(nameof(NextLabel))]
    [NotifyPropertyChangedFor(nameof(CanHelpBack))]
    private int _helpIndex;
    [ObservableProperty] private bool _showHelp;

    public HelpStep CurrentHelp => HelpSteps[Math.Clamp(HelpIndex, 0, HelpSteps.Count - 1)];
    public string HelpCounter => $"{HelpIndex + 1} / {HelpSteps.Count}";
    public string NextLabel => HelpIndex >= HelpSteps.Count - 1 ? "Got it!" : "Next ›";
    public bool CanHelpBack => HelpIndex > 0;

    [RelayCommand] private void OpenHelp() { HelpIndex = 0; ShowHelp = true; }
    [RelayCommand] private void HelpBack() { if (HelpIndex > 0) HelpIndex--; }
    [RelayCommand]
    private void HelpNext()
    {
        if (HelpIndex >= HelpSteps.Count - 1) CloseHelp();
        else HelpIndex++;
    }
    [RelayCommand]
    private void CloseHelp()
    {
        ShowHelp = false;
        Preferences.Set("duel_help_seen", true);
    }

    // ---------------- full rules reference ----------------
    [ObservableProperty] private bool _showRules;
    [RelayCommand] private void OpenRules() { ShowHelp = false; ShowRules = true; }
    [RelayCommand] private void CloseRules() => ShowRules = false;

    public List<RuleSection> Rules { get; } = new()
    {
        new("🎯  Objective",
            "Be the first to 5 Victory Points (VP). You earn VP mainly by clearing Mission cards. You also lose instantly if you ever must draw but your deck is empty (deck-out)."),
        new("🗂️  Your deck & hand",
            "Each side has a 50-card deck mixing Characters, Missions and Facilities. You draw a hand of 6 and refill to 6 at the end of each of your turns."),
        new("👤  Character cards",
            "Your forces. Each shows:\n• STR (Strength) — clears normal missions and blocks facilities\n• MIL (Military) — used only in Military missions (big numbers)\n• CON (Construction) — used only to build facilities\n• Links A–I — the letters that let a card chain onto your others\n\nClasses: Leader (must open a battle), Commoner & Free (general troops), Craftman (good at facilities)."),
        new("📜  Mission cards",
            "An objective worth VP. It shows a Clear value (e.g. “clear 6 STR”) and a VP value written as self/opponent (e.g. 1/2): you get the first number if you clear it, the opponent gets the second if they steal it."),
        new("🏛️  Facility cards",
            "A structure you build. It shows a Build value (CON) and a Block value (STR). You build it with Construction; the opponent can't steal it, only destroy it with Strength. Every 2 facilities you build = 1 VP."),
        new("🔁  A turn, step by step",
            "1. Start a battle by playing one Mission or Facility (you need a Leader available).\n2. Deploy Step — your OPPONENT deploys first; the first card a side plays must be a Leader.\n3. The Link Rule — every card after the Leader must share at least one link letter with the cards already deployed.\n4. Players alternate deploying one card, or Pass. The battle resolves when someone wins it, or both pass.\n5. Both players draw back up to 6, then it's the other player's turn.\n\nIf you can't (or don't want to) start a battle, End Turn to discard weak cards and draw fresh ones."),
        new("🏅  Winning a battle",
            "• STR mission: first side to reach the Clear value in Strength clears it.\n• MIL mission: when both pass, the armies clash — the bigger total Military wins and keeps the difference; if that remainder ≥ the Clear value, it's cleared.\n• Facility: the owner builds with CON to the Build value; the opponent destroys it with STR to the Block value."),
        new("⚔️  The steal — important!",
            "Because your opponent deploys first, they can race to the Clear value and take YOUR mission — often for the bigger “opponent” VP. So don't play a mission you can't win, and watch for low-clear missions a single strong card can grab."),
        new("🔤  Stat shorthand",
            "On a card: STR = Strength, MIL = Military, CON = Construction, Links = its A–I letters. While deploying, a card shows how much it adds to the current goal, e.g. “+4 STR”. Missions show “Clear X” and “VP a/b”; facilities show “Build X / Block Y”."),
        new("ℹ️  About this version",
            "Two simplifications for now: a turn plays one mission/facility (the full TCG allows two at once), and card text abilities ([Deploy]/[Unite]/Rune) are shown but not yet active — battles are decided by the numbers and the Link rule. Ability effects are coming in a later update."),
    };

    public ObservableCollection<StarterDecks.Deck> Decks { get; } = new(StarterDecks.All);

    // board state
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private int _humanVp;
    [ObservableProperty] private int _aiVp;
    [ObservableProperty] private string _aiName = "Opponent";
    [ObservableProperty] private bool _battleActive;
    [ObservableProperty] private string _goalTitle = "";
    [ObservableProperty] private string _goalSub = "";
    [ObservableProperty] private string? _goalImage;
    [ObservableProperty] private int _aiHandCount;
    [ObservableProperty] private string _humanProgress = "";
    [ObservableProperty] private string _aiProgress = "";
    [ObservableProperty] private bool _canEndTurn;
    [ObservableProperty] private bool _canPass;
    [ObservableProperty] private string _resultText = "";

    public ObservableCollection<DuelCardVM> Hand { get; } = new();
    public ObservableCollection<CardEntry> HumanDeployed { get; } = new();
    public ObservableCollection<CardEntry> AiDeployed { get; } = new();
    public ObservableCollection<int> AiBacks { get; } = new();   // face-down opponent hand
    public ObservableCollection<string> LogLines { get; } = new();

    public async Task InitializeAsync()
    {
        if (!_dataReady)
        {
            await _data.EnsureLoadedAsync();
            _dataReady = true;
            if (!Preferences.Get("duel_help_seen", false)) OpenHelp();
        }
        await _store.EnsureLoadedAsync();
        RebuildMyDecks();
    }

    void RebuildMyDecks()
    {
        MyDecks.Clear();
        foreach (var d in _store.Decks)
        {
            var cards = Resolve(d.CardNumbers.ToArray());
            var mm = d.MastermindNumber is null ? null : _data.Cards.FirstOrDefault(c => c.Number == d.MastermindNumber);
            var check = DeckRules.Check(cards, mm);
            MyDecks.Add(new DeckSummaryVM
            {
                Deck = d,
                Valid = check.Valid,
                Subtitle = $"{cards.Count}/50 · " + (check.Valid ? "ready" : "not legal yet"),
                MastermindLabel = mm is null ? "" : $"Mastermind: {mm.Name}",
            });
        }
        HasMyDecks = MyDecks.Count > 0;
    }

    List<CardEntry> Resolve(string[] numbers) =>
        numbers.Select(n => _data.Cards.FirstOrDefault(c => c.Number == n))
               .Where(c => c is not null).Select(c => c!).ToList();

    void StartDuel(string humanName, List<CardEntry> human, string aiName, List<CardEntry> aiCards)
    {
        _duel = new Duel(humanName, human, aiName, aiCards, Environment.TickCount);
        AiName = aiName;
        ShowSetup = false; ShowResult = false; ShowBoard = true;
        _duel.Start();
        Refresh();
    }

    [RelayCommand]
    private void ChooseDeck(StarterDecks.Deck deck)
    {
        var ai = StarterDecks.All.First(d => d.Name != deck.Name);
        StartDuel("You", Resolve(deck.Numbers), ai.Name, Resolve(ai.Numbers));
    }

    [RelayCommand]
    private async Task ChooseSavedDeck(DeckSummaryVM? s)
    {
        if (s is null) return;
        if (!s.Valid)
        {
            await (Shell.Current?.DisplayAlert("Deck not ready",
                "This deck isn't legal yet (needs exactly 50 valid cards). Edit it in My Decks first.", "OK") ?? Task.CompletedTask);
            return;
        }
        var ai = StarterDecks.IronFangs;
        StartDuel("You", Resolve(s.Deck.CardNumbers.ToArray()), ai.Name, Resolve(ai.Numbers));
    }

    [RelayCommand]
    private async Task OpenDeckManager() => await Shell.Current.GoToAsync(nameof(DeckManagerPage));

    [RelayCommand]
    private void TapCard(DuelCardVM vm)
    {
        if (_duel is null || !vm.Actionable) return;
        if (_duel.Phase == DuelPhase.HumanMain) _duel.PlayGoal(vm.Card);
        else if (_duel.Phase == DuelPhase.HumanDeploy) _duel.DeployCard(vm.Card);
        Refresh();
    }

    [RelayCommand]
    private void EndTurn() { if (_duel is null) return; _duel.EndTurn(); Refresh(); }

    [RelayCommand]
    private void Pass() { if (_duel is null) return; _duel.PassDeploy(); Refresh(); }

    [RelayCommand]
    private void PlayAgain() { ShowResult = false; ShowBoard = false; ShowSetup = true; }

    void Refresh()
    {
        if (_duel is null) return;
        HumanVp = _duel.Human.Vp; AiVp = _duel.Ai.Vp;

        // opponent's face-down hand
        AiHandCount = _duel.Ai.Hand.Count;
        AiBacks.Clear();
        for (int i = 0; i < _duel.Ai.Hand.Count; i++) AiBacks.Add(i);

        LogLines.Clear();
        foreach (var l in _duel.Log) LogLines.Add(l);

        BattleActive = _duel.BattleActive;
        HumanDeployed.Clear(); AiDeployed.Clear();
        if (_duel.BattleActive && _duel.Goal is not null)
        {
            foreach (var c in _duel.HumanSide.Cards) HumanDeployed.Add(c);
            foreach (var c in _duel.AiSide.Cards) AiDeployed.Add(c);
            GoalTitle = _duel.Goal.Name;
            GoalImage = _duel.Goal.ImageName;
            GoalSub = _duel.Kind switch
            {
                BattleKind.Facility => $"Facility · build {_duel.Cp} CON / block {_duel.Bp} STR · {(_duel.HumanIsOwner ? "you build" : "you block")}",
                BattleKind.MilMission => $"Military mission · clear {_duel.Cp} MIL · VP {_duel.Goal.VpSelf()}/{_duel.Goal.VpOpp()}",
                _ => $"Mission · clear {_duel.Cp} STR · VP {_duel.Goal.VpSelf()}/{_duel.Goal.VpOpp()}",
            };
            HumanProgress = SideText(true);
            AiProgress = SideText(false);
        }
        else { GoalTitle = ""; GoalSub = ""; GoalImage = null; HumanProgress = ""; AiProgress = ""; }

        // hand + context buttons
        Hand.Clear();
        var playableGoals = _duel.PlayableGoals();
        var legal = _duel.LegalDeploys();
        foreach (var c in _duel.Human.Hand)
        {
            bool actionable = _duel.Phase == DuelPhase.HumanMain
                ? playableGoals.Contains(c)
                : _duel.Phase == DuelPhase.HumanDeploy && legal.Contains(c);
            Hand.Add(MakeCard(c, actionable));
        }

        CanEndTurn = _duel.Phase == DuelPhase.HumanMain;
        CanPass = _duel.Phase == DuelPhase.HumanDeploy;

        Status = _duel.Phase switch
        {
            DuelPhase.HumanMain => playableGoals.Count > 0
                ? "Your turn — start a battle by playing a Mission (you'll then deploy characters to win it). Or End Turn to draw fresh cards."
                : "Your turn — you can't start a battle yet (need a Leader + a Mission/Facility in hand). Tap End Turn to discard and draw.",
            DuelPhase.HumanDeploy => _duel.HumanIsOwner
                ? "Deploy characters to reach the clear value and win this battle — or Pass."
                : "Opponent started this battle — deploy to contest/steal it, or Pass to let it resolve.",
            _ => "",
        };

        if (_duel.Phase == DuelPhase.Over)
        {
            ShowBoard = false; ShowResult = true;
            bool won = _duel.Result == "You";
            ResultText = won ? $"Victory!  {HumanVp}–{AiVp}" : $"Defeat.  {HumanVp}–{AiVp}";
        }
    }

    DuelCardVM MakeCard(CardEntry c, bool actionable)
    {
        string line1, line2;
        string links = c.Links.Count > 0 ? "Links " + string.Join(" ", c.Links) : "no links";

        if (c.Type == "Mission")
        {
            line1 = $"Clear {c.Cp} {c.Cbtype}";
            line2 = $"VP {c.VpSelf()}/{c.VpOpp()}";
        }
        else if (c.Type == "Facilities")
        {
            line1 = $"Build {c.Cp} CON";
            line2 = $"Block {c.Bp} STR";
        }
        else if (actionable && _duel!.Phase == DuelPhase.HumanDeploy)
        {
            // show how much this card adds toward the CURRENT goal
            bool owner = _duel.HumanIsOwner;
            (int val, string unit) = _duel.Kind switch
            {
                BattleKind.StrMission => (c.Str ?? 0, "STR"),
                BattleKind.MilMission => (c.Mil ?? 0, "MIL"),
                _ => owner ? (c.Con ?? 0, "CON") : (c.Str ?? 0, "STR"),
            };
            line1 = $"+{val} {unit}";
            line2 = links;
        }
        else
        {
            // base character stats, clearly labelled
            var sp = new List<string>();
            if (c.Str is > 0) sp.Add($"STR {c.Str}");
            if (c.Mil is > 0) sp.Add($"MIL {c.Mil}");
            if (c.Con is > 0) sp.Add($"CON {c.Con}");
            line1 = sp.Count > 0 ? string.Join(" · ", sp) : c.Type;
            line2 = links;
        }
        return new DuelCardVM { Card = c, Actionable = actionable, Line1 = line1, Line2 = line2 };
    }

    string SideText(bool human)
    {
        var side = human ? _duel!.HumanSide : _duel!.AiSide;
        string who = human ? "You" : AiName;
        bool owner = human == _duel.HumanIsOwner;
        int val = _duel.Kind switch
        {
            BattleKind.StrMission => side.Str,
            BattleKind.MilMission => side.Mil,
            _ => owner ? side.Con : side.Str,
        };
        string unit = _duel.Kind switch
        {
            BattleKind.StrMission => "STR",
            BattleKind.MilMission => "MIL",
            _ => owner ? "CON" : "STR",
        };
        return $"{who}: {val} {unit}";
    }
}
