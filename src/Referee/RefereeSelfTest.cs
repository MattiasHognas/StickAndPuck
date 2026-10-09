using System.Numerics;
using StickAndPuck.Core;
using StickAndPuck.Physics;
using StickAndPuck.Rules;

namespace StickAndPuck.Referee;

/// <summary>
/// SAP_SCENE=refereetest: plays a whole match with puck physics, the match rules and the referee, no
/// window, with the match demo's shots so goals, saves and frozen pucks bring faceoffs all over the
/// ice. Prints one line per check and exits with 0 when all pass, 1 otherwise. Covers the W8
/// "done when": the referee drops every faceoff and stays out of the play.
/// </summary>
public sealed class RefereeSelfTest : IScene
{
    int failures;

    /// <inheritdoc/>
    public string Name => "refereetest";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var setup = Scenes.DefaultSetup(clubs) with { PeriodSeconds = 25, Overtime = OvertimeRule.None };
        World Make()
        {
            var w = new World(setup, settings);
            Scenes.AddStarters(w);
            MatchFlow.Begin(w, log: false);
            return w;
        }

        WaitsForReferee(Make());
        FarDot(Make());
        WholeMatch(Make());

        Console.WriteLine(failures == 0 ? "REFEREETEST: all checks passed" : $"REFEREETEST: {failures} check(s) failed");
        Environment.Exit(failures == 0 ? 0 : 1);
        return Make();
    }

    void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (!ok) failures++;
    }

    /// <summary>Runs physics, rules and the referee step by step until <paramref name="until"/> or the limit; <paramref name="each"/> sees every step after it ran.</summary>
    static void Run(World world, Func<World, bool> until, float limit, Action<World>? each = null)
    {
        var systems = new ISystem[] { new PuckPhysics(), new MatchRules(), new RefereeSystem() };
        for (int i = 0; i < limit / Game.Step && !until(world); i++)
        {
            world.Events.Clear();
            foreach (var s in systems) s.Update(world, Game.Step);
            each?.Invoke(world);
            world.Time += Game.Step; world.Tick++;
        }
    }

    static float Distance(Vector3 a, Vector3 b)
    {
        return new Vector2(a.X - b.X, a.Z - b.Z).Length();
    }

    void WaitsForReferee(World w)
    {
        var r = Referee.Of(w);
        r.Position = new Vector3(-20, 0, 11);
        float dropAt = float.NaN;
        Run(w, x => x.Match.Phase == MatchPhase.Play, 20, x => { if (x.Events.Of(MatchEventKind.Faceoff).Any()) dropAt = x.Time; });
        Check(w.Match.Phase == MatchPhase.Play, "the opening faceoff is dropped");
        Check(dropAt > MatchTuning.IntroSeconds + MatchTuning.FaceoffSeconds, $"the drop waits for the referee to skate from the corner ({dropAt:0.0} s)");
        Check(Distance(r.Position, Referee.DropSpot(RinkSpec.CentreDot)) <= RefereeTuning.Arrived, "the referee is at the centre dot when the puck drops");
        Check(r.LateDrops == 0, "no drop needed the safety net");
    }

    void FarDot(World w)
    {
        Run(w, x => x.Match.Phase == MatchPhase.Play, 20);
        var dot = new Vector3(20.7f, 0, 6.7f);
        MatchFlow.Whistle(w, dot);
        var r = Referee.Of(w);
        float start = w.Time, dropAt = float.NaN;
        Run(w, x => x.Match.Phase == MatchPhase.Play, 20, x => { if (x.Events.Of(MatchEventKind.Faceoff).Any()) dropAt = x.Time; });
        Check(w.Match.Phase == MatchPhase.Play && Distance(w.Puck.Position, dot) < 0.5f, "a whistle in the far end is followed by a drop at that dot");
        Check(Distance(r.Position, Referee.DropSpot(dot)) <= RefereeTuning.Arrived, "the referee skated the length of the rink to that dot");
        Check(dropAt - start < 7, $"and got there in good time ({dropAt - start:0.0} s after the whistle)");
    }

    void WholeMatch(World w)
    {
        MatchDemo.Enabled = true;
        var r = Referee.Of(w);
        int setups = 0, drops = 0, atDot = 0, playSteps = 0, nearPuck = 0, nearPlayer = 0;
        float closestPlayer = float.MaxValue, droppedAt = float.NegativeInfinity;
        var last = w.Match.Phase;
        Run(w, x => x.Match.Phase == MatchPhase.Final, 600, x =>
        {
            if (x.Match.Phase == MatchPhase.Faceoff && last != MatchPhase.Faceoff) setups++;
            last = x.Match.Phase;
            if (x.Events.Of(MatchEventKind.Faceoff).Any())
            {
                drops++;
                droppedAt = x.Time;
                if (Distance(r.Position, Referee.DropSpot(x.Puck.Position)) <= RefereeTuning.Arrived) atDot++;
            }
            // the referee stands at the dot for the drop and needs a moment to back off
            if (x.Match.Phase != MatchPhase.Play || x.Time - droppedAt < RefereeTuning.BackOffSeconds) return;
            playSteps++;
            if (Distance(r.Position, x.Puck.Position) < 1.5f) nearPuck++;
            float d = x.Players.Where(p => p.OnIce).Min(p => Distance(r.Position, p.Position));
            closestPlayer = MathF.Min(closestPlayer, d);
            if (d < 1f) nearPlayer++;
        });
        MatchDemo.Enabled = false;
        Check(w.Match.Phase == MatchPhase.Final, "a whole match with the referee reaches the final");
        Check(setups > 3 && drops == setups, $"the referee dropped every faceoff ({drops} of {setups})");
        Check(atDot == drops, $"each from the drop spot beside the dot ({atDot} of {drops})");
        Check(r.LateDrops == 0, $"none needed the safety net ({r.LateDrops})");
        Check(nearPuck <= playSteps / 100, $"the referee stays off the puck in play ({nearPuck} of {playSteps} steps within 1.5 m)");
        // backing off between the wingers right after a drop passes about a metre from each
        Check(closestPlayer >= 0.7f, $"and never bumps a player (closest {closestPlayer:0.0} m, {nearPlayer} of {playSteps} steps within 1 m)");
    }
}
