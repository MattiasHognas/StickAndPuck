using System.Numerics;
using StickAndPuck.AI;
using StickAndPuck.Core;
using StickAndPuck.Fights;
using StickAndPuck.Goalie;
using StickAndPuck.Lines;
using StickAndPuck.Physics;
using StickAndPuck.Players;
using StickAndPuck.Rules;

namespace StickAndPuck.Momentum;

/// <summary>
/// SAP_SCENE=momentumtest: headless checks for W29's momentum, one line per check; exits 0 when all
/// pass. A goal fills the scoring club's boost and ends the other's, a big hit adds to it and a soft
/// one does not, a fight won boosts the winner, the boost drains over about twenty seconds of live
/// play (not while the puck is dead), gives the club's skaters a second wind, and swells the crowd
/// louder than W12's crowd after a goal. Then a full all-AI match with lines and momentum running.
/// </summary>
public sealed class MomentumSelfTest : IScene
{
    int failures;

    /// <inheritdoc/>
    public string Name => "momentumtest";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var setup = Scenes.DefaultSetup(clubs) with { PeriodSeconds = 600 };
        World Make()
        {
            var w = new World(setup, settings with { Difficulty = Difficulty.Normal });
            Scenes.AddStarters(w);
            MatchFlow.Begin(w, log: false);
            Run(w, x => x.Match.Phase == MatchPhase.Play, null, 15);
            return w;
        }

        Goals(Make());
        Hits(Make());
        Drain(Alone(Make()), Alone(Make()));
        SecondWind(Make());
        Crowd(Make());
        FightWon(Make());
        FullMatch(clubs, settings);

        Console.WriteLine(failures == 0 ? "MOMENTUMTEST: all checks passed" : $"MOMENTUMTEST: {failures} check(s) failed");
        Environment.Exit(failures == 0 ? 0 : 1);
        return Make();
    }

    void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (!ok) failures++;
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<World, ISystem[]> Sets = new();

    // the match rules, the bench, fights and momentum; nobody skates on their own
    static ISystem[] Systems(World world)
    {
        return Sets.GetValue(world, _ => [new LinesSystem(), new PuckPhysics(), new MatchRules(), new PenaltyRules(), new FightSystem(), new MomentumSystem()]);
    }

    // momentum on its own, so the match rules do not stop play under it
    static World Alone(World w)
    {
        Sets.AddOrUpdate(w, [new MomentumSystem()]);
        return w;
    }

    static void Run(World world, Func<World, bool> until, Action<World>? each = null, float limit = 10)
    {
        var systems = Systems(world);
        for (int i = 0; i < limit / Game.Step && !until(world); i++)
        {
            world.Events.Clear();
            each?.Invoke(world);
            foreach (var s in systems) s.Update(world, Game.Step);
            world.Time += Game.Step; world.Tick++;
        }
    }

    static void Publish(World w, MatchEvent e)
    {
        bool sent = false;
        Run(w, _ => sent, x => { x.Events.Publish(e with { Time = x.Time }); sent = true; }, 1);
    }

    // nobody skates in these worlds, so keep the puck live as if play went on
    static void Live(World w) => w.Puck.Live = true;

    static MatchEvent Goal(Side side) => new(MatchEventKind.Goal, 0, side, -1, RinkSpec.NetMouth(side == Side.Home ? 1 : -1));

    static MatchEvent Hit(Side side, float strength) => new(MatchEventKind.Check, 0, side, -1, Vector3.Zero, strength);

    void Goals(World w)
    {
        Publish(w, Goal(Side.Home));
        var s = Momentum.State(w);
        Check(Momentum.Of(w, Side.Home) > 0.99f && s.Cause[0] == MomentumCause.Goal && Momentum.Leader(w) == Side.Home, "a goal fills the scoring club's boost");
        Publish(w, Goal(Side.Away));
        Check(Momentum.Of(w, Side.Away) > 0.99f && Momentum.Of(w, Side.Home) == 0f, "a goal against ends the other club's boost");
    }

    void Hits(World w)
    {
        Publish(w, Hit(Side.Away, 1.0f));
        Check(Momentum.Of(w, Side.Away) == 0f, "a soft check gives no boost");
        Publish(w, Hit(Side.Away, 1.8f));
        float one = Momentum.Of(w, Side.Away);
        Check(Math.Abs(one - MomentumTuning.BigHit) < 0.01f && Momentum.State(w).Cause[1] == MomentumCause.BigHit, $"a big hit gives the hitter's club a small boost ({one:0.00})");
        Publish(w, Hit(Side.Away, 2f));
        Publish(w, Hit(Side.Away, 2f));
        Check(Momentum.Of(w, Side.Away) is > 0.99f and <= 1f, "big hits add up, to a full boost at most");
    }

    void Drain(World live, World dead)
    {
        Publish(live, Goal(Side.Home));
        float start = live.Time;
        Run(live, x => x.Time - start >= MomentumTuning.Seconds / 2, Live);
        float half = Momentum.Of(live, Side.Home);
        Check(half is > 0.4f and < 0.6f, $"half the boost is gone after ten seconds of play ({half:0.00})");
        Run(live, x => x.Time - start >= MomentumTuning.Seconds + 0.1f, Live);
        Check(Momentum.Of(live, Side.Home) == 0f && Momentum.Leader(live) == null, $"the boost is gone after twenty seconds of play ({Momentum.Of(live, Side.Home):0.00} and {Momentum.Of(live, Side.Away):0.00})");

        Publish(dead, Goal(Side.Home));
        start = dead.Time;
        Run(dead, x => x.Time - start >= 10f, x => x.Puck.Live = false);
        Check(Momentum.Of(dead, Side.Home) > 0.99f, "the boost waits while the puck is dead (goal celebration, faceoff)");
    }

    void SecondWind(World w)
    {
        var home = w.OnIce(Side.Home).Where(p => !p.IsGoalie).ToList();
        var away = w.OnIce(Side.Away).Where(p => !p.IsGoalie).ToList();
        foreach (var p in home.Concat(away)) p.Energy = 0.6f;     // above the line change mark, so the same skaters stay on
        Publish(w, Goal(Side.Home));
        float start = w.Time;
        Run(w, x => x.Time - start >= MomentumTuning.Seconds, Live);
        float h = home.Average(p => p.Energy), a = away.Average(p => p.Energy);
        Check(h > a + 0.2f, $"the boosted club gets a second wind: energy {h:0.00} against {a:0.00} after twenty seconds");
    }

    void Crowd(World w)
    {
        Publish(w, Goal(Side.Home));
        float start = w.Time;
        var s = Momentum.State(w);
        float first = s.CrowdVolume;
        Run(w, x => x.Time - start >= 8f);
        float w12 = 0.35f * w.Settings.Volume;
        Check(s.Swells >= 3 && first > w12 * 2, $"after a goal the crowd swells {s.Swells} times in 8 s, first at volume {first:0.00} (W12's goal crowd {w12:0.00})");
        Check(s.CrowdVolume < first && MomentumSystem.CrowdVolume(0.3f, 1f) < MomentumSystem.CrowdVolume(1f, 1f), "the crowd quiets as the boost drains");
    }

    void FightWon(World w)
    {
        var fights = Fighting.State(w);
        fights.ForceAccept = true;
        var checker = w.OnIce(Side.Away).First(p => p.Data.Position == PlayingPosition.C);
        var victim = w.OnIce(Side.Home).First(p => p.Data.Position == PlayingPosition.C);
        Fighting.Offer(w, checker.Id, victim.Id);
        Fight? fight = null;
        Run(w, x => fight is { LoserId: >= 0 }, x => fight ??= Fighting.Current(x), 40);
        if (fight is not { LoserId: >= 0 })
        {
            Check(false, "a fight is won");
            return;
        }
        var winner = w.Players[fight.Other(fight.Of(fight.LoserId)!).PlayerId].Side;
        var s = Momentum.State(w);
        Check(Momentum.Of(w, winner) >= MomentumTuning.FightWon - 0.01f && s.Cause[(int)winner] == MomentumCause.FightWon && s.Boosts[(int)winner] == 1,
            $"winning a fight boosts the winner's club ({winner.ToString().ToLowerInvariant()}, {Momentum.Of(w, winner):0.00})");
    }

    void FullMatch(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var setup = Scenes.DefaultSetup(clubs) with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] };
        var world = new World(setup, settings);
        Scenes.AddStarters(world);
        MatchFlow.Begin(world, log: false);
#pragma warning disable S2245 // fixed seed so the match repeats
        ISystem[] systems =
        [
            new PositioningSystem(new Random(1)), new DefenceSystem(new Random(1)), new SpecialTeamsSystem(new Random(1)), new DecisionSystem(new Random(1)), new LinesSystem(), new SkatingSystem(), new CheckingSystem(new Random(1)),
            new PuckHandlingSystem(new Random(1)), new GoalieSystem { Dice = new Random(1) }, new PuckPhysics(), new MatchRules(), new OvertimeRules(), new MomentumSystem(),
        ];
#pragma warning restore S2245
        foreach (var s in systems) if (s is not (PuckPhysics or GoalieSystem)) s.Start(world);
        int goals = 0, boostedAfterGoal = 0, steps = 3 * 60 * 60 * 60;
        for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Final; i++)
        {
            world.Events.Clear();
            foreach (var s in systems) s.Update(world, Game.Step);
            foreach (var e in world.Events.Of(MatchEventKind.Goal))
            {
                goals++;
                if (e.Side is { } side && Momentum.Leader(world) == side && Momentum.Of(world, side) > 0.99f) boostedAfterGoal++;
            }
            world.Time += Game.Step; world.Tick++;
        }
        var st = Momentum.State(world);
        Console.WriteLine($"      match {world.Match.Score[0]}-{world.Match.Score[1]}: boosts {st.Boosts[0]}-{st.Boosts[1]}, energy given back {st.Energy[0]:0.0} and {st.Energy[1]:0.0}, crowd swells {st.Swells}");
        Check(world.Match.Phase == MatchPhase.Final, "a full match with momentum plays out to the final");
        Check(goals > 0 && boostedAfterGoal == goals, $"every goal shows a full boost for the scorer ({boostedAfterGoal} of {goals})");
        Check(st.Boosts[0] + st.Boosts[1] > goals, "big hits give boosts in a real match too");
    }
}
