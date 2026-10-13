using System.Numerics;
using StickAndPuck.AI;
using StickAndPuck.Core;
using StickAndPuck.Physics;
using StickAndPuck.Players;
using StickAndPuck.Pregame;
using StickAndPuck.Rules;
using StickAndPuck.Tests.Support;
using Warmup = StickAndPuck.Pregame.Pregame;

namespace StickAndPuck.Tests;

/// <summary>
/// The warm-up (W28), run with positioning, skating, puck physics and the match rules: both squads
/// skate around until start is pressed, then the opening faceoff follows; and a match without a
/// human keeps the plain intro. Formerly SAP_SCENE=warmuptest.
/// </summary>
public sealed class PregameTests : CheckedTest
{
    /// <summary>A 25-second match with no overtime, the starters on and the match flow begun.</summary>
    static World Make(bool human)
    {
        var builder = new WorldBuilder().Setup(s => s with { PeriodSeconds = 25, Overtime = OvertimeRule.None });
        return (human ? builder.Human() : builder).Build();
    }

    [Test]
    public void SkatesUntilStart()
    {
        var w = Make(human: true);
        var skaters = w.Players.Where(p => !p.IsGoalie).ToList();
        var travelled = skaters.ToDictionary(p => p.Id, _ => 0f);
        var last = skaters.ToDictionary(p => p.Id, p => p.Position);
        bool stayedHome = true;
        float topSpeed = 0;
        Run(w, _ => false, 12f, world =>
        {
            foreach (var p in skaters)
            {
                travelled[p.Id] += Vector3.Distance(last[p.Id], p.Position);
                last[p.Id] = p.Position;
                int end = FaceoffFormation.DefendedEnd(world.Match, p.Side);
                if (Math.Sign(p.Position.X) == -end) stayedHome = false;
                topSpeed = Math.Max(topSpeed, p.Speed);
            }
        });
        Check(w.Match.Phase == MatchPhase.Intro && Warmup.IsWarmingUp(w), "after 12 s with nobody pressing start the match is still in its intro");
        Check(skaters.All(p => travelled[p.Id] > 30f), $"every skater has skated at least 30 m (least {travelled.Values.Min():0} m: {string.Join(" ", skaters.Select(p => $"{p.Data.Position}{travelled[p.Id]:0}"))})");
        Check(stayedHome, "nobody crossed centre during the warm-up");
        Check(topSpeed > 3f, $"the warm-up is skated at a real pace (top {topSpeed:0.0} m/s)");
        Check(w.Players.Where(p => p.IsGoalie).All(p => MathF.Abs(p.Position.X) > RinkSpec.GoalLineX - 4f), "both goalies stay by their nets");

        Check(Warmup.Start(w), "start is taken while warming up");
        float pressedAt = w.Time;
        Run(w, world => world.Match.Phase == MatchPhase.Faceoff, 5f);
        Check(w.Match.Phase == MatchPhase.Faceoff && w.Time - pressedAt < 3f, $"the opening faceoff follows start ({w.Time - pressedAt:0.0} s after the press)");
        Check(skaters.Count(p => p.Side == Side.Home) == 5 && skaters.All(p => p.Speed < 0.01f), "the skaters stand on their faceoff spots");
        Run(w, world => world.Match.Phase == MatchPhase.Play, 3f);
        Check(w.Match.Phase == MatchPhase.Play && w.Puck.Live, "the puck drops and play starts");
        Check(!Warmup.Start(w), "start does nothing once the game is on");
    }

    [Test]
    public void NoHumanNoWarmup()
    {
        var w = Make(human: false);
        Run(w, world => world.Match.Phase != MatchPhase.Intro, 5f);
        Check(w.Match.Phase == MatchPhase.Faceoff && w.Time <= MatchTuning.IntroSeconds + Game.Step * 2, $"an AI-only match keeps the plain intro ({w.Time:0.0} s)");
    }

    [Test]
    public void Forced()
    {
        var w = Make(human: false);
        Warmup.Enable(w);
        Run(w, world => world.Match.Phase != MatchPhase.Intro, 5f);
        Check(w.Match.Phase == MatchPhase.Intro && Warmup.Elapsed(w) > 4.5f, "Pregame.Enable asks for the warm-up in a match with no human");
        Warmup.Enable(w, on: false);
        Run(w, world => world.Match.Phase != MatchPhase.Intro, 5f);
        Check(w.Match.Phase == MatchPhase.Faceoff, "Pregame.Enable(false) lets the intro end");
    }

    /// <summary>Runs the AI, the warm-up, skating, physics and the rules step by step until <paramref name="until"/> or the limit.</summary>
    static void Run(World world, Func<World, bool> until, float limit, Action<World>? each = null)
    {
#pragma warning disable S2245 // a fixed seed, so the test repeats
        var systems = new ISystem[] { new PositioningSystem(new Random(28)), new PregameSystem(), new SkatingSystem(), new PuckPhysics(), new MatchRules() };
#pragma warning restore S2245
        for (int i = 0; i < limit / Game.Step && !until(world); i++)
        {
            world.Events.Clear();
            foreach (var s in systems) s.Update(world, Game.Step);
            each?.Invoke(world);
            world.Time += Game.Step; world.Tick++;
        }
    }
}
