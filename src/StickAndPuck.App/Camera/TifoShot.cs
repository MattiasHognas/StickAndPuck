using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Camera
{
    /// <summary>W120: when the home tifo goes up (<see cref="CrowdState.Tifo"/>), the broadcast camera, which
    /// only sees the stands' front rows, tilts up and pans to it for <see cref="HoldSeconds"/>, while the puck
    /// is dead and no flyover, close-up or replay has the frame, then tilts back down. It stays where it is,
    /// turns and widens a little, so the shot is a broadcaster looking up at the stands. Render only: it reads the
    /// crowd and never touches the simulation, so replays stay the same.</summary>
    internal sealed class TifoShot
    {
        /// <summary>How long the camera stays on one raised tifo, in seconds, once it can.</summary>
        public const float HoldSeconds = 4.5f;

        /// <summary>How long the camera takes to tilt up to it and back down, in seconds.</summary>
        public const float InSeconds = 1.2f,
            OutSeconds = 0.9f;

        /// <summary>The cloth needs to be this far up the stand before the camera looks.</summary>
        public const float LiftFrom = 0.9f;

        /// <summary>The camera aims this far above the cloth's middle (metres), so the tifo sits a little below
        /// the frame's middle: under the match-up card and the warm-up line, over the faceoff hint, clear
        /// of the scorebug.</summary>
        private const float AimAbove = 2.3f;

        /// <summary>The camera widens to this field of view (degrees) on the tifo, so the whole cloth fits
        /// between the cards with the stands round it.</summary>
        private const float FovY = 32f;

        private readonly Dictionary<CrowdSection, Vector3> aims = [];
        private int shotFor = -1;
        private float held;
        private Vector3 aim;

        /// <summary>Gets how far the camera has tilted up to the tifo, 0 (not at all) to 1.</summary>
        public float Weight { get; private set; }

        /// <summary>Reads where each section's tifo hangs, from the seats file.</summary>
        public void Load()
        {
            Rectangle first = SupporterArt.Bundled[SupporterArt.Tifo].Frames[0];
            foreach (
                (CrowdSection section, TifoRenderer.Cloth cloth) in TifoRenderer.Cloths(
                    ResourceLoader.ReadText(Assets.CrowdSeats),
                    first.Width / first.Height
                )
            )
            {
                aims[section] = TifoRenderer.Middle(cloth) + new Vector3(0f, AimAbove, 0f);
            }
        }

        /// <summary>Gets a value indicating whether the camera should be on the tifo now.</summary>
        /// <param name="tifo">The home tifo.</param>
        /// <param name="now">The world time.</param>
        /// <param name="free">Whether nothing else has the frame: the puck dead and no flyover, close-up or replay.</param>
        /// <param name="held">How long the camera has already shown this raise, in seconds.</param>
        /// <returns>True to tilt up to it.</returns>
        internal static bool Wants(TifoState tifo, float now, bool free, float held)
        {
            return free && tifo.Up && tifo.Lift(now) >= LiftFrom && held < HoldSeconds;
        }

        /// <summary>Moves the shot one step.</summary>
        /// <param name="world">The world: its crowd, puck, settings and replay.</param>
        /// <param name="dt">The step, in seconds.</param>
        /// <param name="others">Whether a flyover or close-up has the frame.</param>
        public void Update(World world, float dt, bool others)
        {
            TifoState tifo = world.Crowd.Tifo;
            if (tifo.Raises != shotFor)
            {
                shotFor = tifo.Raises; // a new raise (another home goal) is a new shot
                held = 0f;
            }

            bool free =
                world.Settings.Supporters
                && !others
                && !world.Puck.Live
                && !world.ReplayPlaying
                && aims.ContainsKey(tifo.Section);
            bool on = Wants(tifo, world.Time, free, held);
            if (on)
            {
                aim = aims[tifo.Section];
                held += dt;
            }

            Weight = on
                ? Math.Min(1f, Weight + (dt / InSeconds))
                : Math.Max(0f, Weight - (dt / OutSeconds));
        }

        /// <summary>Turns the camera toward the tifo by the shot's weight.</summary>
        /// <param name="camera">The camera under it.</param>
        /// <returns>The camera looking up at the stand.</returns>
        public Camera3D Apply(Camera3D camera)
        {
            if (Weight <= 0f)
            {
                return camera;
            }

            float t = Weight * Weight * (3f - (2f * Weight));
            Vector3 dirUnder = Vector3.Normalize(camera.Target - camera.Position);
            Vector3 dirAim = Vector3.Normalize(aim - camera.Position);
            Vector3 dir = Vector3.Normalize(Vector3.Lerp(dirUnder, dirAim, t));
            float reach = Vector3.Distance(camera.Position, camera.Target);
            camera.Target = camera.Position + (dir * reach);
            camera.FovY = float.Lerp(camera.FovY, FovY, t);
            return camera;
        }
    }
}
