using System.Numerics;
using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// Tied matches with skating, puck handling, the goalies, puck physics and the match rules (W7c): a
/// tied match goes to 3 on 3 and then to a shootout; and W19's: under the playoff format a tie goes
/// to 5 on 5 and ends on the first goal, with no shootout. Formerly SAP_SCENE=overtimetest.
/// </summary>
public sealed class OvertimeTests : CheckedTest
{
    const float Period = 10;
    static readonly IReadOnlyList<InputSlot> NoHumans = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)];
    static readonly OvertimeFormat Playoffs = OvertimeFormat.ShlPlayoffs with { PeriodSeconds = Period };

    /// <summary>The default exhibition with 10-second periods and no humans.</summary>
    static MatchSetup BaseSetup => WorldBuilder.DefaultSetup with { PeriodSeconds = Period, Inputs = NoHumans };

    static World Make(OvertimeRule rule = OvertimeRule.Shl, Maybe<OvertimeFormat> format = default, Maybe<RuleSet> rules = default) => Make(NoHumans, rule, format, rules);

    static World Make(IReadOnlyList<InputSlot> inputs, OvertimeRule rule = OvertimeRule.Shl, Maybe<OvertimeFormat> format = default, Maybe<RuleSet> rules = default)
    {
        return new WorldBuilder()
            .Setup(BaseSetup with { Overtime = rule, Inputs = inputs, OvertimeFormat = format, Rules = rules.Or(RuleSet.Shl) })
            .Switches(s => s.OvertimeSeconds = Period)
            .Build();
    }

    /// <summary>Gets the match's shootout, none before it starts.</summary>
    static Maybe<ShootoutState> So(World w) => OvertimeRules.Shootout(w);

    static T Must<T>(Maybe<T> m)
        where T : notnull => m.TryGet(out var v) ? v : throw new InvalidOperationException("expected a value");

    /// <summary>Runs the systems a shootout needs, step by step, and returns every event.</summary>
#pragma warning disable S2245 // seeded randomness so the checks repeat
    static List<MatchEvent> Run(World world, Func<World, bool> until, int seed = 1, float limit = 900) => Run(world, until, seed, _ => { }, limit);

    static List<MatchEvent> Run(World world, Func<World, bool> until, int seed, Action<World> each, float limit = 900)
    {
        using var goalie = new GoalieSystem { Dice = new Random(seed) };
        var systems = new ISystem[]
        {
            new PositioningSystem(), new SkatingSystem(), new PuckHandlingSystem(new Random(seed)), goalie,
            new PuckPhysics(), new MatchRules(), new OvertimeRules(new Random(seed)),
        };
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
#pragma warning restore S2245

    /// <summary>A tie after regulation: three skaters a side, the human's control on one of them, then a sudden death faceoff at centre.</summary>
    [Test]
    public void ThreeOnThree()
    {
        var w = Make([new InputSlot(InputDevice.KeyboardMouse, Side.Home), NoHumans[1]]);
        // the human is on the left wing, who goes to the bench
        w.Players.First(p => p.Side == Side.Home && p.Data.Position == PlayingPosition.LW).Slot = 0;
        Run(w, x => x.Match.Phase == MatchPhase.Overtime);
        Run(w, _ => false, limit: Game.Step);
        int Skaters(Side side) => w.OnIce(side).Count(p => !p.IsGoalie);
        Check(w.Match.Phase == MatchPhase.Overtime && Skaters(Side.Home) == 3 && Skaters(Side.Away) == 3, "a tie after regulation leaves three skaters a side");
        Check(w.OnIce(Side.Home).Count(p => p.IsGoalie) == 1 && w.OnIce(Side.Away).Count(p => p.IsGoalie) == 1, "both goalies stay on");
        var kept = w.OnIce(Side.Home).Where(p => !p.IsGoalie).Select(p => p.Data.Position).Order().ToList();
        Check(kept.SequenceEqual([PlayingPosition.C, PlayingPosition.D, PlayingPosition.D]), "the centre and the two defence stay on");
        Check(w.Controlled(0).Is(human => human is { OnIce: true, Data.Position: PlayingPosition.C }), "the human's control moves from the benched wing to the centre");

        Run(w, x => x.Match.Phase == MatchPhase.Faceoff, limit: 10);
        Check(w.Match.Period == 4 && MathF.Abs(w.Match.Clock - Period) < 1e-3f && w.Puck.Position == RinkSpec.CentreDot, "after the break, period 4 starts at centre with a full overtime clock");
        Check(w.Players.Where(p => p.OnIce && !p.IsGoalie).All(p => FaceoffFormation.DefendedEnd(w.Match, p.Side) * p.Position.X > 0), "the six skaters line up in their own halves");
        Run(w, x => x.Match.Phase == MatchPhase.Play, limit: 5);
        Check(w.Puck.Live && w.OnIce(Side.Home).Count(p => !p.IsGoalie) == 3, "the puck drops 3 on 3");
    }

    /// <summary>A goal in the 3 on 3 wins it.</summary>
    [Test]
    public void OvertimeGoal()
    {
        var w = Make();
        Run(w, x => x.Match.Phase == MatchPhase.Play && x.Match.Period == 4);
        int end = PuckHandling.AttackEnd(w, Side.Home);
        var pk = w.Puck;
        pk.Carrier = -1;
        pk.Position = new Vector3(end * (RinkSpec.GoalLineX - 4), 0, 0.6f);
        pk.Velocity = new Vector3(end * 30, 0, 0);
        pk.LastTouch = w.OnIce(Side.Home).First(p => !p.IsGoalie).Id;
        var ev = Run(w, x => x.Match.Phase == MatchPhase.Final, limit: 3 + MatchTuning.CelebrationSeconds);
        var final = ev.OfType<MatchEnded>().ToList();
        bool wonHere = final.Count == 1 && final[0] is MatchWon { Winner: Side.Home } && w.Match.Score[0] == 1 && w.Match.Score[1] == 0;
        // the goalie may stop it; then the overtime runs out into the shootout, still a valid ending
        Check(wonHere || (ev.Any(e => e is ShotSaved) && w.Match.Phase != MatchPhase.Final), $"an overtime goal ends it 1-0 (score {w.Match.Score[0]}-{w.Match.Score[1]})");
        Check(!So(w).HasValue, "no shootout after an overtime goal");
    }

    /// <summary>The tally decides the shootout within the rounds, and in sudden death only after both clubs shot.</summary>
    [Test]
    public void ShootoutTally()
    {
        static ShootoutState Tally(params (Side Side, bool Scored)[] shots)
        {
            var so = new ShootoutState();
            foreach (var (side, scored) in shots) so.Add(new ShootoutShot(side, 0, scored));
            return so;
        }

        const int R = 5;
        var h = Side.Home;
        var a = Side.Away;
        Check(Tally((h, true), (a, false), (h, true), (a, false), (h, true)).Decided(R) == SideOrNone.None, "3-0 with the away club two shots behind is still open");
        Check(Tally((h, true), (a, false), (h, true), (a, true), (h, false), (a, false), (h, false), (a, false)).Decided(R) == SideOrNone.None, "2-1 after four rounds each: away could still tie");
        Check(Tally((h, true), (a, false), (h, true), (a, false), (h, true), (a, false)).Decided(R) == SideOrNone.Home, "3-0 after three rounds each, two to go: home has won");
        Check(Tally((h, true), (a, false), (h, true), (a, false), (h, true), (a, false), (h, true)).Decided(R) == SideOrNone.Home, "4-0 with two away shots left: home has won");
        var level = new List<(Side, bool)>();
        for (int i = 0; i < R; i++) level.AddRange([(h, i == 0), (a, i == 1)]);
        Check(Tally([.. level]).Decided(R) == SideOrNone.None, "level after all rounds: sudden death");
        Check(Tally([.. level, (h, true)]).Decided(R) == SideOrNone.None, "sudden death: home scores, away still to shoot");
        Check(Tally([.. level, (h, true), (a, true)]).Decided(R) == SideOrNone.None, "both score in a sudden death round: go on");
        Check(Tally([.. level, (h, false), (a, true)]).Decided(R) == SideOrNone.Away, "away scores after a home miss: away wins");
    }

    /// <summary>Scoreless overtimes go to shootouts that the AI shooters and goalies settle.</summary>
    [Test]
    public void Shootouts()
    {
        int goals = 0, misses = 0, decided = 0, games = 6;
        bool alternate = true, awayFirst = true, alone = true, oneEnd = true, oneGoal = true, fairOrder = true, celebrated = true;
        for (int seed = 1; seed <= games; seed++)
        {
            var w = Make(OvertimeRule.Shl);
            Run(w, x => x.Match.Phase == MatchPhase.Shootout, seed);
            if (seed == 1) Check(w.Match.Period == 4 && w.Match.Phase == MatchPhase.Shootout, "a scoreless overtime goes to the shootout");
            var ev = Run(w, x => x.Match.Phase == MatchPhase.Final, seed, x =>
            {
                if (!So(x).Is(t => t.ShooterId >= 0)) return;
                // only the shooter and the two goalies on the ice
                alone &= x.Players.Count(p => p.OnIce && !p.IsGoalie) <= 1 && x.Players.Count(p => p.OnIce && p.IsGoalie) == 2;
            });
            if (!So(w).TryGet(out var so) || w.Match.Phase != MatchPhase.Final) { Check(false, $"seed {seed}: the shootout reaches the final"); continue; }
            decided++;
            goals += so.Shots.Count(s => s.Scored);
            misses += so.Shots.Count(s => !s.Scored);
            for (int i = 0; i < so.Shots.Count; i++) alternate &= so.Shots[i].Side == (i % 2 == 0 ? Side.Away : Side.Home);
            awayFirst &= so.Shots[0].Side == Side.Away && so.Rounds == 3;
            var homeShooters = so.Shots.Where(s => s.Side == Side.Home).Select(s => s.PlayerId).Take(3).ToList();
            fairOrder &= homeShooters.Distinct().Count() == homeShooters.Count;
            var end = ev.OfType<MatchEnded>().ToList();
            oneEnd &= end.Count == 1 && end[0] is MatchWon won && won.Winner.OrNone() == so.Winner && so.Decided(so.Rounds) == so.Winner;
            if (!so.Winner.TryGetSide(out var sw)) { Check(false, $"seed {seed}: the shootout has a winner"); continue; }
            oneGoal &= w.Match.Score[(int)sw] == 1 && w.Match.Score[1 - (int)sw] == 0;
            celebrated &= Celebration.Winner(w) == so.Winner && w.OnIce(sw).Count(p => !p.IsGoalie) == 5 && w.OnIce(1 - sw).Any(p => !p.IsGoalie);
            Console.WriteLine($"      seed {seed}: {string.Concat(so.Shots.Select(s => s.Scored ? "o" : "x"))}, {so.Winner} wins {w.Match.Score[0]}-{w.Match.Score[1]}");
        }

        Check(decided == games, $"all {games} shootouts are decided");
        Check(celebrated, "after the shootout both benches pour back onto the ice and the winners celebrate (W40)");
        Check(awayFirst && alternate, "SHL: three shooters each, the away club shoots first and opens every sudden death round");
        Check(fairOrder, "the first three shooters of a club are three different players");
        Check(alone, "only the shooter and the two goalies are on the ice");
        Check(oneEnd, "one MatchEnd, naming the winner the tally gives");
        Check(oneGoal, "the winner gets one goal on the final score: 1-0 after a 0-0 match");
        Check(goals > 0 && misses > 0, $"shooters score and goalies stop some ({goals} goals, {misses} misses)");
    }

    /// <summary>With the shootout-only rule, a tie skips the overtime.</summary>
    [Test]
    public void ShootoutOnly()
    {
        var w = Make(OvertimeRule.ShootoutOnly);
        var ev = Run(w, x => x.Match.Phase == MatchPhase.Final);
        Check(w.Match.Period == 3 && So(w).Is(t => t.Winner != SideOrNone.None), "shootout only: no overtime period, the shootout decides it");
        Check(ev.Count(e => e is MatchEnded) == 1, "one MatchEnd");
    }

    /// <summary>The per-mode formats: what each one plays after a tie.</summary>
    [Test]
    public void Formats()
    {
        var setup = BaseSetup;
        var shl = OvertimeFormat.Shl;
        var po = OvertimeFormat.ShlPlayoffs;
        Check(shl.AfterTie(0) == MatchPhase.Overtime && shl.AfterTie(1) == MatchPhase.Shootout, "SHL: one overtime period, then the shootout");
        Check(po.AfterTie(0) == MatchPhase.Overtime && po.AfterTie(1) == MatchPhase.Overtime && po.AfterTie(5) == MatchPhase.Overtime, "SHL playoffs: overtime periods until a goal");
        Check(po is { Skaters: 5, HasShootout: false, } && po.PeriodSeconds == 20 * 60f, "SHL playoffs: 5 on 5, 20-minute periods, no shootout");
        Check(!OvertimeFormat.None.AfterTie(0).HasValue && OvertimeFormat.ShootoutOnly.AfterTie(0) == MatchPhase.Shootout, "none keeps the tie; shootout only skips the overtime");
        Check(OvertimeFormat.Of(setup with { Overtime = OvertimeRule.Shl }) == shl && OvertimeFormat.Of(setup with { Overtime = OvertimeRule.None }) == OvertimeFormat.None, "a setup without a format plays the one its rule names");
        Check(OvertimeFormat.Of(setup with { Overtime = OvertimeRule.Shl, OvertimeFormat = po }) == po, "a setup's own format wins over its rule");
        var own = shl with { Name = "4 on 4", Skaters = 4 };
        Check(own.Skaters == 4 && own.HasShootout && own.AfterTie(0) == MatchPhase.Overtime, "a league makes its own format from a preset");
    }

    /// <summary>The playoff format: a tie keeps 5 on 5, a scoreless period goes to another, never to a shootout.</summary>
    [Test]
    public void PlayoffOvertime()
    {
        var w = Make([new InputSlot(InputDevice.KeyboardMouse, Side.Home), NoHumans[1]], format: Playoffs);
        w.Players.First(p => p.Side == Side.Home && p.Data.Position == PlayingPosition.LW).Slot = 0;
        Run(w, x => x.Match.Phase == MatchPhase.Overtime);
        Run(w, _ => false, limit: Game.Step);
        int Skaters(Side side) => w.OnIce(side).Count(p => !p.IsGoalie);
        Check(w.Match.Phase == MatchPhase.Overtime && Skaters(Side.Home) == 5 && Skaters(Side.Away) == 5, "playoffs: a tie after regulation keeps five skaters a side");
        Check(w.Controlled(0).Is(p => p is { OnIce: true, Data.Position: PlayingPosition.LW }), "the human keeps the wing");
        Run(w, x => x.Match.Phase == MatchPhase.Play, limit: 10);
        Check(w.Match.Period == 4 && w.Puck.Live && Skaters(Side.Home) == 5 && Skaters(Side.Away) == 5, "period 4 drops 5 on 5");
        Check(MathF.Abs(OvertimeRules.PeriodLength(w) - Period) < 1e-3f, "the overtime period has the format's length");

        // nobody shoots: the period runs out scoreless
        Run(w, x => x.Match.Phase != MatchPhase.Play && x.Match.Phase != MatchPhase.Faceoff && x.Match.Phase != MatchPhase.Stopped && x.Match.Phase != MatchPhase.Goal);
        Check(w.Match.Phase == MatchPhase.Overtime && w.Match.Period == 4, "a scoreless overtime goes to another overtime, not a shootout");
        Run(w, x => x.Match.Phase == MatchPhase.Play, limit: 10);
        Check(w.Match.Period == 5 && Skaters(Side.Home) == 5 && Skaters(Side.Away) == 5, "period 5 drops 5 on 5 as well");
        Check(!So(w).HasValue && w.Match.Score[0] == 0 && w.Match.Score[1] == 0, "no shootout and no score");
    }

    /// <summary>The first goal in a playoff overtime ends it.</summary>
    [Test]
    public void PlayoffGoal()
    {
        var w = Make(format: Playoffs);
        Run(w, x => x.Match.Phase == MatchPhase.Play && x.Match.Period == 5);
        int end = PuckHandling.AttackEnd(w, Side.Away);
        var pk = w.Puck;
        pk.Carrier = -1;
        // a puck the goalie cannot reach: it is already in the net, over the line at the far post
        pk.Position = new Vector3(end * (RinkSpec.GoalLineX + 0.4f), 0, 0.8f);
        pk.Velocity = new Vector3(end * 5, 0, 0);
        pk.LastTouch = w.OnIce(Side.Away).First(p => !p.IsGoalie).Id;
        var ev = Run(w, x => x.Match.Phase == MatchPhase.Final, limit: 3 + MatchTuning.CelebrationSeconds);
        var final = ev.OfType<MatchEnded>().ToList();
        Check(final.Count == 1 && final[0] is MatchWon { Winner: Side.Away } && w.Match.Score[0] == 0 && w.Match.Score[1] == 1, $"the first goal in the second overtime ends it 0-1 (score {w.Match.Score[0]}-{w.Match.Score[1]})");
        Check(w.Match.Phase == MatchPhase.Final && w.Match.Period == 5, "the match is final in period 5");
        Check(!So(w).HasValue, "no shootout in the playoffs");
    }

    /// <summary>A human slot takes its club's shots; a human who does nothing runs out of time.</summary>
    [Test]
    public void HumanShooter()
    {
        var w = Make([new InputSlot(InputDevice.KeyboardMouse, Side.Home), NoHumans[1]], rules: RuleSet.Shl with { ShootoutFirst = ShootoutFirst.Home });
        Run(w, x => So(x).Is(t => t.ShooterId >= 0));
        var so = Must(So(w));
        var shooter = w.Players[so.ShooterId];
        Check(shooter.Side == Side.Home && shooter.Slot == 0 && w.Controlled(0) == shooter, "the human shoots for the home club");
        float start = w.Time;
        Run(w, x => so.Shots.Count == 1);
        Check(!so.Shots[0].Scored && w.Time - start >= OvertimeTuning.AttemptSeconds, "standing still, the attempt runs out of time: a miss");
        Run(w, x => so.ShooterId >= 0 && w.Players[so.ShooterId].Side == Side.Away && so.Shots.Count == 1 && x.Puck.Live);
        Check(w.Players[so.ShooterId].Slot < 0 && !w.Controlled(0).HasValue, "an away shooter is the AI's; the home human controls nobody");
    }

    /// <summary>W45: shooters and order from the rules: SHL three each with away first, five when set, home first when set, NHL home choosing.</summary>
    [Test]
    public void ShootoutRules()
    {
        var setup = BaseSetup;
        Check(ShootoutOrder.Shooters(setup with { Rules = RuleSet.Shl }) == 3 && ShootoutOrder.First(setup with { Rules = RuleSet.Shl }) == SideOrNone.Away, "SHL preset: three shooters each, away first");
        Check(ShootoutOrder.Shooters(setup with { Rules = RuleSet.Nhl }) == 3 && ShootoutOrder.First(setup with { Rules = RuleSet.Nhl, Inputs = NoHumans }) == SideOrNone.Home, "NHL preset: three shooters each, an AI home club chooses to shoot first");
        var human = setup with { Rules = RuleSet.Nhl, Inputs = [new InputSlot(InputDevice.KeyboardMouse, Side.Home), NoHumans[1]] };
        Check(ShootoutOrder.First(human) == SideOrNone.None, "NHL preset: a human home club is asked to choose");
        Check(ShootoutOrder.First(human with { Inputs = [NoHumans[0], new InputSlot(InputDevice.Gamepad1, Side.Away)] }) == SideOrNone.Home, "NHL preset: a human away club does not choose; the AI home club shoots first");

        var five = Make(OvertimeRule.ShootoutOnly, rules: RuleSet.Shl with { ShootoutShooters = 5 });
        Run(five, x => x.Match.Phase == MatchPhase.Final, 3);
        Check(So(five).TryGet(out var so5) && so5 is { Rounds: 5, Winner: not SideOrNone.None } && so5.Shots[0].Side == Side.Away && so5.Decided(5) == so5.Winner, "five shooters when set: decided over five rounds");

        var home = Make(OvertimeRule.ShootoutOnly, rules: RuleSet.Shl with { ShootoutFirst = ShootoutFirst.Home });
        Run(home, x => x.Match.Phase == MatchPhase.Final, 2);
        bool turns = So(home).TryGet(out var soh) && soh.Shots.Select((s, i) => s.Side == (i % 2 == 0 ? Side.Home : Side.Away)).All(b => b);
        Check(turns && Must(So(home)).Shots.Count > 0, "home first when set: home opens every round");

        var nhl = Make(OvertimeRule.ShootoutOnly, rules: RuleSet.Nhl);
        Run(nhl, x => x.Match.Phase == MatchPhase.Final, 4);
        Check(So(nhl).TryGet(out var son) && son is { Winner: not SideOrNone.None } && son.Shots[0].Side == Side.Home, "NHL preset with an AI home club: home shoots first");
    }

    /// <summary>W45: a human home club under NHL rules picks the order; nobody shoots until it does, and a home club that never picks shoots first.</summary>
    [Test]
    public void HomeChooses()
    {
        var w = Make([new InputSlot(InputDevice.KeyboardMouse, Side.Home), NoHumans[1]], OvertimeRule.ShootoutOnly, rules: RuleSet.Nhl);
        Run(w, x => So(x).HasValue);
        Run(w, _ => false, limit: OvertimeTuning.BreakSeconds + 2);
        var so = So(w);
        Check(so.Is(t => t.ShooterId == -1 && t.Shots.Count == 0), "NHL: the shootout waits while the human home club chooses");
        Run(w, x => So(x).Is(t => t.ShooterId >= 0), limit: ShootoutOrder.ChooseSeconds + 2);
        Check(so.Is(t => t.ShooterId >= 0 && w.Players[t.ShooterId].Side == Side.Home), "a home club that does not pick in time shoots first");
    }
}
