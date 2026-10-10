using System.Numerics;
using StickAndPuck.AI;
using StickAndPuck.Core;
using StickAndPuck.Goalie;
using StickAndPuck.Lines;
using StickAndPuck.Physics;
using StickAndPuck.Players;

namespace StickAndPuck.Rules;

/// <summary>
/// SAP_SCENE=penaltytest: scripted calls under the match rules, the bench and the referee, then a
/// full AI match, no window; prints one line per check and exits with 0 when all pass, 1 otherwise.
/// Covers the W14 "done when": calls happen, the penalised club plays short, time is served correctly.
/// In period 1 home defends -x.
/// </summary>
public sealed class PenaltySelfTest : IScene
{
    const float Period = 600;
    int failures;

    /// <inheritdoc/>
    public string Name => "penaltytest";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var setup = Scenes.DefaultSetup(clubs) with { PeriodSeconds = Period };
        World Make(int period = 1)
        {
            var w = new World(setup, settings);
            Scenes.AddStarters(w);
            MatchFlow.Begin(w, log: false);
            if (period > 1) MatchFlow.StartPeriod(w, period, Period);
            Run(w, x => x.Match.Phase == MatchPhase.Play, null, 15);
            return w;
        }

        Penalties.Strictness = 1f;
        DelayedCall(Make());
        ServedInFull(Make());
        PowerPlayGoal(Make());
        ShortHandedGoal(Make());
        GoalWavesOffDelayedCall(Make());
        TwoAtMost(Make());
        FourOnFour(Make());
        CarriesOverIntermission(Make());
        EndsWithRegulation(Make(3));
        NoCallsWhenOffOrInOvertime(Make(), Make(4));
        HumanHandOff(Make());
        ChangedOffDuringDelay(Make());
        Judgement(Make());
        FullMatch(setup, settings);

        Console.WriteLine(failures == 0 ? "PENALTYTEST: all checks passed" : $"PENALTYTEST: {failures} check(s) failed");
        Environment.Exit(failures == 0 ? 0 : 1);
        return Make();
    }

    void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (!ok) failures++;
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<World, ISystem[]> Sets = new();

    /// <summary>Physics, the match rules, the bench, penalties and the referee: everything but the players moving themselves.</summary>
    static ISystem[] Systems(World world)
    {
        return Sets.GetValue(world, _ => [new LinesSystem(), new PuckPhysics(), new MatchRules(), new OffsideRule(), new IcingRule(), new PenaltyRules(), new Referee.RefereeSystem()]);
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

    static int Skaters(World w, Side side)
    {
        return w.OnIce(side).Count(p => !p.IsGoalie);
    }

    /// <summary>Calls a penalty on a skater and makes it at once by giving his club the puck.</summary>
    static PenaltyState Penalise(World w, Player p, string infraction = Infraction.Tripping)
    {
        Penalties.Signal(w, p.Id, infraction);
        w.Puck.Carrier = p.Side == Side.Home ? Skater(w, Side.Home, PlayingPosition.C).Id : Skater(w, Side.Away, PlayingPosition.C).Id;
        if (w.Puck.Carrier == p.Id) w.Puck.Carrier = w.OnIce(p.Side).First(o => !o.IsGoalie && o != p).Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, null, 1);
        w.Puck.Carrier = -1;
        return w.Match.Penalties.Last(x => x.PlayerId == p.Id);
    }

    static void ToPlay(World w)
    {
        Run(w, x => x.Match.Phase == MatchPhase.Play, null, 15);
    }

    static void PublishGoal(World w, Side scorer)
    {
        bool sent = false;
        Run(w, x => sent, x =>
        {
            x.Events.Publish(new MatchEvent(MatchEventKind.Goal, x.Time, scorer, -1, RinkSpec.NetMouth(1)));
            sent = true;
        }, 1);
    }

    void DelayedCall(World w)
    {
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        var ac = Skater(w, Side.Away, PlayingPosition.C);
        w.Puck.Carrier = ac.Id;
        w.Puck.Position = new Vector3(-10, 0, 3);
        bool signalled = Penalties.Signal(w, lw.Id, Infraction.Tripping);
        Check(signalled && Penalties.Delayed(w).Count == 1 && Referee.Referee.Of(w).Call?.PlayerId == lw.Id, "a trip is signalled: the referee's arm goes up");
        Run(w, _ => false, null, 1);
        Check(w.Match.Phase == MatchPhase.Play && w.Match.Penalties.Count == 0 && lw.OnIce, "play goes on while the fouled club keeps the puck");

        w.Puck.Carrier = Skater(w, Side.Home, PlayingPosition.C).Id;
        var events = Run(w, x => x.Match.Phase != MatchPhase.Play, null, 1);
        w.Puck.Carrier = -1;
        var call = events.Find(e => e.Kind == MatchEventKind.Penalty);
        Check(w.Match.Phase == MatchPhase.Stopped && events.Exists(e => e.Kind == MatchEventKind.Whistle), "the whistle goes when the offending club touches the puck");
        Check(call.Side == Side.Home && call.PlayerId == lw.Id && Math.Abs(call.Strength - Penalties.MinorSeconds(w)) < 0.01f, "a Penalty event names the club, the skater and the minutes");
        Check(w.Match.Penalties is [{ Infraction: Infraction.Tripping, SecondsLeft: var left }] && Math.Abs(left - Penalties.MinorSeconds(w)) < 0.01f
            && Math.Abs(Penalties.MinorSeconds(w) - (2 * 60 * Period / 1200)) < 0.01f, "a two minute minor, shortened with the period");
        Check(!lw.OnIce && Penalties.InBox(w, lw.Id) && lw.Position.Z > RinkSpec.HalfWidth, "the skater goes to the box off the ice");
        Check(MatchFlow.FaceoffDot(w) is { X: < -20 } dot && Math.Abs(dot.X + 20.7f) < 0.01f, "the faceoff is in the penalised club's end");
        Check(string.Equals(Referee.Referee.Of(w).Announced?.Infraction, Infraction.Tripping, StringComparison.Ordinal) && Referee.Referee.Of(w).Call == null, "the referee calls tripping and lowers the arm");
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 4 && Skaters(w, Side.Away) == 5 && Penalties.PowerPlay(w) == Side.Away, "home kills it four on five, away is on the power play");
    }

    void ServedInFull(World w)
    {
        var d = Skater(w, Side.Home, PlayingPosition.D);
        Penalise(w, d, Infraction.Hooking);
        ToPlay(w);
        float start = w.Match.Clock;
        bool boxedOn = false, extra = false;
        int changes = 0;
        void Watch(World x)
        {
            if (Penalties.InBox(x, d.Id))
            {
                boxedOn |= d.OnIce;
                extra |= Skaters(x, Side.Home) != 4;
            }
            // ask for a change every few seconds, so the bench changes all through the kill
            if (x.Tick % 180 == 0) { Bench.RequestChange(x, Side.Home); changes++; }
        }
        Run(w, x => x.Match.Clock <= start - 20, Watch, 30);
        float left = w.Match.Penalties.Single().SecondsLeft;
        Check(Math.Abs(left - (Penalties.MinorSeconds(w) - 20)) < 0.02f, $"the penalty clock runs with the game clock ({MatchFlow.ClockText(left)} left after 0:20)");
        float released = float.NaN;
        Run(w, x => !Penalties.InBox(x, d.Id), Watch, 120);
        released = w.Match.Clock;
        Check(Math.Abs(start - released - Penalties.MinorSeconds(w)) < Game.Step * 1.5f, $"served exactly {MatchFlow.ClockText(Penalties.MinorSeconds(w))} of game time ({start - released:0.00} s)");
        Check(!boxedOn && !extra && changes > 3, "the bench changes during the kill but never sends him on or a fifth skater");
        Check(d.OnIce && Skaters(w, Side.Home) == 5 && w.Match.Penalties.Count == 0 && Vector3.Distance(d.Position, Penalties.BoxDoor(Side.Home)) < 1, "he comes back through the box door: five on five");
    }

    void PowerPlayGoal(World w)
    {
        var rw = Skater(w, Side.Home, PlayingPosition.RW);
        Penalise(w, rw, Infraction.Slashing);
        ToPlay(w);
        PublishGoal(w, Side.Away);
        Check(w.Match.Penalties.Count == 0 && rw.OnIce, "a power play goal ends the penalty");
    }

    void ShortHandedGoal(World w)
    {
        var rw = Skater(w, Side.Home, PlayingPosition.RW);
        Penalise(w, rw);
        ToPlay(w);
        PublishGoal(w, Side.Home);
        Check(w.Match.Penalties.Count == 1 && !rw.OnIce, "a short-handed goal does not end it");
    }

    void GoalWavesOffDelayedCall(World w)
    {
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        w.Puck.Carrier = Skater(w, Side.Away, PlayingPosition.C).Id;
        Penalties.Signal(w, lw.Id, Infraction.Hooking);
        PublishGoal(w, Side.Away);
        Check(Penalties.Delayed(w).Count == 0 && w.Match.Penalties.Count == 0 && lw.OnIce, "a goal while the call is delayed waves it off");
    }

    void TwoAtMost(World w)
    {
        var skaters = w.OnIce(Side.Home).Where(p => !p.IsGoalie).ToList();
        Penalise(w, skaters[0]);
        ToPlay(w);
        w.Puck.Carrier = Skater(w, Side.Away, PlayingPosition.C).Id;
        bool second = Penalties.Signal(w, skaters[1].Id, Infraction.Slashing);
        bool third = Penalties.Signal(w, skaters[2].Id, Infraction.Slashing);
        bool goalie = Penalties.Signal(w, w.OnIce(Side.Away).First(p => p.IsGoalie).Id, Infraction.Slashing);
        Check(second && !third && !goalie, "a club is never more than two short, and goalies are not called");
        w.Puck.Carrier = skaters[2].Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, null, 1);
        w.Puck.Carrier = -1;
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 3 && Penalties.Serving(w, Side.Home) == 2, "two penalties: three on five");
    }

    void FourOnFour(World w)
    {
        Penalise(w, Skater(w, Side.Home, PlayingPosition.LW));
        ToPlay(w);
        Penalise(w, Skater(w, Side.Away, PlayingPosition.LW));
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 4 && Skaters(w, Side.Away) == 4 && Penalties.PowerPlay(w) == null, "one each: four on four, no power play");
    }

    void CarriesOverIntermission(World w)
    {
        w.Match.Clock = 10;
        var c = Skater(w, Side.Home, PlayingPosition.C);
        Penalise(w, c);
        Run(w, x => x.Match.Period == 2 && x.Match.Phase == MatchPhase.Play, null, 60);
        var p = w.Match.Penalties.SingleOrDefault();
        Check(p != null && Math.Abs(p.SecondsLeft - (Penalties.MinorSeconds(w) - 10)) < 0.05f && !c.OnIce && Skaters(w, Side.Home) == 4,
            "a penalty carries over the intermission: still short in period 2 with the rest of the time");
    }

    void EndsWithRegulation(World w)
    {
        w.Match.Clock = 5;
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        Penalise(w, lw);
        Run(w, x => x.Match.Phase is MatchPhase.Overtime or MatchPhase.Final, null, 30);
        Run(w, _ => false, null, 0.1f);
        Check(w.Match.Phase == MatchPhase.Overtime && w.Match.Penalties.Count == 0 && !lw.OnIce, "penalties end with regulation (he waits on the bench)");
    }

    void NoCallsWhenOffOrInOvertime(World off, World overtime)
    {
        off.Settings = off.Settings with { Penalties = false };
        bool a = Penalties.Signal(off, Skater(off, Side.Home, PlayingPosition.C).Id, Infraction.Tripping);
        bool b = Penalties.Signal(overtime, Skater(overtime, Side.Home, PlayingPosition.C).Id, Infraction.Tripping);
        Check(!a && !b, "no calls with penalties off in the settings, or in overtime");
    }

    void HumanHandOff(World w)
    {
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        lw.Slot = 0;
        Penalise(w, lw);
        var now = w.Controlled(0);
        Check(lw.Slot < 0 && now != null && now.Side == Side.Home && now.OnIce && !now.IsGoalie, "a human whose skater is penalised takes a teammate");
    }

    void ChangedOffDuringDelay(World w)
    {
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        w.Puck.Carrier = Skater(w, Side.Away, PlayingPosition.C).Id;
        Penalties.Signal(w, lw.Id, Infraction.Tripping);
        // he changes off before the whistle
        var fresh = w.Players.First(p => p.Side == Side.Home && !p.OnIce && !p.IsGoalie && Squad.GroupOf(p) == Group.Forwards);
        fresh.OnIce = true;
        fresh.Position = lw.Position;
        lw.OnIce = false;
        w.Puck.Carrier = Skater(w, Side.Home, PlayingPosition.C).Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, null, 1);
        w.Puck.Carrier = -1;
        ToPlay(w);
        Check(Penalties.InBox(w, lw.Id) && !lw.OnIce && Skaters(w, Side.Home) == 4, "a skater who changed off during the delay still serves, and his club still plays short");
    }

    void Judgement(World w)
    {
        var checker = Skater(w, Side.Away, PlayingPosition.D);
        var target = Skater(w, Side.Home, PlayingPosition.C);
        w.Puck.Carrier = -1;
        w.Puck.LastTouch = -1;
        target.Position = new Vector3(0, 0, 0);
        target.Heading = MathF.PI / 2;      // facing +x
        checker.Position = new Vector3(-1, 0, 0);   // behind him
        MatchEvent Hit(float strength) => new(MatchEventKind.Check, w.Time, Side.Away, checker.Id, target.Position, strength);

        target.Begin(PlayerAction.Fallen);
        Check(PenaltyModel.Judge(w, Hit(1)) is { Infraction: Infraction.Tripping, Chance: > 0.05f } f1 && f1.PlayerId == checker.Id, "knocking a skater down from behind is tripping");
        target.Begin(PlayerAction.None);
        w.Puck.Carrier = target.Id;
        Check(PenaltyModel.Judge(w, Hit(1)) is { Infraction: Infraction.Hooking }, "a hit from behind on the carrier who stays up is hooking");
        checker.Position = new Vector3(1, 0, 0);    // in front
        w.Puck.Carrier = -1;
        Check(PenaltyModel.Judge(w, Hit(CheckingModel.HardHit + 0.2f)) is { Infraction: Infraction.Slashing, Chance: > 0.08f }, "a hard hit away from the puck is slashing");
        w.Puck.Carrier = target.Id;
        Check(PenaltyModel.Judge(w, Hit(1)) is { Chance: < 0.03f }, "a clean hit on the carrier from the front is rarely called");

        // a fast carrier with a defender on his back
        target.Velocity = new Vector3(6, 0, 0);
        checker.Position = new Vector3(-1, 0, 0.2f);
        checker.Begin(PlayerAction.None);
        bool behind = PenaltyModel.Chases(w, Game.Step).Any(f => f.PlayerId == checker.Id && string.Equals(f.Infraction, Infraction.Hooking, StringComparison.Ordinal));
        checker.Position = new Vector3(1, 0, 0.2f);
        bool ahead = PenaltyModel.Chases(w, Game.Step).Any(f => f.PlayerId == checker.Id);
        Check(behind && !ahead, "a defender trailing a fast carrier can be called for hooking, one ahead of him cannot");
        w.Puck.Carrier = -1;
        target.Velocity = Vector3.Zero;
    }

    /// <summary>Full AI matches: a strict referee for the rules, then a normal one for how often he calls.</summary>
    void FullMatch(MatchSetup setup, Settings settings)
    {
        var (calls, broken, served) = Match(setup with { PeriodSeconds = 240 }, settings, 8f, 1);
        Check(calls >= 4 && broken == 0 && served, $"AI match with a strict referee: {calls} calls, every club short by exactly its penalties at every step");
        int normal = 0, broken2 = 0;
        foreach (int seed in Seeds)
        {
            var (n, b, _) = Match(setup with { PeriodSeconds = 600 }, settings, 1f, seed);
            normal += n;
            broken2 += b;
        }
        float per60 = normal / (30f * Seeds.Length) * 60;
        Check(per60 is >= 4 and <= 16 && broken2 == 0, $"AI match of 3 x 10:00 with a normal referee: {normal / (float)Seeds.Length:0.0} calls a match ({per60:0.0} per 60 minutes, 4 to 16 wanted)");
    }

    static readonly int[] Seeds = [2, 3, 4, 5];

    static (int Calls, int Broken, bool Served) Match(MatchSetup setup, Settings settings, float strictness, int seed)
    {
        var w = new World(setup with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] }, settings);
        Scenes.AddStarters(w);
        MatchFlow.Begin(w, log: false);
        Penalties.Strictness = strictness;
#pragma warning disable S2245 // fixed seeds so the checks repeat
        Penalties.Book(w).Rng = new Random(seed);
        ISystem[] systems =
        [
            new PositioningSystem(new Random(seed)), new DecisionSystem(new Random(seed)), new LinesSystem(), new SkatingSystem(), new CheckingSystem(new Random(seed)),
            new PuckHandlingSystem(new Random(seed)), new GoalieSystem { Dice = new Random(seed) }, new PuckPhysics(), new MatchRules(), new OffsideRule(),
            new IcingRule(), new PenaltyRules(), new Referee.RefereeSystem(),
        ];
#pragma warning restore S2245
        foreach (var s in systems) if (s is PositioningSystem or DecisionSystem or LinesSystem or SkatingSystem or CheckingSystem or PuckHandlingSystem) s.Start(w);
        int calls = 0, broken = 0;
        bool served = true;
        var kinds = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < 4000 * 60 && w.Match.Phase is not (MatchPhase.Final or MatchPhase.Overtime or MatchPhase.Shootout); i++)
        {
            w.Events.Clear();
            foreach (var s in systems) s.Update(w, Game.Step);
            w.Time += Game.Step; w.Tick++;
            foreach (var e in w.Events.Of(MatchEventKind.Penalty))
            {
                calls++;
                string what = w.Match.Penalties.FindLast(p => p.PlayerId == e.PlayerId)?.Infraction ?? "?";
                kinds[what] = kinds.GetValueOrDefault(what) + 1;
            }
            // penalties end with regulation, leaving the box empty for overtime
            foreach (var side in w.Match.Phase is MatchPhase.Final or MatchPhase.Overtime or MatchPhase.Shootout ? [] : new[] { Side.Home, Side.Away })
            {
                if (Skaters(w, side) != 5 - Penalties.Serving(w, side))
                    broken++;
                if (w.Match.Penalties.Exists(p => w.Players[p.PlayerId].OnIce)) broken++;
            }
            served &= w.Match.Penalties.TrueForAll(p => p.SecondsLeft <= Penalties.MinorSeconds(w) + 0.01f);
        }
        Penalties.Strictness = 1f;
        Console.WriteLine($"      {string.Join(", ", kinds.Select(k => $"{k.Value} {k.Key}"))}");
        return (calls, broken, served);
    }
}
