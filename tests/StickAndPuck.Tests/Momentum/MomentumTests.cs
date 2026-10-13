using System.Numerics;
using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Fights;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Momentum;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;
using StickAndPuck.Tests.Support;
using Boost = StickAndPuck.App.Momentum.Momentum;

namespace StickAndPuck.Tests;

/// <summary>
/// The momentum: a goal fills the scoring club's boost and ends the other's, a big hit adds to it
/// and a soft one does not, a fight won boosts the winner, the boost drains over about twenty seconds
/// of live play (not while the puck is dead), gives the club's skaters a second wind, and swells the
/// crowd louder than the crowd after a goal. Then a full all-AI match with lines and momentum
/// running. Formerly SAP_SCENE=momentumtest.
/// </summary>
public sealed class MomentumTests : CheckedTest
{
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<World, ISystem[]> Sets = new();

    /// <summary>A 10 minute match at normal difficulty, run until the opening faceoff is dropped.</summary>
    static World Make()
    {
        var w = new WorldBuilder().Setup(s => s with { PeriodSeconds = 600 }).Settings(s => s with { Difficulty = Difficulty.Normal }).Build();
        Run(w, x => x.Match.Phase == MatchPhase.Play, 15);
        return w;
    }

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

    static void Run(World world, Func<World, bool> until, float limit = 10) => Run(world, until, _ => { }, limit);

    static void Run(World world, Func<World, bool> until, Action<World> each, float limit = 10)
    {
        var systems = Systems(world);
        for (int i = 0; i < limit / Game.Step && !until(world); i++)
        {
            world.Events.Clear();
            each(world);
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

    static MatchEvent Goal(World w, Side side) => new GoalScored(0, side, Skater(w, side), RinkSpec.NetMouth(side == Side.Home ? 1 : -1), 0);

    static MatchEvent Hit(World w, Side side, float strength) => new CheckLanded(0, side, Skater(w, side), Vector3.Zero, strength);

    static int Skater(World w, Side side) => w.Players.First(p => p.Side == side && !p.IsGoalie).Id;

    [Test]
    public void Goals()
    {
        var w = Make();
        Publish(w, Goal(w, Side.Home));
        var s = Boost.State(w);
        Check(Boost.Of(w, Side.Home) > 0.99f && s.Cause[0] == MomentumCause.Goal && Boost.Leader(w) == SideOrNone.Home, "a goal fills the scoring club's boost");
        Publish(w, Goal(w, Side.Away));
        Check(Boost.Of(w, Side.Away) > 0.99f && Boost.Of(w, Side.Home) == 0f, "a goal against ends the other club's boost");
    }

    [Test]
    public void Hits()
    {
        var w = Make();
        Publish(w, Hit(w, Side.Away, 1.0f));
        Check(Boost.Of(w, Side.Away) == 0f, "a soft check gives no boost");
        Publish(w, Hit(w, Side.Away, 1.8f));
        float one = Boost.Of(w, Side.Away);
        Check(Math.Abs(one - MomentumTuning.BigHit) < 0.01f && Boost.State(w).Cause[1] == MomentumCause.BigHit, $"a big hit gives the hitter's club a small boost ({one:0.00})");
        Publish(w, Hit(w, Side.Away, 2f));
        Publish(w, Hit(w, Side.Away, 2f));
        Check(Boost.Of(w, Side.Away) is > 0.99f and <= 1f, "big hits add up, to a full boost at most");
    }

    [Test]
    public void Drain()
    {
        var live = Alone(Make());
        var dead = Alone(Make());
        Publish(live, Goal(live, Side.Home));
        float start = live.Time;
        Run(live, x => x.Time - start >= MomentumTuning.Seconds / 2, Live);
        float half = Boost.Of(live, Side.Home);
        Check(half is > 0.4f and < 0.6f, $"half the boost is gone after ten seconds of play ({half:0.00})");
        Run(live, x => x.Time - start >= MomentumTuning.Seconds + 0.1f, Live);
        Check(Boost.Of(live, Side.Home) == 0f && Boost.Leader(live) == SideOrNone.None, $"the boost is gone after twenty seconds of play ({Boost.Of(live, Side.Home):0.00} and {Boost.Of(live, Side.Away):0.00})");

        Publish(dead, Goal(dead, Side.Home));
        start = dead.Time;
        Run(dead, x => x.Time - start >= 10f, x => x.Puck.Live = false);
        Check(Boost.Of(dead, Side.Home) > 0.99f, "the boost waits while the puck is dead (goal celebration, faceoff)");
    }

    [Test]
    public void SecondWind()
    {
        var w = Make();
        var home = w.OnIce(Side.Home).Where(p => !p.IsGoalie).ToList();
        var away = w.OnIce(Side.Away).Where(p => !p.IsGoalie).ToList();
        foreach (var p in home.Concat(away)) p.Energy = 0.6f;     // above the line change mark, so the same skaters stay on
        Publish(w, Goal(w, Side.Home));
        float start = w.Time;
        Run(w, x => x.Time - start >= MomentumTuning.Seconds, Live);
        float h = home.Average(p => p.Energy), a = away.Average(p => p.Energy);
        Check(h > a + 0.2f, $"the boosted club gets a second wind: energy {h:0.00} against {a:0.00} after twenty seconds");
    }

    [Test]
    public void Crowd()
    {
        var w = Make();
        Publish(w, Goal(w, Side.Home));
        float start = w.Time;
        var s = Boost.State(w);
        float first = s.CrowdVolume;
        Run(w, x => x.Time - start >= 8f);
        float w12 = 0.35f * w.Settings.Volume;
        Check(s.Swells >= 3 && first > w12 * 2, $"after a goal the crowd swells {s.Swells} times in 8 s, first at volume {first:0.00} (W12's goal crowd {w12:0.00})");
        Check(s.CrowdVolume < first && MomentumSystem.CrowdVolume(0.3f, 1f) < MomentumSystem.CrowdVolume(1f, 1f), "the crowd quiets as the boost drains");
    }

    [Test]
    public void FightWon()
    {
        var w = Make();
        var fights = Fighting.State(w);
        fights.ForceAccept = true;
        var checker = w.OnIce(Side.Away).First(p => p.Data.Position == PlayingPosition.C);
        var victim = w.OnIce(Side.Home).First(p => p.Data.Position == PlayingPosition.C);
        Fighting.Offer(w, checker.Id, victim.Id);
        var found = Maybe<Fight>.None;
        Run(w, x => found.Is(f => f.LoserId >= 0), x => { if (!found.HasValue && Fighting.Current(x).TryGet(out var f)) found = f; }, 40);
        if (!found.TryGet(out var fight) || fight.LoserId < 0)
        {
            Check(false, "a fight is won");
            return;
        }
        var winner = w.Players[fight.Other(fight.Of(fight.LoserId).TryGet(out var loser) ? loser : throw new InvalidOperationException("the loser is in the fight")).PlayerId].Side;
        var s = Boost.State(w);
        Check(Boost.Of(w, winner) >= MomentumTuning.FightWon - 0.01f && s.Cause[(int)winner] == MomentumCause.FightWon && s.Boosts[(int)winner] == 1,
            $"winning a fight boosts the winner's club ({winner.ToString().ToLowerInvariant()}, {Boost.Of(w, winner):0.00})");
    }

    [Test]
    public void FullMatch()
    {
        var world = new WorldBuilder().Setup(s => s with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] }).Build();
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
            foreach (var e in world.Events.Of<GoalScored>())
            {
                goals++;
                if (e.Side is { } side && Boost.Leader(world).Is(side) && Boost.Of(world, side) > 0.99f) boostedAfterGoal++;
            }
            world.Time += Game.Step; world.Tick++;
        }
        var st = Boost.State(world);
        Console.WriteLine($"      match {world.Match.Score[0]}-{world.Match.Score[1]}: boosts {st.Boosts[0]}-{st.Boosts[1]}, energy given back {st.Energy[0]:0.0} and {st.Energy[1]:0.0}, crowd swells {st.Swells}");
        Check(world.Match.Phase == MatchPhase.Final, "a full match with momentum plays out to the final");
        Check(goals > 0 && boostedAfterGoal == goals, $"every goal shows a full boost for the scorer ({boostedAfterGoal} of {goals})");
        Check(st.Boosts[0] + st.Boosts[1] > goals, "big hits give boosts in a real match too");
    }
}
