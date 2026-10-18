using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Penalties;

namespace StickAndPuck.App.Benches.Dev
{
    /// <summary>The script and camera behind <see cref="BoxScene"/>.</summary>
    internal sealed class BoxDemo(string mode)
    {
        /// <summary>When the call comes (game seconds), once the puck is in play.</summary>
        private const float CallAt = 9f;

        /// <summary>The time left on the penalty of the "out" script (s).</summary>
        private const float OutAfter = 4f;

        private static readonly ConditionalWeakTable<World, BoxDemo> Demos = [];

        private bool called;
        private double drawMs,
            drawMax;
        private int frames;

        /// <summary>Gets the box scene's script for a world, if it runs one.</summary>
        public static Maybe<BoxDemo> Of(World world)
        {
            return Demos.TryGetValue(world, out BoxDemo demo) ? demo : Maybe.None<BoxDemo>();
        }

        /// <summary>Runs the box scene's script in a world.</summary>
        public static void Attach(World world, string mode)
        {
            Demos.Add(world, new BoxDemo(mode));
        }

        /// <summary>Frames the boxes, makes the call when it is time and tallies the draw time.</summary>
        /// <param name="world">The scene's world.</param>
        /// <param name="lastDrawMs">The time the last frame's bench and box passes took (ms).</param>
        public void Run(World world, double lastDrawMs)
        {
            world.Camera = BroadcastCamera.Lowered(0, 3.5f, BroadcastCamera.FovY);
            if (lastDrawMs > 0)
            {
                drawMs += lastDrawMs;
                drawMax = Math.Max(drawMax, lastDrawMs);
                frames++;
            }
            if (frames >= 300)
            {
                int boxed = world.Players.Count(p => !p.OnIce && PenaltyCalls.InBox(world, p.Id));
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"SAP: benches and boxes draw {drawMs / frames:0.000} ms/frame avg, {drawMax:0.000} max over {frames} frames, {boxed} in the boxes"
                    )
                );
                (drawMs, drawMax, frames) = (0, 0, 0);
            }
            if (called || world.Match.Phase != MatchPhase.Play || world.Time < CallAt)
            {
                return;
            }

            called = true;
            Console.WriteLine(
                string.Create(CultureInfo.InvariantCulture, $"SAP: box {mode} at tick {world.Tick}")
            );
            List<Player> home = Farthest(world, Side.Home);
            if (string.Equals(mode, "out", StringComparison.Ordinal))
            {
                PenaltyCalls.Give(world, home[0].Id, Infraction.Roughing, Severity.Minor);
                MatchState m = world.Match;
                for (int i = 0; i < m.Penalties.Count; i++)
                {
                    if (m.Penalties[i].PlayerId != home[0].Id)
                    {
                        continue;
                    }

                    m.Penalties[i] = m.Penalties[i] with { SecondsLeft = OutAfter };
                    break;
                }
                return;
            }
            _ = MatchFlow.Whistle(world, MatchFlow.NearestDot(world.Puck.Position));
            PenaltyCalls.Give(world, home[0].Id, Infraction.Roughing, Severity.Minor);
            if (!string.Equals(mode, "two", StringComparison.Ordinal))
            {
                return;
            }

            PenaltyCalls.Give(world, home[1].Id, Infraction.Roughing, Severity.Minor);
            PenaltyCalls.Give(
                world,
                Farthest(world, Side.Away)[0].Id,
                Infraction.Roughing,
                Severity.Minor
            );
        }

        /// <summary>Gets a club's skaters on the ice, farthest from its box door first, for a long skate in.</summary>
        private static List<Player> Farthest(World world, Side side)
        {
            Vector3 door = PenaltyCalls.BoxDoor(side);
            return
            [
                .. world
                    .OnIce(side)
                    .Where(p => !p.IsGoalie)
                    .OrderByDescending(p => Vector3.DistanceSquared(p.Position, door)),
            ];
        }
    }
}
