using StickAndPuck.App.Core;

namespace StickAndPuck.App.Camera
{
    /// <summary>The camera. The gameplay camera pans along the rink after the puck with a lead, at a fixed
    /// height and tilt, in whole pixels, and zooms with speed when <see cref="Settings.CameraZoom"/> is on.
    /// Goals cut to a low angled replay. Runs after every gameplay system, so it sees this step's
    /// positions and events. F6 toggles the zoom while trying it out (a dev key); SAP_CAMERA_ZOOM=0 or 1
    /// sets it at start, for headless checks. It adds the impact feel (hit-stop, shake, rumble, the
    /// fight close-up) and draws the marker under each skater a person controls. The replay and
    /// the fight close-up ask for a coarser render scale, so a kit texel stays about one pixel. It adds
    /// the close-ups (<see cref="CloseUp"/>): the fight, a push-in on the scorer before the replay, the
    /// warm-up and the final horn, each at the scale its distance asks for.</summary>
    internal sealed class CameraSystem : ISystem
    {
        private readonly FollowCamera follow = new();
        private readonly Replay replay = new();
        private readonly Impact impact = new();
        private readonly CloseUp closeUp = new();
        private int scale = 1;

        public bool RunsWhilePaused => true;

        public void Start(World world)
        {
            string zoom = Env.Get("SAP_CAMERA_ZOOM");
            if (
                string.Equals(zoom, "0", StringComparison.Ordinal)
                || string.Equals(zoom, "1", StringComparison.Ordinal)
            )
            {
                world.Settings = world.Settings with
                {
                    CameraZoom = string.Equals(zoom, "1", StringComparison.Ordinal),
                };
            }

            follow.Cut(world);
            world.Camera = follow.Camera;
            scale = 1;
            world.App.Scale.Request(scale);
        }

        public void Update(World world, float dt)
        {
            if (world.Match.Paused)
            {
                return; // the frame holds while a screen is up
            }

            if (Players.Input.KeyInput.Pressed(world.Settings, GameAction.Zoom))
            {
                world.Settings = world.Settings with { CameraZoom = !world.Settings.CameraZoom };
            }

            impact.Update(world, dt);
            if (replay.Update(world, dt))
            {
                follow.Cut(world);
            }

            closeUp.Update(world, dt, replay.Waiting);
            if (world.ReplayPlaying)
            {
                (world.Camera, scale) = PixelGrid.SnapScaled(replay.Camera, scale);
                world.App.Scale.Request(scale);
                return;
            }

            follow.Update(world, dt);
            world.Camera = impact.Apply(closeUp.Apply(follow.Camera), follow.PixelStep);
            if (closeUp.Weight > 0)
            {
                (world.Camera, scale) = PixelGrid.SnapScaled(world.Camera, scale);
            }
            else
            {
                scale = 1;
            }

            world.App.Scale.Request(scale);
        }

        public void DrawScene(World world, RenderContext render)
        {
            if (!world.ReplayPlaying)
            {
                Render.Marker.Draw(world, render);
            }
        }

        public void DrawHud(World world, RenderContext render)
        {
            replay.DrawHud(world);
        }
    }
}
