using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Camera.Dev
{
    /// <summary>SAP_SCENE=camera: both lineups, with the puck shot from the neutral zone into the right-hand
    /// net, so the camera has something fast to follow. With SAP_REPLAY_AT=2 the puck's arrival counts
    /// as a goal and the replay plays.</summary>
    public sealed class CameraScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "camera";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings);
            Scenes.AddStarters(world);
            if (world.OnIce(Side.Home).FirstOrDefault(p => !p.IsGoalie) is { } centre)
            {
                centre.Slot = 0;
            }

            world.Puck.Position = new Vector3(-6f, 0, 3f);
            world.Puck.Velocity = new Vector3(16.4f, 0, -1.5f);
            return world;
        }
    }
}
