using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// The dekes and one-timers, with skating, checking, puck handling and puck physics. A carrier
/// rushes a defender who checks once in reach: with no deke the carrier loses the puck, a deke
/// pressed in the sweet spot beats the defender most times, a press too early mostly fails, and
/// auto-deke beats them less often than a timed press. A shot pressed while a pass is on its way is
/// struck on arrival without the receiver ever carrying the puck, and with
/// <see cref="Settings.OneTimers"/> off the receiver takes the pass first. Formerly
/// SAP_SCENE=deketest.
/// </summary>
public sealed class DekeTests : CheckedTest
{
    const int Trials = 40;
    int seed = 1;

    /// <summary>An empty world on hard, where the AI plays the human's carrier in full (see <see cref="StickAndPuck.App.AI.HumanEdge"/>).</summary>
    static World Make(bool autoDeke = false, bool oneTimers = true)
    {
        return new WorldBuilder().Settings(s => s with { AutoDeke = autoDeke, OneTimers = oneTimers, Difficulty = Difficulty.Hard }).NoStarters().NotBegun().Build();
    }

    List<MatchEvent> Run(World w, float seconds, Action<int> each)
    {
#pragma warning disable S2245 // a fixed seed per run so the checks repeat
        ISystem[] systems = [new SkatingSystem(), new CheckingSystem(new Random(seed)), new PuckHandlingSystem(new Random(seed + 1000)), new PuckPhysics()];
#pragma warning restore S2245
        seed++;
        foreach (var s in systems) if (s is not PuckPhysics) s.Start(w);
        var events = new List<MatchEvent>();
        for (int i = 0; i < (int)(seconds / Game.Step); i++)
        {
            w.Events.Clear();
            each(i);
            foreach (var s in systems) s.Update(w, Game.Step);
            events.AddRange(w.Events.Current);
            w.Time += Game.Step; w.Tick++;
        }
        return events;
    }

    static Player Add(World w, Side side, Vector3 at, float heading, Traits traits)
    {
        var p = w.AddPlayer(side, new PlayerData { Position = PlayingPosition.C, Traits = traits }, at);
        p.Heading = heading;
        return p;
    }

    /// <summary>
    /// A carrier skates at a defender who skates back at them and checks the moment the carrier is
    /// in reach. The carrier presses deke once the bodies are <paramref name="pressAt"/> metres apart
    /// (never when NaN). Returns whether the carrier got past with the puck, and the closest the
    /// carrier came to the defender's line sideways (for the shimmy check).
    /// </summary>
    bool Rush(float pressAt, bool auto = false, bool human = true, int puckControl = 70, float defenderAt = 9f)
    {
        var w = Make(auto, true);
        var carrier = Add(w, Side.Home, Vector3.Zero, MathF.PI / 2, new Traits { PuckControl = puckControl, Checking = 40 });
        var defender = Add(w, Side.Away, new Vector3(defenderAt, 0, 0.3f), -MathF.PI / 2, new Traits { Checking = 85 });
        carrier.Velocity = new Vector3(5, 0, 0);
        if (human) carrier.Slot = 0;
        w.Puck.Carrier = carrier.Id;
        w.Puck.Position = PuckHandling.Blade(carrier);
        bool pressed = false, checkedOnce = false;
        Run(w, 2.5f, i =>
        {
            float d = Vector2.Distance(new Vector2(carrier.Position.X, carrier.Position.Z), new Vector2(defender.Position.X, defender.Position.Z));
            bool press = !pressed && !float.IsNaN(pressAt) && d <= pressAt;
            pressed |= press;
            carrier.Command = new PlayerCommand { Move = Vector2.UnitX, Deke = press };
            var to = new Vector2(carrier.Position.X - defender.Position.X, carrier.Position.Z - defender.Position.Z);
            bool check = !checkedOnce && defender.Action == PlayerAction.None && CheckingModel.Target(w, defender) == carrier;
            checkedOnce |= check;
            defender.Command = new PlayerCommand { Move = to.LengthSquared() > 0.01f ? Vector2.Normalize(to) : -Vector2.UnitX, Check = check };
        });
        return w.Puck.Carrier == carrier.Id && carrier.Position.X > defender.Position.X + 1f;
    }

    int Beats(float pressAt, bool auto = false, bool human = true, int puckControl = 70)
    {
        return Enumerable.Range(0, Trials).Count(_ => Rush(pressAt, auto, human, puckControl));
    }

    /// <summary>
    /// The dekes, then the one-timers: every run takes the next seed from the same counter, so the
    /// one-timers' seeds follow on from the dekes' and the two stay in one test, in this order.
    /// </summary>
    [Test]
    public void DekesThenOneTimers()
    {
        Dekes();
        OneTimers();
    }

    void Dekes()
    {
        int none = Beats(float.NaN), timed = Beats(2.0f), early = Beats(3.45f);
        int auto = Beats(float.NaN, auto: true), autoAi = Beats(float.NaN, auto: true, human: false);
        int clumsy = Beats(2.0f, puckControl: 5);
        Check(none <= Trials / 10, $"with no deke the defender's check wins the puck ({Trials - none}/{Trials} lost)");
        Check(timed >= Trials * 3 / 4, $"a deke pressed in the sweet spot beats the defender ({timed}/{Trials})");
        Check(early < timed / 2, $"a deke pressed too early mostly fails ({early}/{Trials} vs {timed} timed)");
        Check(clumsy < timed, $"puck control matters: a clumsy carrier's timed deke beats them less ({clumsy} vs {timed}/{Trials})");
        Check(auto >= Trials / 8 && auto <= timed - Trials / 5, $"auto-deke works but fails more often than a timed press ({auto} vs {timed}/{Trials})");
        Check(autoAi <= Trials / 10, $"auto-deke is for the human: an AI carrier is left alone ({autoAi}/{Trials})");

        // a shimmy in open ice: nothing to beat, the puck stays and the carrier steps sideways
        var w = Make(false, true);
        var p = Add(w, Side.Home, Vector3.Zero, MathF.PI / 2, new Traits { PuckControl = 70 });
        p.Velocity = new Vector3(5, 0, 0);
        w.Puck.Carrier = p.Id;
        w.Puck.Position = PuckHandling.Blade(p);
        float widest = 0;
        Run(w, 1f, i =>
        {
            p.Command = new PlayerCommand { Move = Vector2.UnitX, Deke = i == 5 };
            widest = Math.Max(widest, MathF.Abs(p.Position.Z));
        });
        Check(w.Puck.Carrier == p.Id && widest > 0.5f, $"a deke in open ice keeps the puck and steps aside ({widest:0.00} m)");
    }

    [Test]
    public void Model()
    {
        Check(DekeModel.Timing(2f) > 0.99f && DekeModel.Timing(0.5f) < 1e-6f && DekeModel.Timing(DekeModel.Range + 0.1f) < 1e-6f, "timing peaks in the sweet spot and is gone too close or too far");
        Check(DekeModel.Chance(1f, 50) > DekeModel.AutoChance(50) + 0.3f, $"a timed press is far likelier than auto-deke ({DekeModel.Chance(1f, 50):0.00} vs {DekeModel.AutoChance(50):0.00})");
        Check(DekeModel.Chance(1f, 95) > DekeModel.Chance(1f, 10) && DekeModel.AutoChance(95) > DekeModel.AutoChance(10), "better puck control dekes better, by hand and auto");
    }

    /// <summary>
    /// A passer sends the puck across to a teammate facing the net who holds shoot from
    /// <paramref name="shootFrom"/> for <paramref name="shootFor"/> steps. Returns whether the
    /// receiver ever carried the puck, the step they shot on (or -1), and the step they first had it.
    /// </summary>
    (bool Carried, int ShotStep, int HadStep, float ShotSpeed) Pass(bool oneTimers, int shootFrom, int shootFor, bool opponentInLane = false)
    {
        var w = Make(false, oneTimers);
        int end = PuckHandling.AttackEnd(w, Side.Home);
        float face = end > 0 ? MathF.PI / 2 : -MathF.PI / 2;
        var passer = Add(w, Side.Home, new Vector3(end * 12f, 0, -5f), face, new Traits { HockeyIq = 90 });
        var receiver = Add(w, Side.Home, new Vector3(end * 15f, 0, 4f), face, new Traits { ShotPower = 70, ShotAccuracy = 70, PuckControl = 60 });
        if (opponentInLane)
        {
            // stick blade right in the lane, halfway between the passer's blade and the receiver's
            var mid = (PuckHandling.Blade(passer) + PuckHandling.Blade(receiver)) / 2;
            var cutter = Add(w, Side.Away, Vector3.Zero, -face, new Traits { PuckControl = 90 });
            cutter.Position = mid - PuckHandling.Blade(Vector3.Zero, -face);
        }
        w.Puck.Carrier = passer.Id;
        w.Puck.Position = PuckHandling.Blade(passer);
        var toReceiver = Vector2.Normalize(new Vector2(receiver.Position.X - passer.Position.X, receiver.Position.Z - passer.Position.Z));
        bool carried = false;
        int shotStep = -1, hadStep = -1;
        float shotSpeed = 0;
        var events = Run(w, 2.5f, i =>
        {
            passer.Command = new PlayerCommand { Move = i < 3 ? toReceiver : Vector2.Zero, Pass = i == 2 };
            receiver.Command = new PlayerCommand { Shoot = i >= shootFrom && i < shootFrom + shootFor };
            if (w.Puck.Carrier == receiver.Id) { carried = true; if (hadStep < 0) hadStep = i; }
        });
        foreach (var e in events.OfType<ShotTaken>().Where(e => e.ShooterId == receiver.Id).Take(1))
        {
            shotStep = (int)MathF.Round(e.Time / Game.Step);
            shotSpeed = e.Speed;
        }
        return (carried, shotStep, hadStep, shotSpeed);
    }

    void OneTimers()
    {
        var on = Pass(true, 8, 30);
        Check(on.ShotStep >= 0 && !on.Carried, $"a shot held as the pass comes is struck on arrival, never carried (shot at step {on.ShotStep})");
        Check(on.ShotSpeed > 25f, $"a one-timer is struck hard ({on.ShotSpeed:0.0} m/s)");
        var tap = Pass(true, 8, 1);
        Check(tap.ShotStep >= 0 && !tap.Carried, "a tap of shoot while the pass is on its way still one-times it");
        var off = Pass(false, 8, 70);
        Check(off.Carried && off.ShotStep > off.HadStep, $"with one-timers off the receiver takes the pass first, then shoots (had it at {off.HadStep}, shot at {off.ShotStep})");
        var offTap = Pass(false, 8, 1);
        Check(offTap.Carried && offTap.ShotStep < 0, "with one-timers off a tap before the puck arrives is a swing at nothing");
        var late = Pass(true, 200, 30);
        Check(late.Carried && late.ShotStep < 0, "no shoot press, no one-timer: the receiver takes the pass");
        var cut = Pass(true, 8, 30, opponentInLane: true);
        Check(cut.ShotStep < 0, "an opponent in the lane intercepts: the one-timer never fires");
    }
}
