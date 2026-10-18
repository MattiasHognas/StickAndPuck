using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Pregame;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Camera
{
    /// <summary>The arena flyover's high sweep (<see cref="ArenaFlyover"/>): the camera starts high over the
    /// far corner of the away end and circles down behind the away net and along the near side to the
    /// home end,
    /// where the close-up camera takes over on the home squad; the close-up then eases out to the
    /// broadcast view. Render only: it reads the warm-up's clock and never touches the simulation.</summary>
    internal sealed class Flyover
    {
        /// <summary>How long the sweep takes to hand over to the camera under it, in seconds (when it ends, or when it is skipped).</summary>
        private const float BlendSeconds = 1.2f,
            SkipSeconds = 0.5f;

        /// <summary>The sweep's path: angle round the rink (0 is the broadcast side, +z), distance from centre ice, height and field of view, at its start and end.</summary>
        private const float FromAngle = 130f,
            ToAngle = -22f,
            FromRadius = 44f,
            ToRadius = 24f,
            FromHeight = 24f,
            ToHeight = 8f,
            FromFov = 46f,
            ToFov = 36f;

        /// <summary>Where the sweep looks: from centre ice to this far into the home end, at about knee height.</summary>
        private const float AimInto = 13f,
            AimHeight = 0.5f;

        /// <summary>Gets how much of the frame is the sweep, 0 (the camera under it) to 1.</summary>
        public float Weight { get; private set; }

        /// <summary>Follows the flyover's phase: the sweep is fully on while it flies and eases out after.</summary>
        public void Update(World world, float dt)
        {
            FlyoverPhase phase = ArenaFlyover.Phase(world);
            if (phase == FlyoverPhase.Aerial)
            {
                Weight = 1;
                return;
            }

            float seconds = phase == FlyoverPhase.Done ? SkipSeconds : BlendSeconds;
            Weight = Math.Max(0, Weight - (dt / seconds));
        }

        /// <summary>Blends the camera under the flyover (the gameplay camera and its close-ups) toward the sweep.</summary>
        public Camera3D Apply(World world, Camera3D under)
        {
            if (Weight <= 0)
            {
                return under;
            }

            Camera3D sweep = At(world, ArenaFlyover.Elapsed(world) / ArenaFlyover.AerialSeconds);
            float t = Weight * Weight * (3 - (2 * Weight));
            Vector3 dirUnder = Vector3.Normalize(under.Target - under.Position);
            Vector3 dirSweep = Vector3.Normalize(sweep.Target - sweep.Position);
            Vector3 pos = Vector3.Lerp(under.Position, sweep.Position, t);
            Vector3 dir = Vector3.Normalize(Vector3.Lerp(dirUnder, dirSweep, t));
            float reach = float.Lerp(
                Vector3.Distance(under.Position, under.Target),
                Vector3.Distance(sweep.Position, sweep.Target),
                t
            );
            under.Position = pos;
            under.Target = pos + (dir * reach);
            under.FovY = float.Lerp(under.FovY, sweep.FovY, t);
            return under;
        }

        /// <summary>The sweep's camera a fraction of the way along its path (clamped to the path's end), mirrored so it ends by the home end.</summary>
        public static Camera3D At(World world, float u)
        {
            float k = Math.Clamp(u, 0, 1);
            float e = k * k * (3 - (2 * k));
            float end = FaceoffFormation.DefendedEnd(world.Match, Side.Home);
            float angle = float.Lerp(FromAngle, ToAngle, e) * MathF.PI / 180f;
            float radius = float.Lerp(FromRadius, ToRadius, e);
            Vector3 pos = new(
                -end * radius * MathF.Sin(angle),
                float.Lerp(FromHeight, ToHeight, e),
                radius * MathF.Cos(angle)
            );
            Vector3 aim = new(end * AimInto * e, AimHeight, 0);
            return new Camera3D
            {
                Position = pos,
                Target = aim,
                Up = Vector3.UnitY,
                FovY = float.Lerp(FromFov, ToFov, e),
                Projection = CameraProjection.Perspective,
            };
        }
    }
}
