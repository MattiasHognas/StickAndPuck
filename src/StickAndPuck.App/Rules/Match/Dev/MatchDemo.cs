using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match.Dev
{
    /// <summary>The shot loop and camera behind <see cref="MatchDemoScene"/>.</summary>
    internal sealed class MatchDemo
    {
        private readonly Random dice = Seeds.Dice(7);
        private float shotAt = float.NegativeInfinity;
        private float cameraX;
        private Side next = Side.Home;
        private bool earlyDown;

        /// <summary>Gets the seconds after each drop the demo presses shoot for the human centre; below zero the demo fires shots instead.</summary>
        public float PressAfter { get; init; } = -1f;

        /// <summary>Gets how many times per faceoff the demo presses shoot for the human centre before the drop, a faceoff violation each; 0 for none.</summary>
        public int EarlyPresses { get; init; }

        /// <summary>Seconds into a faceoff before each early press, times the violations so far plus one.</summary>
        private const float EarlyAfter = 0.35f;

        /// <summary>Fires the next shot a moment after the puck drops or settles, and pans the camera to the puck.</summary>
        public static void Update(World world)
        {
            if (world.Dev.Match.TryGet(out MatchDemo demo))
            {
                demo.Run(world);
            }
        }

        private void Run(World world)
        {
            Pan(world);
            if (PressAfter >= 0)
            {
                Faceoffs(world);
                return;
            }
            Puck pk = world.Puck;
            if (world.Match.Phase != MatchPhase.Play)
            {
                shotAt = world.Time;
                return;
            }
            float since = world.Time - shotAt;
            if (since < 1.2f || (since < 4f && pk.Carrier < 0 && pk.Velocity.Length() > 1f))
            {
                return;
            }

            if (pk.Carrier >= 0 && world.Players[pk.Carrier].IsGoalie)
            {
                return;
            }

            Shoot(world, next);
            next = next == Side.Home ? Side.Away : Side.Home;
            shotAt = world.Time;
        }

        /// <summary>Presses shoot for slot 0 <see cref="PressAfter"/> seconds after each drop (and <see cref="EarlyPresses"/> times before it), and whistles a new faceoff a while after each draw.</summary>
        private void Faceoffs(World world)
        {
            DrawState draw = MatchFlow.State(world).Draw;
            // shotAt holds the step the drop was first seen, then the step play started
            if (world.Match.Phase != MatchPhase.Play && !draw.Dropped)
            {
                shotAt = float.PositiveInfinity;
            }

            if (draw.Dropped && float.IsPositiveInfinity(shotAt))
            {
                shotAt = world.Time;
            }

            // an early press, released the next step so each one is fresh
            bool early =
                world.Match.Phase == MatchPhase.Faceoff
                && !draw.Dropped
                && !earlyDown
                && draw.Violations[(int)Side.Home] < EarlyPresses
                && draw.Waited >= EarlyAfter * (draw.Violations[(int)Side.Home] + 1);
            earlyDown = early;
            bool press =
                early
                || (
                    world.Match.Phase == MatchPhase.Faceoff
                    && draw.Dropped
                    && world.Time - shotAt >= PressAfter
                );
            if (world.Controlled(0).TryGet(out Player me))
            {
                me.Command = me.Command with { Shoot = press };
            }

            if (world.Match.Phase == MatchPhase.Play && world.Time - shotAt > 2.5f)
            {
                _ = MatchFlow.Whistle(world, RinkSpec.CentreDot);
            }
        }

        private void Shoot(World world, Side shooter)
        {
            int end = -FaceoffFormation.DefendedEnd(world.Match, shooter);
            Vector3 from = new(
                end * (RinkSpec.GoalLineX - 6 - (dice.NextSingle() * 8)),
                0,
                ((dice.NextSingle() * 2) - 1) * 6
            );
            // aimed at the corners of the net, so some beat the goalie
            float aim = (0.55f + (dice.NextSingle() * 0.3f)) * ((dice.Next(2) * 2) - 1);
            Vector3 target = new(end * RinkSpec.GoalLineX, 0, aim);
            Puck pk = world.Puck;
            pk.Carrier = -1;
            pk.LastTouch = world.OnIce(shooter).FirstOrDefault(p => !p.IsGoalie)?.Id ?? -1;
            pk.Position = from;
            pk.Velocity = Vector3.Normalize(target - from) * (28 + (dice.NextSingle() * 10));
        }

        private void Pan(World world)
        {
            float goal = Math.Clamp(world.Puck.Position.X, -17f, 17f);
            cameraX += (goal - cameraX) * 0.06f;
            float x = MathF.Round(cameraX * 4, MidpointRounding.ToEven) / 4;
            Camera3D cam = BroadcastCamera.Straight();
            world.Camera = cam with
            {
                Position = cam.Position + new Vector3(x, 0, 0),
                Target = cam.Target + new Vector3(x, 0, 0),
            };
        }
    }
}
