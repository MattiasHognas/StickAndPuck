using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Physics.Dev
{
    /// <summary>SAP_SCENE=puck: an empty rink where the puck plays a loop of shots by itself: rims round the
    /// corners, a post, a goal, a shot off the glass and a dump behind the net. For watching the bounces.</summary>
    public sealed class PuckScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "puck";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings);
            // one skater per club on the bench, clear of the puck, so the goal has someone to credit
            PlayerData skater = clubs[0].Roster.First(p => p.Position != PlayingPosition.G);
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                world.AddPlayer(side, skater, Vector3.Zero).OnIce = false;
            }

            world.Dev.Puck.Set(new PuckDemo());
            return world;
        }
    }
}
