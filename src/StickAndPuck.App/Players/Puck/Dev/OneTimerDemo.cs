using System.Numerics;
using System.Runtime.CompilerServices;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Puck.Dev
{
    /// <summary>The script and camera behind <see cref="OneTimerScene"/>, run from the skater renderer.</summary>
    internal sealed class OneTimerDemo
    {
        /// <summary>The step the pass leaves on.</summary>
        private const int PassStep = 60;

        /// <summary>Steps after the pass that the receiver presses shoot.</summary>
        private const int PressAfter = 4;

        /// <summary>Steps each round runs, the pause after the shot included.</summary>
        private const int RoundSteps = 300;

        private static readonly ConditionalWeakTable<World, OneTimerDemo> Demos = [];

        private Maybe<Player> passer,
            receiver,
            cutter;
        private float start;
        private int round = -1;

        /// <summary>Gets a value indicating whether this round's pass is cut off by the away skater.</summary>
        private bool Intercepted => round % 2 == 1;

        /// <summary>Gets the one-timer scene's script for a world, if it runs one.</summary>
        public static Maybe<OneTimerDemo> Of(World world)
        {
            return Demos.TryGetValue(world, out OneTimerDemo demo)
                ? demo
                : Maybe.None<OneTimerDemo>();
        }

        /// <summary>Adds the players and runs the one-timer scene's script in a world.</summary>
        public static void Attach(World world)
        {
            OneTimerDemo demo = new();
            List<PlayerData> home =
            [
                .. world.Setup.Home.Roster.Where(p => p.Position != PlayingPosition.G),
            ];
            List<PlayerData> away =
            [
                .. world.Setup.Away.Roster.Where(p => p.Position != PlayingPosition.G),
            ];
            static PlayerData Pick(List<PlayerData> list, int i)
            {
                return list.Count > 0
                    ? list[i % list.Count]
                    : new PlayerData { Position = PlayingPosition.C };
            }

            demo.passer = world.AddPlayer(Side.Home, Pick(home, 0), Vector3.Zero);
            demo.receiver = world.AddPlayer(Side.Home, Pick(home, 1), Vector3.Zero);
            demo.cutter = world.AddPlayer(Side.Away, Pick(away, 3), Vector3.Zero);
            if (
                world.Setup.Away.Roster.FirstOrDefault(p => p.Position == PlayingPosition.G) is
                { } g
            )
            {
                world.AddPlayer(Side.Away, g, new Vector3(RinkSpec.GoalLineX - 1f, 0, 0)).Heading =
                    -MathF.PI / 2;
            }

            Demos.Add(world, demo);
            demo.Reset(world);
        }

        /// <summary>Writes the next step's commands, frames the play and starts a new round when this one is over.</summary>
        public void Run(World world)
        {
            if (!passer.TryGet(out Player a) || !receiver.TryGet(out Player r))
            {
                return;
            }

            int step = (int)MathF.Round((world.Time - start) / Game.Step, MidpointRounding.ToEven);
            Vector3 toMate = PuckHandling.Blade(r) - world.Puck.Position;
            a.Command =
                step == PassStep
                    ? new PlayerCommand
                    {
                        Move = Vector2.Normalize(new Vector2(toMate.X, toMate.Z)),
                        Pass = true,
                    }
                    : default;
            r.Command = new PlayerCommand
            {
                Shoot = step is >= (PassStep + PressAfter) and < (PassStep + PressAfter + 30),
            };
            world.Camera = BroadcastCamera.Straight((a.Position.X + r.Position.X) / 2f, 31.5f, 17f);
            if (step >= RoundSteps)
            {
                Reset(world);
            }
        }

        private void Reset(World world)
        {
            if (
                !passer.TryGet(out Player a)
                || !receiver.TryGet(out Player r)
                || !cutter.TryGet(out Player c)
            )
            {
                return; // never: Attach adds them first
            }

            round++;
            int end = PuckHandling.AttackEnd(world, Side.Home);
            float face = end > 0 ? MathF.PI / 2 : -MathF.PI / 2;
            Place(a, new Vector3(end * (RinkSpec.GoalLineX - 20f), 0, -4f), face);
            Place(r, new Vector3(end * (RinkSpec.GoalLineX - 9f), 0, 2.5f), face);
            Vector3 mid = (PuckHandling.Blade(a) + PuckHandling.Blade(r)) / 2;
            // in the lane, its blade on the line between the two sticks, or trailing the passer well out of it
            Vector3 at = Intercepted
                ? mid - PuckHandling.Blade(Vector3.Zero, -face)
                : a.Position + new Vector3(-end * 5f, 0, 3f);
            Place(c, at, -face);
            Core.Puck pk = world.Puck;
            pk.Carrier = a.Id;
            pk.Position = PuckHandling.Blade(a);
            pk.Velocity = Vector3.Zero;
            pk.Live = true;
            start = world.Time;
        }

        private static void Place(Player p, Vector3 at, float heading)
        {
            p.Position = at;
            p.Velocity = Vector3.Zero;
            p.Heading = heading;
            p.Command = default;
            p.Begin(PlayerAction.None);
        }
    }
}
