using System.Numerics;
using StickAndPuck.AI;
using StickAndPuck.Core;
using StickAndPuck.Goalie;
using StickAndPuck.Lines;
using StickAndPuck.Physics;
using StickAndPuck.Players;
using StickAndPuck.Rules;

namespace StickAndPuck.Fights;

/// <summary>
/// SAP_SCENE=fighttest: scripted fights under the match rules, the bench, the penalty box and the
/// referee, then full AI matches, no window; prints one line per check and exits with 0 when all
/// pass, 1 otherwise. Covers the W15 "done when": a fight can start, be won by timing, and sends the
/// loser to the box; and W48's: each preset's fight penalty (Arcade the loser's minor, NHL coincidental
/// majors, SHL majors and game misconducts that take both fighters off the squad).
/// </summary>
public sealed class FightSelfTest : IScene
{
    const float Period = 600;
    int failures;

    /// <inheritdoc/>
    public string Name => "fighttest";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var shl = Scenes.DefaultSetup(clubs) with { PeriodSeconds = Period };
        var setup = shl.WithRules(RuleSet.Arcade);   // W15's checks: the loser's two minutes
        World Make(int period = 1, Settings? options = null, RuleSet? rules = null)
        {
            var w = new World(rules == null ? setup : shl.WithRules(rules), options ?? settings);
            Scenes.AddStarters(w);
            MatchFlow.Begin(w, log: false);
            if (period > 1) MatchFlow.StartPeriod(w, period, Period);
            Run(w, x => x.Match.Phase == MatchPhase.Play, null, 15);
            return w;
        }

        Penalties.Strictness = 0;     // the scripted hits must not be called as slashing or tripping first
        HardCheckStartsFight(Make());
        SoftCheckOrDecline(Make(), Make());
        HumanAccepts(Make());
        WonByTiming(Make());
        BadTimingLoses(Make());
        BlockTiming(Make(), Make());
        BrokenUp(Make());
        NoFight(Make(4), Make(options: settings with { Fights = false }), Make());
        ArcadePenalty(Make(rules: RuleSet.Arcade));
        MajorsStay(Make(rules: RuleSet.Nhl));
        MajorsAndEjection(Make(rules: RuleSet.Shl));
        MajorsNeverShort(Make(rules: RuleSet.Shl));
        Penalties.Strictness = 1;
        foreach (var rules in new[] { RuleSet.Arcade, RuleSet.Shl, RuleSet.Nhl }) FullMatches(shl.WithRules(rules), settings);

        Console.WriteLine(failures == 0 ? "FIGHTTEST: all checks passed" : $"FIGHTTEST: {failures} check(s) failed");
        Environment.Exit(failures == 0 ? 0 : 1);
        return Make();
    }

    void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (!ok) failures++;
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<World, ISystem[]> Sets = new();

    /// <summary>Physics, the match rules, the bench, penalties, fights and the referee: everything but the players moving themselves.</summary>
    static ISystem[] Systems(World world)
    {
        return Sets.GetValue(world, _ => [new LinesSystem(), new PuckPhysics(), new MatchRules(), new OffsideRule(), new IcingRule(), new PenaltyRules(), new FightSystem(), new Referee.RefereeSystem()]);
    }

    static List<MatchEvent> Run(World world, Func<World, bool> until, Action<World>? each = null, float limit = 10)
    {
        var systems = Systems(world);
        var events = new List<MatchEvent>();
        for (int i = 0; i < limit / Game.Step && !until(world); i++)
        {
            world.Events.Clear();
            each?.Invoke(world);
            foreach (var s in systems) s.Update(world, Game.Step);
            events.AddRange(world.Events.Current);
            world.Time += Game.Step; world.Tick++;
        }
        return events;
    }

    static Player Skater(World w, Side side, PlayingPosition position)
    {
        return w.OnIce(side).First(p => p.Data.Position == position);
    }

    /// <summary>A hit from the away centre on the home centre, published as W4c would.</summary>
    static List<MatchEvent> Hit(World w, float strength)
    {
        var checker = Skater(w, Side.Away, PlayingPosition.C);
        var target = Skater(w, Side.Home, PlayingPosition.C);
        checker.Position = target.Position + new Vector3(0.8f, 0, 0);
        bool sent = false;
        return Run(w, _ => sent, x =>
        {
            x.Events.Publish(new MatchEvent(MatchEventKind.Check, x.Time, checker.Side, checker.Id, target.Position, strength));
            sent = true;
        }, 1);
    }

    /// <summary>Starts a fight between the away centre (checker) and the home centre, both AI unless a slot is set.</summary>
    static Fight StartFight(World w, bool? aiAccepts = true)
    {
        var s = Fighting.State(w);
        s.ForceAccept = aiAccepts;
        Fighting.Offer(w, Skater(w, Side.Away, PlayingPosition.C).Id, Skater(w, Side.Home, PlayingPosition.C).Id);
        // a human on either side accepts
        Run(w, x => Fighting.Current(x) != null, x => { if (x.Controlled(0) is { } me) me.Command = new PlayerCommand { AcceptFight = true }; }, 0.1f);
        return Fighting.Current(w)!;
    }

    /// <summary>Human slot 0's command each step: punch when the marker first passes <paramref name="at"/>, block as asked.</summary>
    static Action<World> Human(float at, Func<World, Fight, bool>? block = null)
    {
        return w =>
        {
            if (w.Controlled(0) is not { } me) return;
            var cmd = default(PlayerCommand);
            if (Fighting.Current(w) is { } f && f.Of(me.Id) is { } mine)
            {
                cmd.Punch = f.Stage == FightStage.Exchange && !mine.Punched && f.Sweep + (Game.Step / FightTuning.ExchangeSeconds) >= at;
                cmd.Block = block?.Invoke(w, f) ?? false;
            }
            me.Command = cmd;
        };
    }

    void HardCheckStartsFight(World w)
    {
        var s = Fighting.State(w);
        s.OfferChance = 1;
        s.ForceAccept = true;
        var events = Hit(w, 1.8f);
        float clock = w.Match.Clock;
        var f = Fighting.Current(w);
        var a = Skater(w, Side.Away, PlayingPosition.C);
        var h = Skater(w, Side.Home, PlayingPosition.C);
        Check(f != null && f.Checker.PlayerId == a.Id && f.Victim.PlayerId == h.Id, "a hard check (1.8) drops the gloves and both accept: the checker and the skater he hit fight");
        Check(events.Exists(e => e.Kind == MatchEventKind.Fight) && events.Exists(e => e.Kind == MatchEventKind.Whistle) && w.Match.Phase == MatchPhase.Stopped,
            "the fight publishes a Fight event and the referee whistles play dead");
        Check(a.Action == PlayerAction.Fight && h.Action == PlayerAction.Fight && Math.Abs(Vector3.Distance(a.Position, h.Position) - (2 * FightTuning.HalfGap)) < 0.01f
            && string.Equals(Fighting.Pose(w, a)?.Clip, "grab", StringComparison.Ordinal), "both square off, an arm's length apart, grabbing sweaters");

        Run(w, x => Fighting.Current(x) == null, null, 20);
        Check(Math.Abs(w.Match.Clock - clock) < 0.001f, "the clock does not run during the fight");
        Check(Math.Abs(Vector3.Distance(w.Puck.Position, f!.Spot) - 1.2f) < 0.01f, "the dead puck lies by the fight, so the camera stays on it");
    }

    void SoftCheckOrDecline(World soft, World decline)
    {
        Fighting.State(soft).OfferChance = 1;
        Fighting.State(soft).ForceAccept = true;
        Hit(soft, 1.2f);
        Check(Fighting.Current(soft) == null && Fighting.Pending(soft) == null && soft.Match.Phase == MatchPhase.Play, "a check under 1.5 never starts a fight");

        Fighting.State(decline).OfferChance = 1;
        Fighting.State(decline).ForceAccept = false;
        Hit(decline, 2f);
        Run(decline, _ => false, null, 0.1f);
        Check(Fighting.Current(decline) == null && Fighting.Pending(decline) == null && decline.Match.Phase == MatchPhase.Play && Fighting.State(decline).Declined == 1,
            "an AI who declines skates away and play goes on");
    }

    void HumanAccepts(World w)
    {
        var me = Skater(w, Side.Home, PlayingPosition.C);
        me.Slot = 0;
        var s = Fighting.State(w);
        s.ForceAccept = true;
        var them = Skater(w, Side.Away, PlayingPosition.C);
        Fighting.Offer(w, them.Id, me.Id);
        Run(w, _ => false, null, 1);
        Check(Fighting.Pending(w) != null && Fighting.Current(w) == null && w.Match.Phase == MatchPhase.Play, "an offer to a human waits for his answer while play goes on");
        Run(w, x => Fighting.Pending(x) == null, null, FightTuning.OfferSeconds + 0.5f);
        Check(Fighting.Current(w) == null && s.Declined == 1, "a human who lets the offer pass declines it");

        Fighting.Offer(w, them.Id, me.Id);
        Run(w, _ => false, null, 0.5f);
        Run(w, x => Fighting.Current(x) != null, x => x.Controlled(0)!.Command = new PlayerCommand { AcceptFight = true }, 0.1f);
        Check(Fighting.Current(w) != null, "a human who presses accept (F, or Y on a pad) fights");
    }

    void WonByTiming(World w)
    {
        var me = Skater(w, Side.Home, PlayingPosition.C);
        var them = Skater(w, Side.Away, PlayingPosition.C);
        me.Slot = 0;
        var s = Fighting.State(w);
        s.AiPunches = false;
        s.AiBlocks = false;
        var fight = StartFight(w);
        var events = Run(w, x => Fighting.Current(x) == null, Human(0.5f), 20);
        Check(fight.Knockdown && fight.LoserId == them.Id && fight.Exchange <= 3, $"punches timed in the middle of the bar knock the other fighter down (in {fight.Exchange + 1} exchanges, {fight.Victim.Landed} landed)");

        var call = w.Match.Penalties.Find(p => p.PlayerId == them.Id);
        Check(call != null && string.Equals(call.Infraction, FightTuning.Infraction, StringComparison.Ordinal) && Math.Abs(call.SecondsLeft - Penalties.MinorSeconds(w)) < 0.01f && !them.OnIce && Penalties.InBox(w, them.Id),
            "the loser goes to the box for two minutes for fighting");
        Check(events.Exists(e => e.Kind == MatchEventKind.Penalty && e.PlayerId == them.Id) && me.OnIce && !Penalties.InBox(w, me.Id) && me.Action == PlayerAction.None,
            "a Penalty event is published for him; the winner stays on the ice");
        Run(w, x => x.Match.Phase == MatchPhase.Play, null, 15);
        var dot = MatchFlow.FaceoffDot(w);
        Check(w.Match.Phase == MatchPhase.Play && Penalties.PowerPlay(w) == Side.Home && Math.Abs(dot.X) > 15 && Math.Sign(dot.X) == FaceoffFormation.DefendedEnd(w.Match, Side.Away),
            "play restarts with a faceoff in the loser's end and the winner's club on the power play");
    }

    void BadTimingLoses(World w)
    {
        var me = Skater(w, Side.Home, PlayingPosition.C);
        me.Slot = 0;
        var s = Fighting.State(w);
        s.AiBlocks = false;
        var fight = StartFight(w);
        Run(w, x => Fighting.Current(x) == null, Human(0.05f), 20);
        Check(fight.LoserId == me.Id && fight.Victim.Missed >= 1 && fight.Victim.Landed == 0,
            $"punches thrown at the edge of the bar miss, and the human loses ({fight.Victim.Missed} missed, {fight.Checker.Landed} taken)");
        var now = w.Controlled(0);
        Check(Penalties.InBox(w, me.Id) && now != null && now != me && now.OnIce && now.Side == Side.Home, "the losing human's skater goes to the box and control moves to a teammate");
    }

    void BlockTiming(World fresh, World stale)
    {
        // block just as the AI winds up: every punch is stopped
        var me = Skater(fresh, Side.Home, PlayingPosition.C);
        me.Slot = 0;
        Fighting.State(fresh).AiBlocks = false;
        var fight = StartFight(fresh);
        Run(fresh, x => Fighting.Current(x)?.Exchange >= 3, Human(2f, (x, f) => !float.IsNaN(f.Checker.LandsAt)), 10);
        Check(fight.Checker.Blocked >= 2 && fight.Checker.Landed == 0 && fight.Victim.Balance >= 1f,
            $"a guard raised during the wind-up blocks the punch ({fight.Checker.Blocked} blocked)");

        // a guard held up the whole time has dropped by the time the punch lands
        var me2 = Skater(stale, Side.Home, PlayingPosition.C);
        me2.Slot = 0;
        Fighting.State(stale).AiBlocks = false;
        var fight2 = StartFight(stale);
        Run(stale, x => Fighting.Current(x) is not { } f || f.Exchange >= 3 || f.Stage == FightStage.Over, Human(2f, (_, _) => true), 10);
        Check(fight2.Checker.Blocked == 0 && fight2.Checker.Landed >= 1 && fight2.Victim.Balance < 1f,
            $"a guard held up from the start is too old to block ({fight2.Checker.Landed} landed)");
    }

    void BrokenUp(World w)
    {
        var s = Fighting.State(w);
        s.AiPunches = false;
        var a = Skater(w, Side.Away, PlayingPosition.C);
        var fight = StartFight(w);
        Run(w, x => Fighting.Current(x) == null, null, 20);
        Check(fight.Exchange == FightTuning.MaxExchanges && !fight.Knockdown && fight.LoserId == a.Id && Penalties.InBox(w, a.Id),
            "with nobody down after the last exchange the linesmen break it up; on even balance the checker who started it goes to the box");
    }

    void NoFight(World overtime, World off, World w)
    {
        Fighting.State(overtime).ForceAccept = true;
        Check(!Fighting.Offer(overtime, Skater(overtime, Side.Away, PlayingPosition.C).Id, Skater(overtime, Side.Home, PlayingPosition.C).Id), "no fights in overtime");
        Fighting.State(off).ForceAccept = true;
        Check(!Fighting.Offer(off, Skater(off, Side.Away, PlayingPosition.C).Id, Skater(off, Side.Home, PlayingPosition.C).Id), "no fights when fights are off in the settings");

        var goalie = w.OnIce(Side.Home).First(p => p.IsGoalie);
        Check(!Fighting.Offer(w, Skater(w, Side.Away, PlayingPosition.C).Id, goalie.Id), "goalies never fight");
        // two home skaters already in the box: a fight could put home three short
        var book = Penalties.Book(w);
        Penalties.Assess(w, book, new DelayedPenalty(Side.Home, Skater(w, Side.Home, PlayingPosition.LW).Id, Infraction.Tripping, w.Time));
        Penalties.Assess(w, book, new DelayedPenalty(Side.Home, Skater(w, Side.Home, PlayingPosition.RW).Id, Infraction.Hooking, w.Time));
        Check(!Fighting.Offer(w, Skater(w, Side.Away, PlayingPosition.C).Id, Skater(w, Side.Home, PlayingPosition.C).Id), "no fight when a club already serves two penalties");
    }

    /// <summary>A fight the home centre wins by knockdown; returns the events from the gloves dropping to the end.</summary>
    static List<MatchEvent> HomeWins(World w)
    {
        var me = Skater(w, Side.Home, PlayingPosition.C);
        me.Slot = 0;
        var s = Fighting.State(w);
        s.AiPunches = false;
        s.AiBlocks = false;
        StartFight(w);
        return Run(w, x => Fighting.Current(x) == null, Human(0.5f), 20);
    }

    static int Skaters(World w, Side side)
    {
        return w.OnIce(side).Count(p => !p.IsGoalie);
    }

    static PenaltyState? Fighting5(World w, Player p)
    {
        return w.Match.Penalties.Find(x => x.PlayerId == p.Id && string.Equals(x.Infraction, FightTuning.Infraction, StringComparison.Ordinal)
            && Math.Abs(x.SecondsLeft - Penalties.ClubSeconds(w, Severity.Major)) < 0.01f);
    }

    void ArcadePenalty(World w)
    {
        var me = Skater(w, Side.Home, PlayingPosition.C);
        var them = Skater(w, Side.Away, PlayingPosition.C);
        HomeWins(w);
        Run(w, x => x.Match.Phase == MatchPhase.Play, null, 15);
        var call = w.Match.Penalties.Find(p => p.PlayerId == them.Id);
        Check(call != null && Math.Abs(call.SecondsLeft - Penalties.MinorSeconds(w)) < 0.01f && w.Match.Penalties.Count == 1 && me.OnIce
            && Penalties.PowerPlay(w) == Side.Home && !Penalties.Ejected(w, them.Id),
            "Arcade: the loser gets two minutes, the winner stays on and his club has the power play");
    }

    void MajorsStay(World w)
    {
        var me = Skater(w, Side.Home, PlayingPosition.C);
        var them = Skater(w, Side.Away, PlayingPosition.C);
        var events = HomeWins(w);
        Check(Fighting5(w, me) != null && Fighting5(w, them) != null && Penalties.InBox(w, me.Id) && Penalties.InBox(w, them.Id) && !me.OnIce && !them.OnIce
            && events.Count(e => e.Kind == MatchEventKind.Penalty) == 2 && !Penalties.Ejected(w, me.Id) && !Penalties.Ejected(w, them.Id),
            "NHL: both fighters get a five minute major for fighting, winner and loser, and neither is ejected");
        Run(w, x => x.Match.Phase == MatchPhase.Play, null, 15);
        Check(Skaters(w, Side.Home) == 5 && Skaters(w, Side.Away) == 5 && Penalties.PowerPlay(w) == null,
            "NHL: the majors are coincidental and substituted, so play goes on five on five");
        var homeSquad = Bench.SquadOf(w, Side.Home)!;
        var awaySquad = Bench.SquadOf(w, Side.Away)!;
        Run(w, x => x.Match.Penalties.Count == 0, x => { if (x.Tick % 180 == 0) { Bench.RequestChange(x, Side.Home); Bench.RequestChange(x, Side.Away); } }, 400);
        Check(w.Match.Penalties.Count == 0 && !Penalties.InBox(w, me.Id) && !Penalties.InBox(w, them.Id) && homeSquad.UnitOf(me) != null && awaySquad.UnitOf(them) != null,
            "NHL: when the five minutes are up both fighters leave the box and stay in their lines");
    }

    void MajorsAndEjection(World w)
    {
        var me = Skater(w, Side.Home, PlayingPosition.C);
        var them = Skater(w, Side.Away, PlayingPosition.C);
        var homeSquad = Bench.SquadOf(w, Side.Home)!;
        var awaySquad = Bench.SquadOf(w, Side.Away)!;
        var events = HomeWins(w);
        Check(Fighting5(w, me) != null && Fighting5(w, them) != null && events.Count(e => e.Kind == MatchEventKind.Penalty) == 2,
            "SHL: both fighters get a five minute major for fighting, winner and loser, on the scorebug");
        Check(Penalties.Ejected(w, me.Id) && Penalties.Ejected(w, them.Id) && homeSquad.UnitOf(me) == null && awaySquad.UnitOf(them) == null && !me.OnIce && !them.OnIce,
            "SHL: both also get a game misconduct and are off the squad, the lines refill without them");
        Run(w, x => x.Match.Phase == MatchPhase.Play, null, 15);
        Check(Skaters(w, Side.Home) == 5 && Skaters(w, Side.Away) == 5 && Penalties.PowerPlay(w) == null,
            "SHL: the majors are coincidental and substituted, so play goes on five on five");
        bool back = false;
        Run(w, x => x.Match.Penalties.Count == 0, x =>
        {
            back |= me.OnIce || them.OnIce;
            if (x.Tick % 180 == 0) { Bench.RequestChange(x, Side.Home); Bench.RequestChange(x, Side.Away); }
        }, 400);
        Check(w.Match.Penalties.Count == 0 && !back && !me.OnIce && !them.OnIce && Skaters(w, Side.Home) == 5 && Skaters(w, Side.Away) == 5,
            "SHL: when the five minutes are up neither fighter comes back, both clubs play five on five");
    }

    void MajorsNeverShort(World w)
    {
        // two home skaters already serve: substituted majors cannot put home three short, so the fight may go on
        var book = Penalties.Book(w);
        Penalties.Assess(w, book, new DelayedPenalty(Side.Home, Skater(w, Side.Home, PlayingPosition.LW).Id, Infraction.Tripping, w.Time));
        Penalties.Assess(w, book, new DelayedPenalty(Side.Home, Skater(w, Side.Home, PlayingPosition.RW).Id, Infraction.Hooking, w.Time));
        Fighting.State(w).ForceAccept = true;
        Check(Fighting.Offer(w, Skater(w, Side.Away, PlayingPosition.C).Id, Skater(w, Side.Home, PlayingPosition.C).Id),
            "SHL: a club serving two penalties can still fight, since coincidental majors never leave it shorter");
    }

    void FullMatches(MatchSetup setup, Settings settings)
    {
        int perFight = setup.Rules.FightPenalty == FightPenalty.LoserMinor ? 1 : 2;
        int fights = 0, boxed = 0, broken = 0, open = 0;
        foreach (int seed in Seeds)
        {
            var (f, b, x, o) = Match(setup with { PeriodSeconds = 600 }, settings, seed);
            fights += f; boxed += b; broken += x; open += o;
        }
        Check(fights >= 1 && boxed == fights * perFight && broken == 0 && open == 0,
            $"{setup.Rules.Preset}: AI matches of 3 x 10:00: {fights} fight(s) in {Seeds.Length} matches, {boxed} fighting penalties ({perFight} a fight), clubs short by exactly their penalties");
    }

    static readonly int[] Seeds = [2, 3, 4, 5];

    static (int Fights, int Boxed, int Broken, int Open) Match(MatchSetup setup, Settings settings, int seed)
    {
        var w = new World(setup with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] }, settings);
        Scenes.AddStarters(w);
        MatchFlow.Begin(w, log: false);
#pragma warning disable S2245 // fixed seeds so the checks repeat
        Penalties.Book(w).Rng = new Random(seed);
        Fighting.State(w).Rng = new Random(seed);
        ISystem[] systems =
        [
            new PositioningSystem(new Random(seed)), new DecisionSystem(new Random(seed)), new LinesSystem(), new SkatingSystem(), new CheckingSystem(new Random(seed)),
            new PuckHandlingSystem(new Random(seed)), new GoalieSystem { Dice = new Random(seed) }, new PuckPhysics(), new MatchRules(), new OffsideRule(),
            new IcingRule(), new PenaltyRules(), new FightSystem(), new Referee.RefereeSystem(),
        ];
#pragma warning restore S2245
        foreach (var s in systems) if (s is PositioningSystem or DecisionSystem or LinesSystem or SkatingSystem or CheckingSystem or PuckHandlingSystem) s.Start(w);
        int boxed = 0, broken = 0;
        for (int i = 0; i < 4000 * 60 && w.Match.Phase is not (MatchPhase.Final or MatchPhase.Overtime or MatchPhase.Shootout); i++)
        {
            w.Events.Clear();
            foreach (var s in systems) s.Update(w, Game.Step);
            w.Time += Game.Step; w.Tick++;
            foreach (var e in w.Events.Of(MatchEventKind.Penalty))
                if (string.Equals(w.Match.Penalties.FindLast(p => p.PlayerId == e.PlayerId)?.Infraction, FightTuning.Infraction, StringComparison.Ordinal)) boxed++;
            // strength is checked in play: between a whistle and the faceoff the bench is still filling the gaps (as W47's penaltytest does)
            if (w.Match.Phase != MatchPhase.Play) continue;
            foreach (var side in new[] { Side.Home, Side.Away })
                if (w.OnIce(side).Count(p => !p.IsGoalie) != Penalties.TargetSkaters(w, side)) broken++;
        }
        return (Fighting.Started(w), boxed, broken, Fighting.Current(w) != null ? 1 : 0);
    }
}
