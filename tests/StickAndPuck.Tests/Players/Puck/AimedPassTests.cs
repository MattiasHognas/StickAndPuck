using System.Numerics;
using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// Aimed human passes: after a human's pass, control follows the puck to the receiver, who
/// skates to meet it by himself, the stick still held from the pass ignored, until the player
/// steers him with fresh input; and a human's pass with nobody in the 70° cone goes to the nearest
/// open teammate within 40 metres.
/// </summary>
public sealed class AimedPassTests : CheckedTest
{
    const float Right = MathF.PI / 2;
    const int PassAt = 10;

    /// <summary>A begun match in play with nobody on the ice: each test adds its own skaters.</summary>
    static World Make()
    {
        var w = new WorldBuilder().NoStarters().Build();
        w.Match.Phase = MatchPhase.Play;
        w.Puck.Live = true;
        return w;
    }

    static Player Add(World w, Side side, Vector3 at, float heading = Right)
    {
        var p = w.AddPlayer(side, new PlayerData { Position = PlayingPosition.C, Traits = new Traits() }, at);
        p.Heading = heading;
        return p;
    }

    static Player AddBladeAt(World w, Side side, Vector3 blade, float heading = Right)
    {
        return Add(w, side, blade - PuckHandling.Blade(Vector3.Zero, heading), heading);
    }

    static void Give(World w, Player p)
    {
        w.Puck.Carrier = p.Id;
        w.Puck.Position = PuckHandling.Blade(p);
        w.Puck.Velocity = Vector3.Zero;
    }

    /// <summary>
    /// Runs decisions, skating, handling and physics. Before each step the slot's player gets the
    /// stick and buttons <paramref name="input"/> gives for that step, the way the input system
    /// writes them; <paramref name="seen"/> sees each step's commands once decisions have run.
    /// </summary>
    static List<MatchEvent> Run(World w, float seconds, Func<int, PlayerCommand> input, Action<int> seen)
    {
#pragma warning disable S2245 // fixed seeds so the checks repeat
        ISystem[] systems = [new DecisionSystem(new Random(1)), new SkatingSystem(), new PuckHandlingSystem(new Random(1)), new PuckPhysics()];
#pragma warning restore S2245
        foreach (var s in systems) if (s is not PuckPhysics) s.Start(w);
        var events = new List<MatchEvent>();
        int steps = (int)MathF.Round(seconds / Game.Step);
        for (int i = 0; i < steps; i++)
        {
            w.Events.Clear();
            if (w.Controlled(0).TryGet(out var human)) human.Command = input(i);
            foreach (var s in systems)
            {
                s.Update(w, Game.Step);
                if (s is DecisionSystem) seen(i);
            }
            events.AddRange(w.Events.Current);
            w.Time += Game.Step; w.Tick++;
        }
        return events;
    }

    static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    /// <summary>The usual pair: a human carrier at the left, a teammate up and across the ice, standing still.</summary>
    static (World W, Player Passer, Player Receiver, Vector2 Stick) Pair()
    {
        var w = Make();
        var a = Add(w, Side.Home, new Vector3(-10, 0, 0));
        var b = AddBladeAt(w, Side.Home, new Vector3(2, 0, -6), heading: -Right);
        a.Slot = 0;
        Give(w, a);
        var stick = Vector2.Normalize(Flat(PuckHandling.Blade(b)) - Flat(w.Puck.Position));
        return (w, a, b, stick);
    }

    [Test]
    public void ReceiverMeetsThePass()
    {
        // the stick held toward the receiver through the pass and on: it would carry him away from the puck
        var (w, a, b, stick) = Pair();
        int got = -1, steered = 0, away = 0, held = 0;
        Run(w, 4f, i => new PlayerCommand { Move = stick, Pass = i == PassAt }, i =>
        {
            if (got < 0 && w.Puck.Carrier == b.Id) got = i;
            if (i <= PassAt || got >= 0 || b.Slot != 0 || w.Puck.Carrier >= 0) return;
            steered++;
            var toPuck = Flat(w.Puck.Position) - Flat(b.Position);
            if (Vector2.Dot(b.Command.Move, toPuck) < 0) away++;
            if (Vector2.Distance(b.Command.Move, stick) < 1e-3f) held++;
        });
        Check(b.Slot == 0 && a.Slot == -1, "control follows the pass to the receiver");
        Check(got > 0 && (got - PassAt) * Game.Step < 2.5f, $"the receiver of a human pass meets it and has the puck ({(got - PassAt) * Game.Step:0.00} s after the pass)");
        Check(steered > 5 && away == 0 && held == 0, $"while it comes he stays on its line or skates to it, never away along the held stick ({steered} steps, {away} away from it, {held} on the stick)");

    }

    [Test]
    public void MovingReceiverMeetsThePass()
    {
        // the receiver skating away up the ice, the stick still held toward him: led, met and taken
        var (w2, a2, b2, _) = Pair();
        b2.Heading = Right;
        b2.Position = new Vector3(-2, 0, -6) - PuckHandling.Blade(Vector3.Zero, Right);
        b2.Velocity = new Vector3(5f, 0, 0);
        var stick2 = Vector2.Normalize(new Vector2(2, -6) - Flat(w2.Puck.Position));
        int got2 = -1, away2 = 0, steps2 = 0;
        Run(w2, 4f, i => new PlayerCommand { Move = stick2, Pass = i == PassAt }, i =>
        {
            if (got2 < 0 && w2.Puck.Carrier == b2.Id) got2 = i;
            if (i <= PassAt || got2 >= 0 || b2.Slot != 0 || w2.Puck.LastTouch != a2.Id) return;
            steps2++;
            if (Vector2.Distance(b2.Command.Move, stick2) < 1e-3f) away2++;
        });
        Check(got2 > 0 && (got2 - PassAt) * Game.Step < 2.5f, $"a receiver skating away meets a led pass too ({(got2 - PassAt) * Game.Step:0.00} s after it)");
        Check(steps2 > 5 && away2 == 0, $"and is steered by the meet, not the held stick, while it comes ({steps2 - away2} of {steps2} steps)");

    }

    [Test]
    public void OneTimerReceiverIsMoved()
    {
        // held shoot to one-time it: still moved to meet it, and the one-timer goes
        var (w3, a3, b3, stick3) = Pair();
        int moved = 0, steps3 = 0;
        bool struck = false;
        var events = Run(w3, 4f, i => new PlayerCommand { Move = stick3, Pass = i == PassAt, Shoot = i > PassAt + 3 }, i =>
        {
            struck |= i > PassAt && (w3.Puck.LastTouch != a3.Id || w3.Puck.Carrier >= 0);
            if (i <= PassAt || b3.Slot != 0 || struck) return;
            steps3++;
            if (Vector2.Distance(b3.Command.Move, stick3) > 1e-3f) moved++;
        });
        Check(steps3 > 5 && moved == steps3, $"a receiver holding shoot for a one-timer is still moved to meet the pass ({moved} of {steps3} steps)");
        Check(events.Any(e => e is ShotTaken shot && shot.ShooterId == b3.Id), "and he one-times it");
    }

    /// <summary>Human passes to receivers all over: standing, skating up ice, across, back; the stick held toward each.</summary>
    [Test]
    public void ReceiversAllOverMeetThePass()
    {
        int passes = 0, taken = 0;
        foreach (var blade in new[] { new Vector2(2, -6), new Vector2(-2, 7), new Vector2(8, 2), new Vector2(4, -10), new Vector2(-14, -8) })
        {
            foreach (var skate in new[] { Vector2.Zero, new Vector2(4, 0), new Vector2(0, 4), new Vector2(0, -4), new Vector2(-3, 0) })
            {
                var w = Make();
                var a = Add(w, Side.Home, new Vector3(-10, 0, 0));
                float heading = skate == Vector2.Zero ? -Right : SimMath.Atan2(skate.X, skate.Y);
                var b = AddBladeAt(w, Side.Home, new Vector3(blade.X, 0, blade.Y), heading);
                b.Velocity = new Vector3(skate.X, 0, skate.Y);
                a.Slot = 0;
                Give(w, a);
                var stick = Vector2.Normalize(blade - Flat(w.Puck.Position));
                int got = -1;
                Run(w, 4f, i => new PlayerCommand { Move = stick, Pass = i == PassAt }, i =>
                {
                    if (got < 0 && w.Puck.Carrier == b.Id) got = i;
                });
                passes++;
                if (got > 0) taken++;
            }
        }
        Check(taken >= passes * 0.9f, $"receivers standing, skating up, across and back all meet a human's pass with the stick still held ({taken} of {passes} taken)");
    }

    [Test]
    public void FreshInputSteers()
    {
        // let go of the stick after the pass, then push it the same way again: that is fresh input
        var (w, _, b, stick) = Pair();
        var push = Vector2.Normalize(new Vector2(0.2f, 1f));
        int afterPush = 0, obeyed = 0, before = 0, metBefore = 0;
        Run(w, 1.2f, i => new PlayerCommand { Move = i < PassAt + 15 ? stick : i < PassAt + 18 ? Vector2.Zero : push, Pass = i == PassAt }, i =>
        {
            if (b.Slot != 0 || w.Puck.Carrier >= 0) return;
            if (i > PassAt && i < PassAt + 15)
            {
                before++;
                if (Vector2.Distance(b.Command.Move, stick) > 1e-3f) metBefore++;
            }
            if (i >= PassAt + 18)
            {
                afterPush++;
                if (Vector2.Distance(b.Command.Move, push) < 1e-4f) obeyed++;
            }
        });
        Check(before > 5 && metBefore == before, $"before the player steers, the stick held from the pass is ignored ({metBefore} of {before} steps)");
        Check(afterPush > 5 && obeyed == afterPush, $"the stick let go and pushed again steers him from then on ({obeyed} of {afterPush} steps)");

        // the stick turned clearly to a new way hands him back at once
        var (w2, _, b2, stick2) = Pair();
        var turned = new Vector2(-stick2.Y, stick2.X);
        int turnedSteps = 0, turnedObeyed = 0;
        Run(w2, 1f, i => new PlayerCommand { Move = i <= PassAt ? stick2 : turned, Pass = i == PassAt }, i =>
        {
            if (i <= PassAt || b2.Slot != 0 || w2.Puck.Carrier >= 0) return;
            turnedSteps++;
            if (Vector2.Distance(b2.Command.Move, turned) < 1e-4f) turnedObeyed++;
        });
        Check(turnedSteps > 5 && turnedObeyed == turnedSteps, $"a stick turned 90° from the held one steers him at once ({turnedObeyed} of {turnedSteps} steps)");
    }

    [Test]
    public void NoConePassFindsOpenTeammate()
    {
        // nobody ahead: a near teammate behind is covered, a further one is open
        var w = Make();
        var a = Add(w, Side.Home, new Vector3(0, 0, 0));
        var covered = AddBladeAt(w, Side.Home, new Vector3(-6, 0, 4), heading: Right);
        var open = AddBladeAt(w, Side.Home, new Vector3(-11, 0, -6), heading: Right);
        var marker = Add(w, Side.Away, PuckHandling.Blade(covered) + new Vector3(-0.8f, 0, 0.6f), heading: Right);
        a.Slot = 0;
        Give(w, a);

        Check(!PuckHandling.PickReceiver(w, a, Vector2.UnitX).HasValue, "nobody is in the 70° cone of the stick");
        Check(PuckHandling.PickOpen(w, a) == open, "the pick skips a nearer covered teammate for the nearest open one");
        var keep = marker.Position;
        var openAt = open.Position;
        marker.Position = new Vector3(20, 0, 10);
        Check(PuckHandling.PickOpen(w, a) == covered, "uncovered, the nearer teammate is the pick");
        marker.Position = new Vector3(-3, 0, 2);
        Check(PuckHandling.PickOpen(w, a) == open, "an opponent in the lane to him makes him covered too");
        marker.Position = keep;
        open.Position = new Vector3(-45, 0, -6);
        Check(!PuckHandling.PickOpen(w, a).HasValue, "with the open teammate past 40 m, nobody is open: the pass goes into space");
        open.Position = openAt;

        int got = -1;
        var events = Run(w, 4f, i => new PlayerCommand { Move = Vector2.UnitX, Pass = i == PassAt }, i =>
        {
            if (got < 0 && w.Puck.Carrier >= 0 && i > PassAt) got = w.Puck.Carrier;
        });
        Check(events.Any(e => e is PassMade pass && pass.PasserId == a.Id), "the human passes with the stick pointing at nobody");
        Check(got == open.Id, $"the no-cone pass reaches the nearest open teammate within 40 m (taken by #{got}, open is #{open.Id})");
        Check(open.Slot == 0, "and control follows it to him");
    }
}
