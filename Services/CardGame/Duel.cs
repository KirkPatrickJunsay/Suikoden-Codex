using SuikodenCodex.Models;

namespace SuikodenCodex.Services.CardGame;

public enum DuelPhase { HumanMain, HumanDeploy, Over }
public enum BattleKind { StrMission, MilMission, Facility }

static class CardX
{
    public static bool IsLeader(this CardEntry c) => c.Type == "Leader";
    public static bool IsCharacter(this CardEntry c) =>
        c.Type is "Leader" or "Commoner" or "Craftman" or "Free";
    public static bool IsGoal(this CardEntry c) => c.Type is "Mission" or "Facilities";
    public static int S(this CardEntry c) => c.Str ?? 0;
    public static int M(this CardEntry c) => c.Mil ?? 0;
    public static int Cn(this CardEntry c) => c.Con ?? 0;
    public static int VpSelf(this CardEntry c) => ParseVp(c.Vp).self;
    public static int VpOpp(this CardEntry c) => ParseVp(c.Vp).opp;
    static (int self, int opp) ParseVp(string? vp)
    {
        if (string.IsNullOrWhiteSpace(vp)) return (1, 1);
        var p = vp.Split('/');
        int s = int.TryParse(p[0], out var a) ? a : 1;
        int o = p.Length > 1 && int.TryParse(p[1], out var b) ? b : s;
        return (s, o);
    }
}

public class DuelPlayer
{
    public string Name = "";
    public bool IsHuman;
    public List<CardEntry> Deck = new(), Hand = new(), Discard = new();
    public int Vp;
    public int FacilitiesBuilt;
    public int HandLimit = 6;
    public bool Draw() { if (Deck.Count == 0) return false; Hand.Add(Deck[^1]); Deck.RemoveAt(Deck.Count - 1); return true; }
    public void Replenish() { while (Hand.Count < HandLimit && Draw()) { } }
}

public class BattleSide
{
    public List<CardEntry> Cards = new();
    public HashSet<string> Links = new();
    public int Str => Cards.Sum(c => c.S());
    public int Mil => Cards.Sum(c => c.M());
    public int Con => Cards.Sum(c => c.Cn());
}

/// <summary>Interactive Card Stories duel: human (player 0) vs AI (player 1). UI drives it via the public methods.</summary>
public class Duel
{
    public const int VpGoal = 5;

    public DuelPlayer Human = new() { IsHuman = true };
    public DuelPlayer Ai = new() { Name = "Opponent" };
    DuelPlayer[] P;
    readonly Random R;

    public DuelPhase Phase { get; private set; }
    public string? Result { get; private set; }
    public List<string> Log { get; } = new();

    // battle state
    public bool BattleActive { get; private set; }
    public CardEntry? Goal { get; private set; }
    public BattleKind Kind { get; private set; }
    public int Cp { get; private set; }
    public int Bp { get; private set; }
    public BattleSide HumanSide { get; private set; } = new();
    public BattleSide AiSide { get; private set; } = new();
    int _ownerIdx;
    int _deployActor;
    int _passes;
    int _turn;        // whose Main Phase

    public bool HumanIsOwner => _ownerIdx == 0;

    public Duel(string humanName, List<CardEntry> humanDeck, string aiName, List<CardEntry> aiDeck, int seed)
    {
        Human.Name = humanName; Human.Deck = new(humanDeck);
        Ai.Name = aiName; Ai.Deck = new(aiDeck);
        P = new[] { Human, Ai };
        R = new Random(seed);
    }

    public void Start()
    {
        foreach (var p in P) { Shuffle(p.Deck); for (int i = 0; i < p.HandLimit; i++) p.Draw(); }
        _turn = 0; // human first (rules: no advantage)
        Note("A new duel begins. You hold the first move.");
        Advance();
    }

    void Shuffle(List<CardEntry> l) { for (int i = l.Count - 1; i > 0; i--) { int j = R.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); } }
    void Note(string s) { Log.Add(s); if (Log.Count > 12) Log.RemoveAt(0); }
    BattleSide SideOf(int idx) => idx == 0 ? HumanSide : AiSide;
    BattleSide OwnerSide => SideOf(_ownerIdx);

    // ---------------- driver ----------------
    void Advance()
    {
        for (int guard = 0; guard < 1000; guard++)
        {
            if (Human.Vp >= VpGoal) { Finish(Human); return; }
            if (Ai.Vp >= VpGoal) { Finish(Ai); return; }

            if (BattleActive)
            {
                if (P[_deployActor].IsHuman) { Phase = DuelPhase.HumanDeploy; return; }
                // AI deploy turn
                var pick = AiDeploy(_deployActor);
                ApplyDeployOrPass(_deployActor, pick);
                continue;
            }

            // main phase
            if (P[_turn].IsHuman) { Phase = DuelPhase.HumanMain; return; }
            AiMainPhase();
        }
        Finish(Human.Vp >= Ai.Vp ? Human : Ai); // safety
    }

    void Finish(DuelPlayer w) { Phase = DuelPhase.Over; Result = w.Name; Note($"{w.Name} wins the duel {Human.Vp}-{Ai.Vp}!"); }

    // ---------------- human actions ----------------
    public IReadOnlyList<CardEntry> PlayableGoals()
    {
        if (Phase != DuelPhase.HumanMain) return Array.Empty<CardEntry>();
        if (!Human.Hand.Any(c => c.IsLeader())) return Array.Empty<CardEntry>();
        return Human.Hand.Where(c => c.IsGoal() && c.Cp.HasValue).ToList();
    }

    public void PlayGoal(CardEntry goal)
    {
        if (Phase != DuelPhase.HumanMain) return;
        Human.Hand.Remove(goal);
        StartBattle(0, goal);
        Advance();
    }

    public void EndTurn()
    {
        if (Phase != DuelPhase.HumanMain) return;
        Dig(Human);
        EndOfTurn(0);
        Advance();
    }

    public IReadOnlyList<CardEntry> LegalDeploys()
    {
        if (Phase != DuelPhase.HumanDeploy) return Array.Empty<CardEntry>();
        return Legal(Human, HumanSide);
    }

    public void DeployCard(CardEntry c)
    {
        if (Phase != DuelPhase.HumanDeploy) return;
        ApplyDeployOrPass(0, c);
        Advance();
    }

    public void PassDeploy()
    {
        if (Phase != DuelPhase.HumanDeploy) return;
        ApplyDeployOrPass(0, null);
        Advance();
    }

    // ---------------- battle plumbing ----------------
    void StartBattle(int ownerIdx, CardEntry goal)
    {
        BattleActive = true; Goal = goal; _ownerIdx = ownerIdx;
        Cp = goal.Cp ?? 0; Bp = goal.Bp ?? 0;
        Kind = goal.Type == "Facilities" ? BattleKind.Facility
             : (goal.Cbtype == "MIL" ? BattleKind.MilMission : BattleKind.StrMission);
        HumanSide = new(); AiSide = new();
        _passes = 0;
        _deployActor = 1 - ownerIdx; // opponent deploys first
        string who = ownerIdx == 0 ? "You play" : $"{Ai.Name} plays";
        string k = Kind == BattleKind.Facility ? $"facility (build {Cp} CON / block {Bp})"
                 : Kind == BattleKind.MilMission ? $"MIL mission (clear {Cp})"
                 : $"mission (clear {Cp} STR)";
        Note($"{who} {goal.Name} — {k}.");
    }

    List<CardEntry> Legal(DuelPlayer p, BattleSide s)
    {
        if (s.Cards.Count == 0) return p.Hand.Where(c => c.IsLeader()).ToList();
        return p.Hand.Where(c => c.IsCharacter() && c.Links.Any(s.Links.Contains)).ToList();
    }

    void DoDeploy(int actor, CardEntry c)
    {
        var p = P[actor]; var s = SideOf(actor);
        p.Hand.Remove(c); s.Cards.Add(c);
        foreach (var l in c.Links) s.Links.Add(l);
    }

    // contribution stat for an actor in the current battle
    Func<CardEntry, int> StatFor(int actor) => Kind switch
    {
        BattleKind.StrMission => c => c.S(),
        BattleKind.MilMission => c => c.M(),
        _ => actor == _ownerIdx ? (Func<CardEntry, int>)(c => c.Cn()) : (c => c.S()),
    };
    int CurFor(int actor) => Kind switch
    {
        BattleKind.StrMission => SideOf(actor).Str,
        BattleKind.MilMission => SideOf(actor).Mil,
        _ => actor == _ownerIdx ? SideOf(actor).Con : SideOf(actor).Str,
    };
    int TargetFor(int actor) => (Kind == BattleKind.Facility && actor != _ownerIdx) ? Bp : Cp;

    void ApplyDeployOrPass(int actor, CardEntry? pick)
    {
        if (pick == null) { _passes++; Note($"{P[actor].Name} passes."); }
        else { _passes = 0; DoDeploy(actor, pick); Note($"{P[actor].Name} deploys {pick.Name} ({StatFor(actor)(pick)})."); }

        // immediate resolution checks
        if (pick != null)
        {
            if (Kind == BattleKind.StrMission && SideOf(actor).Str >= Cp) { FinishBattle(actor); return; }
            if (Kind == BattleKind.Facility)
            {
                if (actor == _ownerIdx && OwnerSide.Con >= Cp) { FinishBattle(_ownerIdx); return; }
                if (actor != _ownerIdx && SideOf(actor).Str >= Bp) { FinishBattle(actor); return; } // blocker destroys
            }
        }
        if (_passes >= 2)
        {
            if (Kind == BattleKind.MilMission)
            {
                int a = HumanSide.Mil, b = AiSide.Mil;
                int win = a == b ? -1 : (a > b ? 0 : 1);
                int rem = Math.Abs(a - b);
                FinishBattle(win >= 0 && rem >= Cp ? win : -1);
            }
            else FinishBattle(-1);
            return;
        }
        _deployActor = 1 - _deployActor;
    }

    void FinishBattle(int clearer)
    {
        var goal = Goal!;
        if (Kind == BattleKind.Facility)
        {
            if (clearer == _ownerIdx)
            {
                P[_ownerIdx].FacilitiesBuilt++;
                Note($"{P[_ownerIdx].Name} builds {goal.Name}.");
                if (P[_ownerIdx].FacilitiesBuilt % 2 == 0) { P[_ownerIdx].Vp++; Note($"{P[_ownerIdx].Name} gains 1 VP from facilities."); }
            }
            else if (clearer >= 0) Note($"{P[clearer].Name} destroys {goal.Name}.");
            else Note($"{goal.Name} goes unresolved.");
        }
        else
        {
            if (clearer >= 0)
            {
                int gain = clearer == _ownerIdx ? goal.VpSelf() : goal.VpOpp();
                P[clearer].Vp += gain;
                Note($"{P[clearer].Name} clears {goal.Name} for {gain} VP.");
            }
            else Note($"{goal.Name} goes unresolved.");
        }

        // discard mission + all deployed
        P[_ownerIdx].Discard.Add(goal);
        foreach (var c in HumanSide.Cards) Human.Discard.Add(c);
        foreach (var c in AiSide.Cards) Ai.Discard.Add(c);
        BattleActive = false; Goal = null;
        HumanSide = new(); AiSide = new();

        EndOfTurn(_ownerIdx);
    }

    void EndOfTurn(int actorTurn)
    {
        Human.Replenish(); Ai.Replenish();
        if (Human.Deck.Count == 0 && Human.Hand.Count < Human.HandLimit) { Finish(Ai); return; }
        if (Ai.Deck.Count == 0 && Ai.Hand.Count < Ai.HandLimit) { Finish(Human); return; }
        _turn = 1 - actorTurn;
    }

    void Dig(DuelPlayer p)
    {
        var junk = p.Hand.Where(c => !c.IsLeader() && !c.IsGoal()).Take(3).ToList();
        if (junk.Count == 0) junk = p.Hand.Take(3).ToList();
        foreach (var c in junk) { p.Hand.Remove(c); p.Discard.Add(c); }
        Note($"{p.Name} regroups (discards {junk.Count}).");
    }

    // ---------------- AI ----------------
    void AiMainPhase()
    {
        if (Ai.Hand.Any(c => c.IsLeader()))
        {
            var goal = Ai.Hand.Where(c => c.IsGoal() && c.Cp.HasValue)
                .OrderBy(c => c.Type == "Mission" && c.Cbtype == "STR" ? 0 : c.Type == "Facilities" ? 1 : 2)
                .ThenBy(c => c.Cp ?? 999).FirstOrDefault();
            if (goal != null) { Ai.Hand.Remove(goal); StartBattle(1, goal); return; }
        }
        Dig(Ai);
        EndOfTurn(1);
    }

    CardEntry? AiDeploy(int actor)
    {
        // AI cedes contested MIL missions for now (smarter contesting is a later pass)
        if (Kind == BattleKind.MilMission && actor != _ownerIdx) return null;

        var legal = Legal(Ai, SideOf(actor));
        if (legal.Count == 0) return null;
        var stat = StatFor(actor);
        int cur = CurFor(actor), target = TargetFor(actor);

        var clinch = legal.Where(c => cur + stat(c) >= target).OrderBy(c => stat(c)).FirstOrDefault();
        if (clinch != null) return clinch;

        var best = legal.OrderByDescending(stat).ThenByDescending(c => c.Links.Count).First();
        bool mustOpen = SideOf(actor).Cards.Count == 0;
        return (stat(best) > 0 || mustOpen) ? best : null;
    }
}
