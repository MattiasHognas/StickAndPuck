using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Arena.Dev
{
    /// <summary>SAP_SCENE=arena: the empty rink for looking at the arena's league layers (A17): only the
    /// goalies on the ice and the puck on the centre dot, so centre ice shows clear. SAP_HOME picks the
    /// home club (an NHL club for the NHL layers: its crest at centre ice, the NHL line colours, the rafter
    /// banners); the trapezoids follow the trapezoid rule (on with the default SHL rules), SAP_TRAPEZOID=0 or 1
    /// overrides it; SAP_ARENA_END=1 puts the puck in front of the
    /// right-hand net instead, so the camera looks at the end and its trapezoid.</summary>
    public sealed class ArenaScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "arena";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings);
            Scenes.AddStarters(world);
            foreach (Side side in (Side[])[Side.Home, Side.Away])
            {
                foreach (Player p in world.OnIce(side).Where(p => !p.IsGoalie).ToList())
                {
                    p.OnIce = false;
                }
            }

            world.Puck.Position = Env.Is("SAP_ARENA_END", "1")
                ? new Vector3(RinkSpec.GoalLineX - 6f, 0, 0)
                : RinkSpec.CentreDot;
            return world;
        }
    }
}
