using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Arena.Dev
{
    /// <summary>SAP_SCENE=crowd: both lineups without the away goalie, and the puck shot from the neutral
    /// zone into the empty right-hand net, so the home crowd (W119) rises and cheers for a real goal,
    /// the end behind the net first.</summary>
    public sealed class CrowdScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "crowd";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings);
            Scenes.AddStarters(world);
            if (world.OnIce(Side.Away).FirstOrDefault(p => p.IsGoalie) is { } goalie)
            {
                goalie.OnIce = false; // an empty net, so the shot goes in
            }

            world.Puck.Position = new Vector3(-6f, 0, 3f);
            world.Puck.Velocity = new Vector3(16.4f, 0, -1.5f);
            return world;
        }
    }
}
