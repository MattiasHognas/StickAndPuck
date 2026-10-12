using System.Numerics;
using StickAndPuck.AI;
using StickAndPuck.Core;
using StickAndPuck.Goalie;
using StickAndPuck.Physics;
using StickAndPuck.Players;

namespace StickAndPuck.Rules;

/// <summary>
/// SAP_SCENE=overtimetest: plays tied matches with skating, puck handling, the goalies, puck physics
/// and the match rules, no window, prints one line per check and exits with 0 when all pass, 1
/// otherwise. Covers the W7c "done when": a tied match goes to 3 on 3 and then to a shootout, and
/// W19's: under the playoff format a tie goes to 5 on 5 and ends on the first goal, with no shootout.
/// </summary>
public sealed class OvertimeSelfTest : IScene
{
    const float Period = 10;
    static readonly IReadOnlyList<InputSlot> NoHumans = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)];
    int failures;

    /// <inheritdoc/>
    public string Name => "overtimetest";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var setup = Scenes.DefaultSetup(clubs) with { PeriodSeconds = Period, Inputs = NoHumans };
        World Make(OvertimeRule rule = OvertimeRule.Shl, IReadOnlyList<InputSlot>? inputs = null, OvertimeFormat? format = null, RuleSet? rules = null)
        {
            var w = new World(setup with { Overtime = rule, Inputs = inputs ?? NoHumans, OvertimeFormat = format, Rules = rules ?? RuleSet.Shl }, settings);
            Scenes.AddStarters(w);
            MatchFlow.Begin(w, log: false);
            return w;
        }

        OvertimeRules.PeriodSeconds = Period;
        ThreeOnThree(Make(inputs: [new InputSlot(InputDevice.KeyboardMouse, Side.Home), NoHumans[1]]));
        OvertimeGoal(Make());
        ShootoutTally();
        Shootouts(rule => Make(rule));
        ShootoutOnly(Make(OvertimeRule.ShootoutOnly));
        var homeFirst = RuleSet.Shl with { ShootoutFirst = ShootoutFirst.Home };
        HumanShooter(Make(inputs: [new InputSlot(InputDevice.KeyboardMouse, Side.Home), NoHumans[1]], rules: homeFirst));
        ShootoutRules(setup, rules => Make(OvertimeRule.ShootoutOnly, rules: rules));
        HomeChooses(Make(OvertimeRule.ShootoutOnly, inputs: [new InputSlot(InputDevice.KeyboardMouse, Side.Home), NoHumans[1]], rules: RuleSet.Nhl));
        Formats(setup);
        var playoffs = OvertimeFormat.ShlPlayoffs with { PeriodSeconds = Period };
        PlayoffOvertime(Make(format: playoffs, inputs: [new InputSlot(InputDevice.KeyboardMouse, Side.Home), NoHumans[1]]));
        PlayoffGoal(Make(format: playoffs));
        OvertimeRules.PeriodSeconds = MatchTuning.OvertimeSeconds;

        Console.WriteLine(failures == 0 ? "OVERTIMETEST: all checks passed" : $"OVERTIMETEST: {failures} check(s) failed");
        Environment.Exit(failures == 0 ? 0 : 1);
        return Make();
    }

    void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (!ok) failures++;
    }

    /// <summary>Runs the systems a shootout needs, step by step, calling <paramref name="each"/> before each step, and returns every event.</summary>
#pragma warning disable S2245 // seeded randomness so the checks repeat
    static List<MatchEvent> Run(World world, Func<World, bool> until, int seed = 1, Action<World>? each = null, float limit = 900)
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
            each?.Invoke(world);
            foreach (var s in systems) s.Update(world, Game.Step);
            events.AddRange(world.Events.Current);
            world.Time += Game.Step; world.Tick++;
        }
        return events;
    }
#pragma warning restore S2245

    /// <summary>A tie after regulation: three skaters a side, the human's control on one of them, then a sudden death faceoff at centre.</summary>
    void ThreeOnThree(World w)
    {
        // the human is on the left wing, who goes to the bench
        w.Players.First(p => p.Side == Side.Home && p.Data.Position == PlayingPosition.LW).Slot = 0;
        Run(w, x => x.Match.Phase == MatchPhase.Overtime);
        Run(w, _ => false, limit: Game.Step);
        int Skaters(Side side) => w.OnIce(side).Count(p => !p.IsGoalie);
        Check(w.Match.Phase == MatchPhase.Overtime && Skaters(Side.Home) == 3 && Skaters(Side.Away) == 3, "a tie after regulation leaves three skaters a side");
        Check(w.OnIce(Side.Home).Count(p => p.IsGoalie) == 1 && w.OnIce(Side.Away).Count(p => p.IsGoalie) == 1, "both goalies stay on");
        var kept = w.OnIce(Side.Home).Where(p => !p.IsGoalie).Select(p => p.Data.Position).Order().ToList();
        Check(kept.SequenceEqual([PlayingPosition.C, PlayingPosition.D, PlayingPosition.D]), "the centre and the two defence stay on");
        var human = w.Controlled(0);
        Check(human is { OnIce: true, Data.Position: PlayingPosition.C }, "the human's control moves from the benched wing to the centre");

        Run(w, x => x.Match.Phase == MatchPhase.Faceoff, limit: 10);
        Check(w.Match.Period == 4 && MathF.Abs(w.Match.Clock - Period) < 1e-3f && w.Puck.Position == RinkSpec.CentreDot, "after the break, period 4 starts at centre with a full overtime clock");
        Check(w.Players.Where(p => p.OnIce && !p.IsGoalie).All(p => FaceoffFormation.DefendedEnd(w.Match, p.Side) * p.Position.X > 0), "the six skaters line up in their own halves");
        Run(w, x => x.Match.Phase == MatchPhase.Play, limit: 5);
        Check(w.Puck.Live && w.OnIce(Side.Home).Count(p => !p.IsGoalie) == 3, "the puck drops 3 on 3");
    }

    /// <summary>A goal in the 3 on 3 wins it.</summary>
    void OvertimeGoal(World w)
    {
        Run(w, x => x.Match.Phase == MatchPhase.Play && x.Match.Period == 4);
        int end = PuckHandling.AttackEnd(w, Side.Home);
        var pk = w.Puck;
        pk.Carrier = -1;
        pk.Position = new Vector3(end * (RinkSpec.GoalLineX - 4), 0, 0.6f);
        pk.Velocity = new Vector3(end * 30, 0, 0);
        pk.LastTouch = w.OnIce(Side.Home).First(p => !p.IsGoalie).Id;
        var ev = Run(w, x => x.Match.Phase == MatchPhase.Final, limit: 3 + MatchTuning.CelebrationSeconds);
        var final = ev.Where(e => e.Kind == MatchEventKind.MatchEnd).ToList();
        bool wonHere = final.Count == 1 && final[0].Side == Side.Home && w.Match.Score[0] == 1 && w.Match.Score[1] == 0;
        // the goalie may stop it; then the overtime runs out into the shootout, still a valid ending
        Check(wonHere || (ev.Any(e => e.Kind == MatchEventKind.Save) && w.Match.Phase != MatchPhase.Final), $"an overtime goal ends it 1-0 (score {w.Match.Score[0]}-{w.Match.Score[1]})");
        Check(OvertimeRules.Shootout(w) == null, "no shootout after an overtime goal");
    }

    /// <summary>The tally decides the shootout within the rounds, and in sudden death only after both clubs shot.</summary>
    void ShootoutTally()
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
        Check(Tally((h, true), (a, false), (h, true), (a, false), (h, true)).Decided(R) == null, "3-0 with the away club two shots behind is still open");
        Check(Tally((h, true), (a, false), (h, true), (a, true), (h, false), (a, false), (h, false), (a, false)).Decided(R) == null, "2-1 after four rounds each: away could still tie");
        Check(Tally((h, true), (a, false), (h, true), (a, false), (h, true), (a, false)).Decided(R) == Side.Home, "3-0 after three rounds each, two to go: home has won");
        Check(Tally((h, true), (a, false), (h, true), (a, false), (h, true), (a, false), (h, true)).Decided(R) == Side.Home, "4-0 with two away shots left: home has won");
        var level = new List<(Side, bool)>();
        for (int i = 0; i < R; i++) level.AddRange([(h, i == 0), (a, i == 1)]);
        Check(Tally([.. level]).Decided(R) == null, "level after all rounds: sudden death");
        Check(Tally([.. level, (h, true)]).Decided(R) == null, "sudden death: home scores, away still to shoot");
        Check(Tally([.. level, (h, true), (a, true)]).Decided(R) == null, "both score in a sudden death round: go on");
        Check(Tally([.. level, (h, false), (a, true)]).Decided(R) == Side.Away, "away scores after a home miss: away wins");
    }

    /// <summary>Scoreless overtimes go to shootouts that the AI shooters and goalies settle.</summary>
    void Shootouts(Func<OvertimeRule, World> make)
    {
        int goals = 0, misses = 0, decided = 0, games = 6;
        bool alternate = true, awayFirst = true, alone = true, oneEnd = true, oneGoal = true, fairOrder = true, celebrated = true;
        for (int seed = 1; seed <= games; seed++)
        {
            var w = make(OvertimeRule.Shl);
            Run(w, x => x.Match.Phase == MatchPhase.Shootout, seed);
            if (seed == 1) Check(w.Match.Period == 4 && w.Match.Phase == MatchPhase.Shootout, "a scoreless overtime goes to the shootout");
            var ev = Run(w, x => x.Match.Phase == MatchPhase.Final, seed, x =>
            {
                if (OvertimeRules.Shootout(x) is not { ShooterId: >= 0 }) return;
                // only the shooter and the two goalies on the ice
                alone &= x.Players.Count(p => p.OnIce && !p.IsGoalie) <= 1 && x.Players.Count(p => p.OnIce && p.IsGoalie) == 2;
            });
            var so = OvertimeRules.Shootout(w);
            if (so == null || w.Match.Phase != MatchPhase.Final) { Check(false, $"seed {seed}: the shootout reaches the final"); continue; }
            decided++;
            goals += so.Shots.Count(s => s.Scored);
            misses += so.Shots.Count(s => !s.Scored);
            for (int i = 0; i < so.Shots.Count; i++) alternate &= so.Shots[i].Side == (i % 2 == 0 ? Side.Away : Side.Home);
            awayFirst &= so.Shots[0].Side == Side.Away && so.Rounds == 3;
            var homeShooters = so.Shots.Where(s => s.Side == Side.Home).Select(s => s.PlayerId).Take(3).ToList();
            fairOrder &= homeShooters.Distinct().Count() == homeShooters.Count;
            var end = ev.Where(e => e.Kind == MatchEventKind.MatchEnd).ToList();
            oneEnd &= end.Count == 1 && end[0].Side == so.Winner && so.Decided(so.Rounds) == so.Winner;
            oneGoal &= w.Match.Score[(int)so.Winner!.Value] == 1 && w.Match.Score[1 - (int)so.Winner.Value] == 0;
            celebrated &= Celebration.Winner(w) == so.Winner && w.OnIce(so.Winner.Value).Count(p => !p.IsGoalie) == 5 && w.OnIce(1 - so.Winner.Value).Any(p => !p.IsGoalie);
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
    void ShootoutOnly(World w)
    {
        var ev = Run(w, x => x.Match.Phase == MatchPhase.Final);
        Check(w.Match.Period == 3 && OvertimeRules.Shootout(w) is { Winner: not null }, "shootout only: no overtime period, the shootout decides it");
        Check(ev.Count(e => e.Kind == MatchEventKind.MatchEnd) == 1, "one MatchEnd");
    }

    /// <summary>The per-mode formats: what each one plays after a tie.</summary>
    void Formats(MatchSetup setup)
    {
        var shl = OvertimeFormat.Shl;
        var po = OvertimeFormat.ShlPlayoffs;
        Check(shl.AfterTie(0) == MatchPhase.Overtime && shl.AfterTie(1) == MatchPhase.Shootout, "SHL: one overtime period, then the shootout");
        Check(po.AfterTie(0) == MatchPhase.Overtime && po.AfterTie(1) == MatchPhase.Overtime && po.AfterTie(5) == MatchPhase.Overtime, "SHL playoffs: overtime periods until a goal");
        Check(po is { Skaters: 5, HasShootout: false, PeriodSeconds: 20 * 60 }, "SHL playoffs: 5 on 5, 20-minute periods, no shootout");
        Check(OvertimeFormat.None.AfterTie(0) == null && OvertimeFormat.ShootoutOnly.AfterTie(0) == MatchPhase.Shootout, "none keeps the tie; shootout only skips the overtime");
        Check(OvertimeFormat.Of(setup with { Overtime = OvertimeRule.Shl }) == shl && OvertimeFormat.Of(setup with { Overtime = OvertimeRule.None }) == OvertimeFormat.None, "a setup without a format plays the one its rule names");
        Check(OvertimeFormat.Of(setup with { Overtime = OvertimeRule.Shl, OvertimeFormat = po }) == po, "a setup's own format wins over its rule");
        var own = shl with { Name = "4 on 4", Skaters = 4 };
        Check(own.Skaters == 4 && own.HasShootout && own.AfterTie(0) == MatchPhase.Overtime, "a league makes its own format from a preset");
    }

    /// <summary>The playoff format: a tie keeps 5 on 5, a scoreless period goes to another, never to a shootout.</summary>
    void PlayoffOvertime(World w)
    {
        w.Players.First(p => p.Side == Side.Home && p.Data.Position == PlayingPosition.LW).Slot = 0;
        Run(w, x => x.Match.Phase == MatchPhase.Overtime);
        Run(w, _ => false, limit: Game.Step);
        int Skaters(Side side) => w.OnIce(side).Count(p => !p.IsGoalie);
        Check(w.Match.Phase == MatchPhase.Overtime && Skaters(Side.Home) == 5 && Skaters(Side.Away) == 5, "playoffs: a tie after regulation keeps five skaters a side");
        Check(w.Controlled(0) is { OnIce: true, Data.Position: PlayingPosition.LW }, "the human keeps the wing");
        Run(w, x => x.Match.Phase == MatchPhase.Play, limit: 10);
        Check(w.Match.Period == 4 && w.Puck.Live && Skaters(Side.Home) == 5 && Skaters(Side.Away) == 5, "period 4 drops 5 on 5");
        Check(MathF.Abs(OvertimeRules.PeriodLength(w) - Period) < 1e-3f, "the overtime period has the format's length");

        // nobody shoots: the period runs out scoreless
        Run(w, x => x.Match.Phase != MatchPhase.Play && x.Match.Phase != MatchPhase.Faceoff && x.Match.Phase != MatchPhase.Stopped && x.Match.Phase != MatchPhase.Goal);
        Check(w.Match.Phase == MatchPhase.Overtime && w.Match.Period == 4, "a scoreless overtime goes to another overtime, not a shootout");
        Run(w, x => x.Match.Phase == MatchPhase.Play, limit: 10);
        Check(w.Match.Period == 5 && Skaters(Side.Home) == 5 && Skaters(Side.Away) == 5, "period 5 drops 5 on 5 as well");
        Check(OvertimeRules.Shootout(w) == null && w.Match.Score[0] == 0 && w.Match.Score[1] == 0, "no shootout and no score");
    }

    /// <summary>The first goal in a playoff overtime ends it.</summary>
    void PlayoffGoal(World w)
    {
        Run(w, x => x.Match.Phase == MatchPhase.Play && x.Match.Period == 5);
        int end = PuckHandling.AttackEnd(w, Side.Away);
        var pk = w.Puck;
        pk.Carrier = -1;
        // a puck the goalie cannot reach: it is already in the net, over the line at the far post
        pk.Position = new Vector3(end * (RinkSpec.GoalLineX + 0.4f), 0, 0.8f);
        pk.Velocity = new Vector3(end * 5, 0, 0);
        pk.LastTouch = w.OnIce(Side.Away).First(p => !p.IsGoalie).Id;
        var ev = Run(w, x => x.Match.Phase == MatchPhase.Final, limit: 3 + MatchTuning.CelebrationSeconds);
        var final = ev.Where(e => e.Kind == MatchEventKind.MatchEnd).ToList();
        Check(final.Count == 1 && final[0].Side == Side.Away && w.Match.Score[0] == 0 && w.Match.Score[1] == 1, $"the first goal in the second overtime ends it 0-1 (score {w.Match.Score[0]}-{w.Match.Score[1]})");
        Check(w.Match.Phase == MatchPhase.Final && w.Match.Period == 5, "the match is final in period 5");
        Check(OvertimeRules.Shootout(w) == null, "no shootout in the playoffs");
    }

    /// <summary>A human slot takes its club's shots; a human who does nothing runs out of time.</summary>
    void HumanShooter(World w)
    {
        Run(w, x => OvertimeRules.Shootout(x) is { ShooterId: >= 0 });
        var so = OvertimeRules.Shootout(w)!;
        var shooter = w.Players[so.ShooterId];
        Check(shooter.Side == Side.Home && shooter.Slot == 0 && w.Controlled(0) == shooter, "the human shoots for the home club");
        float start = w.Time;
        Run(w, x => so.Shots.Count == 1);
        Check(!so.Shots[0].Scored && w.Time - start >= OvertimeTuning.AttemptSeconds, "standing still, the attempt runs out of time: a miss");
        Run(w, x => so.ShooterId >= 0 && w.Players[so.ShooterId].Side == Side.Away && so.Shots.Count == 1 && x.Puck.Live);
        Check(w.Players[so.ShooterId].Slot < 0 && w.Controlled(0) == null, "an away shooter is the AI's; the home human controls nobody");
    }

    /// <summary>W45: shooters and order from the rules: SHL three each with away first, five when set, home first when set, NHL home choosing.</summary>
    void ShootoutRules(MatchSetup setup, Func<RuleSet, World> make)
    {
        Check(ShootoutOrder.Shooters(setup with { Rules = RuleSet.Shl }) == 3 && ShootoutOrder.First(setup with { Rules = RuleSet.Shl }) == Side.Away, "SHL preset: three shooters each, away first");
        Check(ShootoutOrder.Shooters(setup with { Rules = RuleSet.Nhl }) == 3 && ShootoutOrder.First(setup with { Rules = RuleSet.Nhl, Inputs = NoHumans }) == Side.Home, "NHL preset: three shooters each, an AI home club chooses to shoot first");
        var human = setup with { Rules = RuleSet.Nhl, Inputs = [new InputSlot(InputDevice.KeyboardMouse, Side.Home), NoHumans[1]] };
        Check(ShootoutOrder.First(human) == null, "NHL preset: a human home club is asked to choose");
        Check(ShootoutOrder.First(human with { Inputs = [NoHumans[0], new InputSlot(InputDevice.Gamepad1, Side.Away)] }) == Side.Home, "NHL preset: a human away club does not choose; the AI home club shoots first");

        var five = make(RuleSet.Shl with { ShootoutShooters = 5 });
        Run(five, x => x.Match.Phase == MatchPhase.Final, 3);
        Check(OvertimeRules.Shootout(five) is { Rounds: 5, Winner: not null } so5 && so5.Shots[0].Side == Side.Away && so5.Decided(5) == so5.Winner, "five shooters when set: decided over five rounds");

        var home = make(RuleSet.Shl with { ShootoutFirst = ShootoutFirst.Home });
        Run(home, x => x.Match.Phase == MatchPhase.Final, 2);
        bool turns = OvertimeRules.Shootout(home) is { } soh && soh.Shots.Select((s, i) => s.Side == (i % 2 == 0 ? Side.Home : Side.Away)).All(b => b);
        Check(turns && OvertimeRules.Shootout(home)!.Shots.Count > 0, "home first when set: home opens every round");

        var nhl = make(RuleSet.Nhl);
        Run(nhl, x => x.Match.Phase == MatchPhase.Final, 4);
        Check(OvertimeRules.Shootout(nhl) is { Winner: not null } son && son.Shots[0].Side == Side.Home, "NHL preset with an AI home club: home shoots first");
    }

    /// <summary>W45: a human home club under NHL rules picks the order; nobody shoots until it does, and a home club that never picks shoots first.</summary>
    void HomeChooses(World w)
    {
        Run(w, x => OvertimeRules.Shootout(x) != null);
        Run(w, _ => false, limit: OvertimeTuning.BreakSeconds + 2);
        var so = OvertimeRules.Shootout(w);
        Check(so is { ShooterId: -1 } && so.Shots.Count == 0, "NHL: the shootout waits while the human home club chooses");
        Run(w, x => OvertimeRules.Shootout(x) is { ShooterId: >= 0 }, limit: ShootoutOrder.ChooseSeconds + 2);
        Check(so is { ShooterId: >= 0 } && w.Players[so.ShooterId].Side == Side.Home, "a home club that does not pick in time shoots first");
    }
}
