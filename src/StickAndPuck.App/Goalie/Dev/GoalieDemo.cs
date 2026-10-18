using System.Globalization;
using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Goalie.Dev
{
    /// <summary>The shot loop and tally behind <see cref="GoalieScene"/>.</summary>
    internal sealed class GoalieDemo
    {
        private readonly Random dice = new(11);
        private float shotAt = float.NegativeInfinity;
        private bool scored;
        private int shots,
            saves,
            rebounds,
            goals;

        /// <summary>Starts the next shot when the last one is done, and counts goals.</summary>
        public static void Update(World world)
        {
            if (world.Dev.Goalie.TryGet(out GoalieDemo demo))
            {
                demo.Run(world);
            }
        }

        private void Run(World world)
        {
            Camera3D cam = BroadcastCamera.Straight();
            world.Camera = cam with
            {
                Position = cam.Position + new Vector3(17, 0, 0),
                Target = cam.Target + new Vector3(17, 0, 0),
            };
            Puck pk = world.Puck;
            if (!scored && PuckMotion.FullyOverGoalLine(pk.Position, 1))
            {
                scored = true;
                goals++;
            }
            if (pk.Carrier >= 0)
            {
                return;
            }

            float since = world.Time - shotAt;
            if (since < 3.5f && (since < 1.4f || pk.Velocity.Length() > 0.8f))
            {
                return;
            }

            Shoot(world, dice);
            shots++;
            scored = false;
            shotAt = world.Time;
        }

        /// <summary>Puts the puck somewhere in the attacking zone and fires it at a random point on the +x net.</summary>
        public static void Shoot(World world, Random dice)
        {
            Vector3 from = new(
                RinkSpec.GoalLineX - 6 - (dice.NextSingle() * 10),
                0,
                ((dice.NextSingle() * 2) - 1) * 7
            );
            Vector3 target = new(
                RinkSpec.GoalLineX,
                0.05f + (dice.NextSingle() * 1.05f),
                ((dice.NextSingle() * 2) - 1) * 0.85f
            );
            Fire(world, from, target, 18 + (dice.NextSingle() * 16));
        }

        /// <summary>Fires the puck from a spot on the ice so it reaches a target point at a given speed, allowing for gravity.</summary>
        public static void Fire(World world, Vector3 from, Vector3 target, float speed)
        {
            Vector3 flat = new(target.X - from.X, 0, target.Z - from.Z);
            float t = flat.Length() / speed;
            float lift =
                target.Y < 0.06f ? 0 : (target.Y + (0.5f * PuckTuning.Gravity * t * t)) / t;
            Puck pk = world.Puck;
            pk.Carrier = -1;
            pk.Live = true;
            pk.Position = from;
            pk.Velocity = (Vector3.Normalize(flat) * speed) + new Vector3(0, lift, 0);
        }

        /// <summary>Counts a save.</summary>
        public static void Saved(World world, bool caught)
        {
            if (!world.Dev.Goalie.TryGet(out GoalieDemo demo))
            {
                return;
            }

            demo.saves++;
            if (!caught)
            {
                demo.rebounds++;
            }
        }

        /// <summary>Draws the tally in the corner.</summary>
        public static void DrawHud(World world)
        {
            if (!world.Dev.Goalie.TryGet(out GoalieDemo demo))
            {
                return;
            }

            string who = world.Players.Find(p => p.IsGoalie) is not { Data.Traits: { } t } g
                ? ""
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{g.Data.LastName.ToUpperInvariant()} REF {t.GoalieReflexes} POS {t.HockeyIq} GLV {t.GoalieGlove} BLK {t.GoalieBlocker} REB {t.GoalieReboundControl} PH {t.GoaliePuckHandling}"
                );
            DrawText(who, 6, 6, 10, new Color(0xef, 0xe0, 0xc2, 255));
            DrawText(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SHOTS {demo.shots}  SAVES {demo.saves}  REBOUNDS {demo.rebounds}  GOALS {demo.goals}"
                ),
                6,
                18,
                10,
                new Color(0xef, 0xe0, 0xc2, 255)
            );
        }
    }
}
