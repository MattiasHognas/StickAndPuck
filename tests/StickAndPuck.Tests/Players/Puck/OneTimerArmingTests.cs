using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// The read-only one-timer arming the skater renderer poses the wind-up from: a receiver who presses
/// shoot while a teammate's pass is on its way reads as armed until the puck arrives and is struck,
/// and an opponent who intercepts the pass drops it.
/// </summary>
public sealed class OneTimerArmingTests : CheckedTest
{
    static Player Add(World w, Side side, Vector3 at, float heading, Traits traits)
    {
        var p = w.AddPlayer(side, new PlayerData { Position = PlayingPosition.C, Traits = traits }, at);
        p.Heading = heading;
        return p;
    }

    /// <summary>
    /// A passer feeds a teammate who presses shoot two steps after the pass. Returns whether the
    /// receiver read as armed while the puck travelled, whether they still did at the end, and whether
    /// they shot.
    /// </summary>
    static (bool ArmedOnTheWay, bool ArmedAtEnd, bool Shot) Feed(bool opponentInLane)
    {
        var w = new WorldBuilder().Settings(s => s with { OneTimers = true }).NoStarters().NotBegun().Build();
        int end = PuckHandling.AttackEnd(w, Side.Home);
        float face = end > 0 ? MathF.PI / 2 : -MathF.PI / 2;
        var passer = Add(w, Side.Home, new Vector3(end * 12f, 0, -5f), face, new Traits { HockeyIq = 90 });
        var receiver = Add(w, Side.Home, new Vector3(end * 15f, 0, 4f), face, new Traits { ShotPower = 70, PuckControl = 60 });
        if (opponentInLane)
        {
            var mid = (PuckHandling.Blade(passer) + PuckHandling.Blade(receiver)) / 2;
            var cutter = Add(w, Side.Away, Vector3.Zero, -face, new Traits { PuckControl = 90 });
            cutter.Position = mid - PuckHandling.Blade(Vector3.Zero, -face);
        }
        w.Puck.Carrier = passer.Id;
        w.Puck.Position = PuckHandling.Blade(passer);
        var toReceiver = Vector2.Normalize(new Vector2(receiver.Position.X - passer.Position.X, receiver.Position.Z - passer.Position.Z));
#pragma warning disable S2245 // a fixed seed so the checks repeat
        ISystem[] systems = [new SkatingSystem(), new PuckHandlingSystem(new Random(1)), new PuckPhysics()];
#pragma warning restore S2245
        foreach (var s in systems) if (s is not PuckPhysics) s.Start(w);
        bool armedOnTheWay = false, shot = false;
        for (int i = 0; i < 120; i++)
        {
            w.Events.Clear();
            passer.Command = new PlayerCommand { Move = i < 3 ? toReceiver : Vector2.Zero, Pass = i == 2 };
            receiver.Command = new PlayerCommand { Shoot = i >= 5 && i < 40 };
            foreach (var s in systems) s.Update(w, Game.Step);
            armedOnTheWay |= OneTimerArming.Armed(w, receiver.Id) && w.Puck.Carrier < 0;
            shot |= w.Events.Current.OfType<ShotTaken>().Any(e => e.ShooterId == receiver.Id);
            w.Time += Game.Step; w.Tick++;
        }
        return (armedOnTheWay, OneTimerArming.Armed(w, receiver.Id), shot);
    }

    [Test]
    public void ArmedUntilStruck()
    {
        var (onTheWay, atEnd, shot) = Feed(false);
        Check(onTheWay, "shoot pressed while the pass comes arms the receiver");
        Check(shot && !atEnd, "the one-timer fires on arrival and the receiver is no longer armed");
    }

    [Test]
    public void InterceptionDropsIt()
    {
        var (_, atEnd, shot) = Feed(true);
        Check(!shot && !atEnd, "an intercepted pass leaves the receiver unarmed, with no shot");
    }
}
