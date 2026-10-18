using System.Globalization;
using System.Numerics;
using StickAndPuck.App.AI.Decisions;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Players.Puck
{
    /// <summary>
    /// Aimed human passes: after a human's pass, control follows the puck to the receiver, who
    /// skates to meet it by himself, the stick still held from the pass ignored, until the player
    /// steers him with fresh input; and a human's pass with nobody in the 70° cone goes to the nearest
    /// open teammate within 40 metres.
    /// </summary>
    public sealed class AimedPassTests : CheckedTest
    {
        private const float Right = MathF.PI / 2;
        private const int PassAt = 10;

        /// <summary>A begun match in play with nobody on the ice: each test adds its own skaters.</summary>
        private static World Make()
        {
            World w = new WorldBuilder().NoStarters().Build();
            w.Match.Phase = MatchPhase.Play;
            w.Puck.Live = true;
            return w;
        }

        private static Player Add(World w, Side side, Vector3 at, float heading = Right)
        {
            Player p = w.AddPlayer(
                side,
                new PlayerData { Position = PlayingPosition.C, Traits = new Traits() },
                at
            );
            p.Heading = heading;
            return p;
        }

        private static Player AddBladeAt(World w, Side side, Vector3 blade, float heading = Right)
        {
            return Add(w, side, blade - PuckHandling.Blade(Vector3.Zero, heading), heading);
        }

        private static void Give(World w, Player p)
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
        private static List<MatchEvent> Run(
            World w,
            float seconds,
            Func<int, PlayerCommand> input,
            Action<int> seen
        )
        {
            ISystem[] systems =
            [
                new DecisionSystem(Seeds.Dice(1)),
                new SkatingSystem(),
                new PuckHandlingSystem(Seeds.Dice(1)),
                new PuckPhysics(),
            ];
            foreach (ISystem s in systems.Where(s => s is not PuckPhysics))
            {
                s.Start(w);
            }

            List<MatchEvent> events = [];
            int steps = (int)MathF.Round(seconds / Game.Step, MidpointRounding.ToEven);
            for (int i = 0; i < steps; i++)
            {
                w.Events.Clear();
                if (w.Controlled(0).TryGet(out Player human))
                {
                    human.Command = input(i);
                }

                foreach (ISystem s in systems)
                {
                    s.Update(w, Game.Step);
                    if (s is DecisionSystem)
                    {
                        seen(i);
                    }
                }
                events.AddRange(w.Events.Current);
                w.Time += Game.Step;
                w.Tick++;
            }
            return events;
        }

        private static Vector2 Flat(Vector3 v)
        {
            return new(v.X, v.Z);
        }

        /// <summary>The usual pair: a human carrier at the left, a teammate up and across the ice, standing still.</summary>
        private static (World W, Player Passer, Player Receiver, Vector2 Stick) Pair()
        {
            World w = Make();
            Player a = Add(w, Side.Home, new Vector3(-10, 0, 0));
            Player b = AddBladeAt(w, Side.Home, new Vector3(2, 0, -6), heading: -Right);
            a.Slot = 0;
            Give(w, a);
            Vector2 stick = Vector2.Normalize(Flat(PuckHandling.Blade(b)) - Flat(w.Puck.Position));
            return (w, a, b, stick);
        }

        [Test]
        public void ReceiverMeetsThePass()
        {
            // the stick held toward the receiver through the pass and on: it would carry him away from the puck
            (World w, Player a, Player b, Vector2 stick) = Pair();
            int got = -1,
                steered = 0,
                away = 0,
                held = 0;
            _ = Run(
                w,
                4f,
                i => new PlayerCommand { Move = stick, Pass = i == PassAt },
                i =>
                {
                    if (got < 0 && w.Puck.Carrier == b.Id)
                    {
                        got = i;
                    }

                    if (i <= PassAt || got >= 0 || b.Slot != 0 || w.Puck.Carrier >= 0)
                    {
                        return;
                    }

                    steered++;
                    Vector2 toPuck = Flat(w.Puck.Position) - Flat(b.Position);
                    if (Vector2.Dot(b.Command.Move, toPuck) < 0)
                    {
                        away++;
                    }

                    if (Vector2.Distance(b.Command.Move, stick) < 1e-3f)
                    {
                        held++;
                    }
                }
            );
            Check(b.Slot == 0 && a.Slot == -1, "control follows the pass to the receiver");
            Check(
                got > 0 && (got - PassAt) * Game.Step < 2.5f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the receiver of a human pass meets it and has the puck ({(got - PassAt) * Game.Step:0.00} s after the pass)"
                )
            );
            Check(
                steered > 5 && away == 0 && held == 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"while it comes he stays on its line or skates to it, never away along the held stick ({steered} steps, {away} away from it, {held} on the stick)"
                )
            );
        }

        [Test]
        public void MovingReceiverMeetsThePass()
        {
            // the receiver skating away up the ice, the stick still held toward him: led, met and taken
            (World w2, Player a2, Player b2, Vector2 _) = Pair();
            b2.Heading = Right;
            b2.Position = new Vector3(-2, 0, -6) - PuckHandling.Blade(Vector3.Zero, Right);
            b2.Velocity = new Vector3(5f, 0, 0);
            Vector2 stick2 = Vector2.Normalize(new Vector2(2, -6) - Flat(w2.Puck.Position));
            int got2 = -1,
                away2 = 0,
                steps2 = 0;
            _ = Run(
                w2,
                4f,
                i => new PlayerCommand { Move = stick2, Pass = i == PassAt },
                i =>
                {
                    if (got2 < 0 && w2.Puck.Carrier == b2.Id)
                    {
                        got2 = i;
                    }

                    if (i <= PassAt || got2 >= 0 || b2.Slot != 0 || w2.Puck.LastTouch != a2.Id)
                    {
                        return;
                    }

                    steps2++;
                    if (Vector2.Distance(b2.Command.Move, stick2) < 1e-3f)
                    {
                        away2++;
                    }
                }
            );
            Check(
                got2 > 0 && (got2 - PassAt) * Game.Step < 2.5f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a receiver skating away meets a led pass too ({(got2 - PassAt) * Game.Step:0.00} s after it)"
                )
            );
            Check(
                steps2 > 5 && away2 == 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"and is steered by the meet, not the held stick, while it comes ({steps2 - away2} of {steps2} steps)"
                )
            );
        }

        [Test]
        public void OneTimerReceiverIsMoved()
        {
            // held shoot to one-time it: still moved to meet it, and the one-timer goes
            (World w3, Player a3, Player b3, Vector2 stick3) = Pair();
            int moved = 0,
                steps3 = 0;
            bool struck = false;
            List<MatchEvent> events = Run(
                w3,
                4f,
                i => new PlayerCommand
                {
                    Move = stick3,
                    Pass = i == PassAt,
                    Shoot = i > PassAt + 3,
                },
                i =>
                {
                    struck |= i > PassAt && (w3.Puck.LastTouch != a3.Id || w3.Puck.Carrier >= 0);
                    if (i <= PassAt || b3.Slot != 0 || struck)
                    {
                        return;
                    }

                    steps3++;
                    if (Vector2.Distance(b3.Command.Move, stick3) > 1e-3f)
                    {
                        moved++;
                    }
                }
            );
            Check(
                steps3 > 5 && moved == steps3,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a receiver holding shoot for a one-timer is still moved to meet the pass ({moved} of {steps3} steps)"
                )
            );
            Check(
                events.Exists(e => e is ShotTaken shot && shot.ShooterId == b3.Id),
                "and he one-times it"
            );
        }

        /// <summary>Human passes to receivers all over: standing, skating up ice, across, back; the stick held toward each.</summary>
        [Test]
        public void ReceiversAllOverMeetThePass()
        {
            int passes = 0,
                taken = 0;
            foreach (
                Vector2 blade in new[]
                {
                    new Vector2(2, -6),
                    new Vector2(-2, 7),
                    new Vector2(8, 2),
                    new Vector2(4, -10),
                    new Vector2(-14, -8),
                }
            )
            {
                foreach (
                    Vector2 skate in new[]
                    {
                        Vector2.Zero,
                        new Vector2(4, 0),
                        new Vector2(0, 4),
                        new Vector2(0, -4),
                        new Vector2(-3, 0),
                    }
                )
                {
                    World w = Make();
                    Player a = Add(w, Side.Home, new Vector3(-10, 0, 0));
                    float heading =
                        skate == Vector2.Zero ? -Right : SimMath.Atan2(skate.X, skate.Y);
                    Player b = AddBladeAt(w, Side.Home, new Vector3(blade.X, 0, blade.Y), heading);
                    b.Velocity = new Vector3(skate.X, 0, skate.Y);
                    a.Slot = 0;
                    Give(w, a);
                    Vector2 stick = Vector2.Normalize(blade - Flat(w.Puck.Position));
                    int got = -1;
                    _ = Run(
                        w,
                        4f,
                        i => new PlayerCommand { Move = stick, Pass = i == PassAt },
                        i =>
                        {
                            if (got < 0 && w.Puck.Carrier == b.Id)
                            {
                                got = i;
                            }
                        }
                    );
                    passes++;
                    if (got > 0)
                    {
                        taken++;
                    }
                }
            }
            Check(
                taken >= passes * 0.9f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"receivers standing, skating up, across and back all meet a human's pass with the stick still held ({taken} of {passes} taken)"
                )
            );
        }

        [Test]
        public void FreshInputSteers()
        {
            // let go of the stick after the pass, then push it the same way again: that is fresh input
            (World w, Player _, Player b, Vector2 stick) = Pair();
            Vector2 push = Vector2.Normalize(new Vector2(0.2f, 1f));
            int afterPush = 0,
                obeyed = 0,
                before = 0,
                metBefore = 0;
            Vector2 MoveAt(int i)
            {
                return i < PassAt + 15 ? stick
                    : i < PassAt + 18 ? Vector2.Zero
                    : push;
            }
            _ = Run(
                w,
                1.2f,
                i => new PlayerCommand { Move = MoveAt(i), Pass = i == PassAt },
                i =>
                {
                    if (b.Slot != 0 || w.Puck.Carrier >= 0)
                    {
                        return;
                    }

                    if (i is > PassAt and < (PassAt + 15))
                    {
                        before++;
                        if (Vector2.Distance(b.Command.Move, stick) > 1e-3f)
                        {
                            metBefore++;
                        }
                    }
                    if (i >= PassAt + 18)
                    {
                        afterPush++;
                        if (Vector2.Distance(b.Command.Move, push) < 1e-4f)
                        {
                            obeyed++;
                        }
                    }
                }
            );
            Check(
                before > 5 && metBefore == before,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"before the player steers, the stick held from the pass is ignored ({metBefore} of {before} steps)"
                )
            );
            Check(
                afterPush > 5 && obeyed == afterPush,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the stick let go and pushed again steers him from then on ({obeyed} of {afterPush} steps)"
                )
            );

            // the stick turned clearly to a new way hands him back at once
            VerifyFreshInputChanges();
        }

        private void VerifyFreshInputChanges()
        {
            (World w2, Player _, Player b2, Vector2 stick2) = Pair();
            Vector2 turned = new(-stick2.Y, stick2.X);
            int turnedSteps = 0,
                turnedObeyed = 0;
            _ = Run(
                w2,
                1f,
                i => new PlayerCommand { Move = i <= PassAt ? stick2 : turned, Pass = i == PassAt },
                i =>
                {
                    if (i <= PassAt || b2.Slot != 0 || w2.Puck.Carrier >= 0)
                    {
                        return;
                    }

                    turnedSteps++;
                    if (Vector2.Distance(b2.Command.Move, turned) < 1e-4f)
                    {
                        turnedObeyed++;
                    }
                }
            );
            Check(
                turnedSteps > 5 && turnedObeyed == turnedSteps,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a stick turned 90° from the held one steers him at once ({turnedObeyed} of {turnedSteps} steps)"
                )
            );
        }

        [Test]
        public void NoConePassFindsOpenTeammate()
        {
            // nobody ahead: a near teammate behind is covered, a further one is open
            World w = Make();
            Player a = Add(w, Side.Home, new Vector3(0, 0, 0));
            Player covered = AddBladeAt(w, Side.Home, new Vector3(-6, 0, 4), heading: Right);
            Player open = AddBladeAt(w, Side.Home, new Vector3(-11, 0, -6), heading: Right);
            Player marker = Add(
                w,
                Side.Away,
                PuckHandling.Blade(covered) + new Vector3(-0.8f, 0, 0.6f),
                heading: Right
            );
            a.Slot = 0;
            Give(w, a);

            Check(
                !PuckHandling.PickReceiver(w, a, Vector2.UnitX).HasValue,
                "nobody is in the 70° cone of the stick"
            );
            Check(
                PuckHandling.PickOpen(w, a) == open,
                "the pick skips a nearer covered teammate for the nearest open one"
            );
            Vector3 keep = marker.Position;
            Vector3 openAt = open.Position;
            marker.Position = new Vector3(20, 0, 10);
            Check(
                PuckHandling.PickOpen(w, a) == covered,
                "uncovered, the nearer teammate is the pick"
            );
            marker.Position = new Vector3(-3, 0, 2);
            Check(
                PuckHandling.PickOpen(w, a) == open,
                "an opponent in the lane to him makes him covered too"
            );
            marker.Position = keep;
            open.Position = new Vector3(-45, 0, -6);
            Check(
                !PuckHandling.PickOpen(w, a).HasValue,
                "with the open teammate past 40 m, nobody is open: the pass goes into space"
            );
            open.Position = openAt;

            int got = -1;
            List<MatchEvent> events = Run(
                w,
                4f,
                i => new PlayerCommand { Move = Vector2.UnitX, Pass = i == PassAt },
                i =>
                {
                    if (got < 0 && w.Puck.Carrier >= 0 && i > PassAt)
                    {
                        got = w.Puck.Carrier;
                    }
                }
            );
            Check(
                events.Exists(e => e is PassMade pass && pass.PasserId == a.Id),
                "the human passes with the stick pointing at nobody"
            );
            Check(
                got == open.Id,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the no-cone pass reaches the nearest open teammate within 40 m (taken by #{got}, open is #{open.Id})"
                )
            );
            Check(open.Slot == 0, "and control follows it to him");
        }
    }
}
