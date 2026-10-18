using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Puck
{
    public static partial class PuckHandling
    {
        /// <summary>Gets the speed of a wrist shot.</summary>
        public static float WristSpeed(Traits t)
        {
            return 20f + (9f * Traits.Unit(t.ShotPower));
        }

        /// <summary>Gets the speed of a slap shot after a wind-up.</summary>
        public static float SlapSpeed(Traits t, float charge)
        {
            float wound = Math.Clamp((charge - SlapAfter) / (FullCharge - SlapAfter), 0f, 1f);
            return (27f + (15f * Traits.Unit(t.ShotPower))) * (0.75f + (0.25f * wound));
        }

        /// <summary>Gets the kind of shot a hold of shoot makes.</summary>
        public static ShotKind KindOf(float charge)
        {
            return charge >= SlapAfter ? ShotKind.Slap : ShotKind.Wrist;
        }

        /// <summary>The velocity of a shot. Facing the attacked net and within range, the shot goes at the net:
        /// to the side the stick points (up or down the screen), else to the far side, at a height
        /// that varies; shot accuracy sets how far it strays, and slap shots stray more. Otherwise the
        /// shot goes the way the player means, low.</summary>
        /// <param name="world">The match.</param>
        /// <param name="shooter">The shooter.</param>
        /// <param name="from">The puck.</param>
        /// <param name="kind">Wrist or slap.</param>
        /// <param name="speed">Speed over the ice.</param>
        /// <param name="rng">Randomness.</param>
        /// <param name="held">Seconds shoot was held, which sets the height (see <see cref="ShotHeight"/>); below 0 for a shot not fired with the button.</param>
        public static Vector3 ShotVelocity(
            World world,
            Player shooter,
            Vector3 from,
            ShotKind kind,
            float speed,
            Random rng,
            float held = -1f
        )
        {
            int end = AttackEnd(world, shooter.Side);
            Vector2 net = new(end * RinkSpec.GoalLineX, 0);
            Vector2 toNet = net - Flat(from);
            Vector2 intent = Intent(shooter);
            bool atNet =
                end * from.X < RinkSpec.GoalLineX
                && toNet.Length() < LongestShot
                && MathF.Abs(Angle(intent, toNet)) < NetAimCone;
            int accuracy = shooter.Data.Traits.ShotAccuracy;
            if (!atNet)
            {
                float up =
                    (held >= 0)
                        ? ShotHeight.Upward(held, accuracy, rng)
                        : ((kind == ShotKind.Wrist) ? 1f : 0f);
                return new Vector3(intent.X * speed, up, intent.Y * speed);
            }

            float stray = (1f - Traits.Unit(accuracy)) * (kind == ShotKind.Slap ? 1.4f : 1f);
            float side = (RinkSpec.NetWidth / 2) - 0.2f;
            float stickZ = shooter.Command.Move.Y;
            float z =
                (MathF.Abs(stickZ) > 0.35f)
                    ? (MathF.Sign(stickZ) * side)
                    : (
                        (MathF.Abs(from.Z) > 0.3f)
                            ? (-MathF.Sign(from.Z) * side * 0.8f)
                            : (((rng.NextSingle() * 2) - 1) * side)
                    );
            float h;
            if (held >= 0)
            {
                h = ShotHeight.AtNet(held, accuracy, rng);
            }
            else
            {
                h =
                    kind == ShotKind.Slap
                        ? 0.1f + (rng.NextSingle() * 0.4f)
                        : 0.15f + (rng.NextSingle() * 0.6f);
                h = Math.Max(0.02f, h + (Gauss(rng) * 0.3f * stray));
            }

            Vector2 dir = Vector2.Normalize(new Vector2(end * RinkSpec.GoalLineX, z) - Flat(from));
            dir = Rotate(dir, Gauss(rng) * Deg(0.3f + (3.5f * stray)));
            float distance = MathF.Abs(((end * RinkSpec.GoalLineX) - from.X) / dir.X);
            // a shot from right at the goal line gets a floor on its flight time and a cap on its lift, so it cannot rocket straight up
            float t = Math.Max(distance / speed, MinFlight);
            float vy = Math.Min(
                ((h - from.Y) / t) + (0.5f * Physics.PuckTuning.Gravity * t),
                MaxLift
            );
            return new Vector3(dir.X * speed, vy, dir.Y * speed);
        }
    }
}
