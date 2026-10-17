using System.Globalization;
using System.Numerics;
using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Fights;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.App.Referee;
using StickAndPuck.App.Rules;
using StickAndPuck.Tests.Support;
using TUnit.Core.Enums;

namespace StickAndPuck.Tests;

/// <summary>
/// Scripted fights under the match rules, the bench, the penalty box and the referee, then full AI
/// matches. Covers that a fight can start, be won by timing, and sends the loser to the box; and
/// Each preset's fight penalty (Arcade the loser's minor, NHL coincidental majors, SHL majors
/// and game misconducts that take both fighters off the squad). Formerly SAP_SCENE=fighttest.
/// </summary>
public sealed class FightTests : CheckedTest
{
    const float Period = 600;

    /// <summary>
    /// A match in play with 10:00 periods, under Arcade unless other rules are given (for the checks of
    /// the loser's two minutes). The scripted hits must not be called as slashing or tripping first,
    /// so it turns the referee's strictness down to 0 before building, as the scene did.
    /// </summary>
    /// <summary>Gets the fight on the ice, if any.</summary>
    static Maybe<Fight> Current(World w) => Fighting.Current(w);

    /// <summary>Gets the offer waiting for an answer, if any.</summary>
    static Maybe<FightOffer> Pending(World w) => Fighting.Pending(w);

    static T Must<T>(Maybe<T> m)
        where T : notnull => m.TryGet(out var v) ? v : throw new InvalidOperationException("expected a value");

    static World Make(int period = 1, Maybe<RuleSet> rules = default) => Make(period, s => s, rules);

    static World Make(int period, Func<Settings, Settings> options, Maybe<RuleSet> rules = default)
    {
        return new WorldBuilder()
            .Setup(s => s with { PeriodSeconds = Period })
            .Switches(s => s.PenaltyStrictness = 0)
            .Rules(rules.Or(RuleSet.Arcade))
            .Settings(options)
            .Then(w =>
            {
                if (period > 1) MatchFlow.StartPeriod(w, period, Period);
                Run(w, x => x.Match.Phase == MatchPhase.Play, 15);
            })
            .Build();
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<World, ISystem[]> Sets = new();

    /// <summary>Physics, the match rules, the bench, penalties, fights and the referee: everything but the players moving themselves.</summary>
    static ISystem[] Systems(World world)
    {
        return Sets.GetValue(world, _ => [new LinesSystem(), new PuckPhysics(), new MatchRules(), new OffsideRule(), new IcingRule(), new PenaltyRules(), new FightSystem(), new RefereeSystem()]);
    }

    static List<MatchEvent> Run(World world, Func<World, bool> until, float limit = 10) => Run(world, until, _ => { }, limit);

    static List<MatchEvent> Run(World world, Func<World, bool> until, Action<World> each, float limit = 10)
    {
        var systems = Systems(world);
        var events = new List<MatchEvent>();
        for (int i = 0; i < limit / Game.Step && !until(world); i++)
        {
            world.Events.Clear();
            each(world);
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

    /// <summary>A hit from the away centre on the home centre, published as checking would.</summary>
    static List<MatchEvent> Hit(World w, float strength)
    {
        var checker = Skater(w, Side.Away, PlayingPosition.C);
        var target = Skater(w, Side.Home, PlayingPosition.C);
        checker.Position = target.Position + new Vector3(0.8f, 0, 0);
        bool sent = false;
        return Run(w, _ => sent, x =>
        {
            x.Events.Publish(new CheckLanded(x.Time, checker.Side, checker.Id, target.Position, strength));
            sent = true;
        }, 1);
    }

    /// <summary>Starts a fight between the away centre (checker) and the home centre, both AI unless a slot is set.</summary>
    static Fight StartFight(World w, bool aiAccepts = true)
    {
        var s = Fighting.State(w);
        s.ForceAccept = aiAccepts;
        Fighting.Offer(w, Skater(w, Side.Away, PlayingPosition.C).Id, Skater(w, Side.Home, PlayingPosition.C).Id);
        // a human on either side accepts
        Run(w, x => Current(x).HasValue, x => { if (x.Controlled(0).TryGet(out var me)) me.Command = new PlayerCommand { AcceptFight = true }; }, 0.1f);
        return Must(Current(w));
    }

    /// <summary>Human slot 0's command each step: punch when the marker first passes <paramref name="at"/>, block as asked.</summary>
    static Action<World> Human(float at) => Human(at, (_, _) => false);

    /// <summary>Human slot 0's command each step: punch when the marker first passes <paramref name="at"/>, block as asked.</summary>
    static Action<World> Human(float at, Func<World, Fight, bool> block)
    {
        return w =>
        {
            if (!w.Controlled(0).TryGet(out var me)) return;
            var cmd = default(PlayerCommand);
            if (Current(w).TryGet(out var f) && f.Of(me.Id).TryGet(out var mine))
            {
                cmd.Punch = f.Stage == FightStage.Exchange && !mine.Punched && f.Sweep + (Game.Step / FightTuning.ExchangeSeconds) >= at;
                cmd.Block = block(w, f);
            }
            me.Command = cmd;
        };
    }

    [Test]
    public void HardCheckStartsFight()
    {
        var w = Make();
        var s = Fighting.State(w);
        s.OfferChance = 1;
        s.ForceAccept = true;
        var events = Hit(w, 1.8f);
        float clock = w.Match.Clock;
        var f = Current(w);
        var a = Skater(w, Side.Away, PlayingPosition.C);
        var h = Skater(w, Side.Home, PlayingPosition.C);
        Check(f.Is(x => x.Checker.PlayerId == a.Id && x.Victim.PlayerId == h.Id), "a hard check (1.8) drops the gloves and both accept: the checker and the skater he hit fight");
        Check(events.Exists(e => e is FightStarted) && events.Exists(e => e is WhistleBlown) && w.Match.Phase == MatchPhase.Stopped,
            "the fight publishes a Fight event and the referee whistles play dead");
        Check(a.Action == PlayerAction.Fight && h.Action == PlayerAction.Fight && Math.Abs(Vector3.Distance(a.Position, h.Position) - (2 * FightTuning.HalfGap)) < 0.01f
            && Fighting.Pose(w, a).Is(p => string.Equals(p.Clip, "grab", StringComparison.Ordinal)), "both square off, an arm's length apart, grabbing sweaters");

        Run(w, x => !Current(x).HasValue, 20);
        Check(Math.Abs(w.Match.Clock - clock) < 0.001f, "the clock does not run during the fight");
        Check(Math.Abs(Vector3.Distance(w.Puck.Position, Must(f).Spot) - 1.2f) < 0.01f, "the dead puck lies by the fight, so the camera stays on it");
    }

    [Test]
    public void SoftCheckOrDecline()
    {
        var soft = Make();
        var decline = Make();
        Fighting.State(soft).OfferChance = 1;
        Fighting.State(soft).ForceAccept = true;
        Hit(soft, 1.2f);
        Check(!Current(soft).HasValue && !Pending(soft).HasValue && soft.Match.Phase == MatchPhase.Play, "a check under 1.5 never starts a fight");

        Fighting.State(decline).OfferChance = 1;
        Fighting.State(decline).ForceAccept = false;
        Hit(decline, 2f);
        Run(decline, _ => false, 0.1f);
        Check(!Current(decline).HasValue && !Pending(decline).HasValue && decline.Match.Phase == MatchPhase.Play && Fighting.State(decline).Declined == 1,
            "an AI who declines skates away and play goes on");
    }

    [Test]
    public void HumanAccepts()
    {
        var w = Make();
        var me = Skater(w, Side.Home, PlayingPosition.C);
        me.Slot = 0;
        var s = Fighting.State(w);
        s.ForceAccept = true;
        var them = Skater(w, Side.Away, PlayingPosition.C);
        Fighting.Offer(w, them.Id, me.Id);
        Run(w, _ => false, 1);
        Check(Pending(w).HasValue && !Current(w).HasValue && w.Match.Phase == MatchPhase.Play, "an offer to a human waits for his answer while play goes on");
        Run(w, x => !Pending(x).HasValue, FightTuning.OfferSeconds + 0.5f);
        Check(!Current(w).HasValue && s.Declined == 1, "a human who lets the offer pass declines it");

        Fighting.Offer(w, them.Id, me.Id);
        Run(w, _ => false, 0.5f);
        Run(w, x => Current(x).HasValue, x => Must(x.Controlled(0)).Command = new PlayerCommand { AcceptFight = true }, 0.1f);
        Check(Current(w).HasValue, "a human who presses accept (F, or Y on a pad) fights");
    }

    [Test]
    public void WonByTiming()
    {
        var w = Make();
        var me = Skater(w, Side.Home, PlayingPosition.C);
        var them = Skater(w, Side.Away, PlayingPosition.C);
        me.Slot = 0;
        var s = Fighting.State(w);
        s.AiPunches = false;
        s.AiBlocks = false;
        var fight = StartFight(w);
        var events = Run(w, x => !Current(x).HasValue, Human(0.5f), 20);
        Check(fight.Knockdown && fight.LoserId == them.Id && fight.Exchange <= 3, string.Create(CultureInfo.InvariantCulture, $"punches timed in the middle of the bar knock the other fighter down (in {fight.Exchange + 1} exchanges, {fight.Victim.Landed} landed)"));

        var call = w.Match.Penalties.FirstOrNone(p => p.PlayerId == them.Id);
        Check(call.Is(c => string.Equals(c.Infraction, FightTuning.Infraction, StringComparison.Ordinal) && Math.Abs(c.SecondsLeft - Penalties.MinorSeconds(w)) < 0.01f) && Penalties.OnTheWay(w, them.Id) && Penalties.InBox(w, them.Id),
            "the loser skates to the box for two minutes for fighting");
        Check(events.Exists(e => e is PenaltyCalled pen && pen.PlayerId == them.Id) && me.OnIce && !Penalties.InBox(w, me.Id) && me.Action == PlayerAction.None,
            "a Penalty event is published for him; the winner stays on the ice");
        Run(w, x => x.Match.Phase == MatchPhase.Play, 15);
        var dot = MatchFlow.FaceoffDot(w);
        Check(w.Match.Phase == MatchPhase.Play && Penalties.PowerPlay(w) == SideOrNone.Home && Math.Abs(dot.X) > 15 && Math.Sign(dot.X) == FaceoffFormation.DefendedEnd(w.Match, Side.Away),
            "play restarts with a faceoff in the loser's end and the winner's club on the power play");
    }

    [Test]
    public void BadTimingLoses()
    {
        var w = Make();
        var me = Skater(w, Side.Home, PlayingPosition.C);
        me.Slot = 0;
        var s = Fighting.State(w);
        s.AiBlocks = false;
        var fight = StartFight(w);
        Run(w, x => !Current(x).HasValue, Human(0.05f), 20);
        Check(fight.LoserId == me.Id && fight.Victim.Missed >= 1 && fight.Victim.Landed == 0,
            $"punches thrown at the edge of the bar miss, and the human loses ({fight.Victim.Missed} missed, {fight.Checker.Landed} taken)");
        Check(Penalties.InBox(w, me.Id) && w.Controlled(0).TryGet(out var now) && now != me && now.OnIce && now.Side == Side.Home, "the losing human's skater goes to the box and control moves to a teammate");
    }

    [Test]
    public void BlockTiming()
    {
        var fresh = Make();
        var stale = Make();
        // block just as the AI winds up: every punch is stopped
        var me = Skater(fresh, Side.Home, PlayingPosition.C);
        me.Slot = 0;
        Fighting.State(fresh).AiBlocks = false;
        var fight = StartFight(fresh);
        Run(fresh, x => Current(x).Is(f => f.Exchange >= 3), Human(2f, (x, f) => !float.IsNaN(f.Checker.LandsAt)), 10);
        Check(fight.Checker.Blocked >= 2 && fight.Checker.Landed == 0 && fight.Victim.Balance >= 1f,
            $"a guard raised during the wind-up blocks the punch ({fight.Checker.Blocked} blocked)");

        // a guard held up the whole time has dropped by the time the punch lands
        var me2 = Skater(stale, Side.Home, PlayingPosition.C);
        me2.Slot = 0;
        Fighting.State(stale).AiBlocks = false;
        var fight2 = StartFight(stale);
        Run(stale, x => !Current(x).TryGet(out var f) || f.Exchange >= 3 || f.Stage == FightStage.Over, Human(2f, (_, _) => true), 10);
        Check(fight2.Checker.Blocked == 0 && fight2.Checker.Landed >= 1 && fight2.Victim.Balance < 1f,
            $"a guard held up from the start is too old to block ({fight2.Checker.Landed} landed)");
    }

    [Test]
    public void BrokenUp()
    {
        var w = Make();
        var s = Fighting.State(w);
        s.AiPunches = false;
        var a = Skater(w, Side.Away, PlayingPosition.C);
        var fight = StartFight(w);
        Run(w, x => !Current(x).HasValue, 20);
        Check(fight.Exchange == FightTuning.MaxExchanges && !fight.Knockdown && fight.LoserId == a.Id && Penalties.InBox(w, a.Id),
            "with nobody down after the last exchange the linesmen break it up; on even balance the checker who started it goes to the box");
    }

    [Test]
    public void NoFight()
    {
        var overtime = Make(4);
        var off = Make(1, s => s with { Fights = false });
        var w = Make();
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
        return Run(w, x => !Current(x).HasValue, Human(0.5f), 20);
    }

    static int Skaters(World w, Side side)
    {
        return w.OnIce(side).Count(p => !p.IsGoalie);
    }

    static Maybe<PenaltyState> Fighting5(World w, Player p)
    {
        return w.Match.Penalties.FirstOrNone(x => x.PlayerId == p.Id && string.Equals(x.Infraction, FightTuning.Infraction, StringComparison.Ordinal)
            && Math.Abs(x.SecondsLeft - Penalties.ClubSeconds(w, Severity.Major)) < 0.01f);
    }

    [Test]
    public void ArcadePenalty()
    {
        var w = Make(rules: RuleSet.Arcade);
        var me = Skater(w, Side.Home, PlayingPosition.C);
        var them = Skater(w, Side.Away, PlayingPosition.C);
        HomeWins(w);
        Run(w, x => x.Match.Phase == MatchPhase.Play, 15);
        var call = w.Match.Penalties.FirstOrNone(p => p.PlayerId == them.Id);
        Check(call.Is(c => Math.Abs(c.SecondsLeft - Penalties.MinorSeconds(w)) < 0.01f) && w.Match.Penalties.Count == 1 && me.OnIce
            && Penalties.PowerPlay(w) == SideOrNone.Home && !Penalties.Ejected(w, them.Id),
            "Arcade: the loser gets two minutes, the winner stays on and his club has the power play");
    }

    [Test]
    public void MajorsStay()
    {
        var w = Make(rules: RuleSet.Nhl);
        var me = Skater(w, Side.Home, PlayingPosition.C);
        var them = Skater(w, Side.Away, PlayingPosition.C);
        var events = HomeWins(w);
        Check(Fighting5(w, me).HasValue && Fighting5(w, them).HasValue && Penalties.InBox(w, me.Id) && Penalties.InBox(w, them.Id) && Penalties.OnTheWay(w, me.Id) && Penalties.OnTheWay(w, them.Id)
            && events.Count(e => e is PenaltyCalled) == 2 && !Penalties.Ejected(w, me.Id) && !Penalties.Ejected(w, them.Id),
            "NHL: both fighters get a five minute major for fighting, winner and loser, skate to the box, and neither is ejected");
        Run(w, x => x.Match.Phase == MatchPhase.Play, 15);
        Check(Skaters(w, Side.Home) == 5 && Skaters(w, Side.Away) == 5 && Penalties.PowerPlay(w) == SideOrNone.None,
            "NHL: the majors are coincidental and substituted, so play goes on five on five");
        var homeSquad = Must(Bench.SquadOf(w, Side.Home));
        var awaySquad = Must(Bench.SquadOf(w, Side.Away));
        Run(w, x => x.Match.Penalties.Count == 0, x => { if (x.Tick % 180 == 0) { Bench.RequestChange(x, Side.Home); Bench.RequestChange(x, Side.Away); } }, 400);
        Check(w.Match.Penalties.Count == 0 && !Penalties.InBox(w, me.Id) && !Penalties.InBox(w, them.Id) && homeSquad.UnitOf(me).HasValue && awaySquad.UnitOf(them).HasValue,
            "NHL: when the five minutes are up both fighters leave the box and stay in their lines");
    }

    [Test]
    public void MajorsAndEjection()
    {
        var w = Make(rules: RuleSet.Shl);
        var me = Skater(w, Side.Home, PlayingPosition.C);
        var them = Skater(w, Side.Away, PlayingPosition.C);
        var homeSquad = Must(Bench.SquadOf(w, Side.Home));
        var awaySquad = Must(Bench.SquadOf(w, Side.Away));
        var events = HomeWins(w);
        Check(Fighting5(w, me).HasValue && Fighting5(w, them).HasValue && events.Count(e => e is PenaltyCalled) == 2,
            "SHL: both fighters get a five minute major for fighting, winner and loser, on the scorebug");
        Check(Penalties.Ejected(w, me.Id) && Penalties.Ejected(w, them.Id) && !homeSquad.UnitOf(me).HasValue && !awaySquad.UnitOf(them).HasValue && !me.OnIce && !them.OnIce,
            "SHL: both also get a game misconduct and are off the squad, the lines refill without them");
        Run(w, x => x.Match.Phase == MatchPhase.Play, 15);
        Check(Skaters(w, Side.Home) == 5 && Skaters(w, Side.Away) == 5 && Penalties.PowerPlay(w) == SideOrNone.None,
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

    [Test]
    public void MajorsNeverShort()
    {
        var w = Make(rules: RuleSet.Shl);
        // two home skaters already serve: substituted majors cannot put home three short, so the fight may go on
        var book = Penalties.Book(w);
        Penalties.Assess(w, book, new DelayedPenalty(Side.Home, Skater(w, Side.Home, PlayingPosition.LW).Id, Infraction.Tripping, w.Time));
        Penalties.Assess(w, book, new DelayedPenalty(Side.Home, Skater(w, Side.Home, PlayingPosition.RW).Id, Infraction.Hooking, w.Time));
        Fighting.State(w).ForceAccept = true;
        Check(Fighting.Offer(w, Skater(w, Side.Away, PlayingPosition.C).Id, Skater(w, Side.Home, PlayingPosition.C).Id),
            "SHL: a club serving two penalties can still fight, since coincidental majors never leave it shorter");
    }

    static readonly RuleSet[] Presets = [RuleSet.Arcade, RuleSet.Shl, RuleSet.Nhl];

    /// <summary>Gets the full matches' results, shared by the class's tests.</summary>
    [ClassDataSource<Played<FightCount>>(Shared = SharedType.PerClass)]
    public required Played<FightCount> FullMatchesPlayed { get; init; }

    /// <summary>Gets the matches: every seed under each preset in turn.</summary>
    public static IEnumerable<int> MatchNumbers() => Enumerable.Range(0, Presets.Length * Seeds.Length);

    /// <summary>One full AI match under a preset, with the referee at full strictness; <see cref="FullMatches"/> judges them.</summary>
    [Test]
    [ExecutionPriority(Priority.High)]
    [MethodDataSource(nameof(MatchNumbers))]
    public void PlayMatch(int i) =>
        FullMatchesPlayed.Add(i, Match(WorldBuilder.DefaultSetup.WithRules(Presets[i / Seeds.Length]) with { PeriodSeconds = 600 }, Seeds[i % Seeds.Length]));

    /// <summary>Full AI matches under each preset, with the referee at full strictness.</summary>
    [Test]
    [DependsOn(nameof(PlayMatch), [typeof(int)])]
    public void FullMatches()
    {
        var played = FullMatchesPlayed.All(Presets.Length * Seeds.Length);
        for (int r = 0; r < Presets.Length; r++)
        {
            var setup = WorldBuilder.DefaultSetup.WithRules(Presets[r]);
            int perFight = setup.Rules.FightPenalty == FightPenalty.LoserMinor ? 1 : 2;
            int fights = 0, boxed = 0, broken = 0, open = 0;
            foreach (var (f, b, x, o) in played.Skip(r * Seeds.Length).Take(Seeds.Length))
            {
                fights += f; boxed += b; broken += x; open += o;
            }
            Check(fights >= 1 && boxed == fights * perFight && broken == 0 && open == 0,
                $"{setup.Rules.Preset}: AI matches of 3 x 10:00: {fights} fight(s) in {Seeds.Length} matches, {boxed} fighting penalties ({perFight} a fight), clubs short by exactly their penalties");
        }
    }

    /// <summary>What one full match came to: its fights, the fighting penalties, steps a club was not at its strength, and penalties left open.</summary>
    public sealed record FightCount(int Fights, int Boxed, int Broken, int Open);

    static readonly int[] Seeds = [2, 3, 4, 5];

    static FightCount Match(MatchSetup setup, int seed)
    {
        var w = new WorldBuilder()
            .Setup(setup with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] })
            .Build();
#pragma warning disable S2245 // fixed seeds so the checks repeat
        Penalties.Book(w).Rng = new Random(seed);
        Fighting.State(w).Rng = new Random(seed);
        ISystem[] systems =
        [
            new PositioningSystem(new Random(seed)), new DecisionSystem(new Random(seed)), new LinesSystem(), new SkatingSystem(), new CheckingSystem(new Random(seed)),
            new PuckHandlingSystem(new Random(seed)), new GoalieSystem { Dice = new Random(seed) }, new PuckPhysics(), new MatchRules(), new OffsideRule(),
            new IcingRule(), new PenaltyRules(), new FightSystem(), new RefereeSystem(),
        ];
#pragma warning restore S2245
        foreach (var s in systems) if (s is PositioningSystem or DecisionSystem or LinesSystem or SkatingSystem or CheckingSystem or PuckHandlingSystem) s.Start(w);
        int boxed = 0, broken = 0;
        for (int i = 0; i < 4000 * 60 && w.Match.Phase is not (MatchPhase.Final or MatchPhase.Overtime or MatchPhase.Shootout); i++)
        {
            w.Events.Clear();
            foreach (var s in systems) s.Update(w, Game.Step);
            w.Time += Game.Step; w.Tick++;
            foreach (var e in w.Events.Of<PenaltyCalled>())
                if (string.Equals(w.Match.Penalties.FindLast(p => p.PlayerId == e.PlayerId)?.Infraction, FightTuning.Infraction, StringComparison.Ordinal)) boxed++;
            // strength is checked in play: between a whistle and the faceoff the bench is still filling the gaps (as the penalty tests do)
            if (w.Match.Phase != MatchPhase.Play) continue;
            foreach (var side in new[] { Side.Home, Side.Away })
                if (w.OnIce(side).Count(p => !p.IsGoalie) != Penalties.TargetSkaters(w, side)) broken++;
        }
        return new FightCount(Fighting.Started(w), boxed, broken, Current(w).HasValue ? 1 : 0);
    }
}
