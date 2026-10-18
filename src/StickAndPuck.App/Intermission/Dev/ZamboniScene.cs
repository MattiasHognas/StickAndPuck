using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Match.Dev;

namespace StickAndPuck.App.Intermission.Dev
{
    /// <summary>SAP_SCENE=zamboni: an exhibition with the AI on both sides that opens in the first intermission,
    /// so the Zamboni comes out at once, and plays on into the second period after it. SAP_ZAMBONI_AT=seconds
    /// starts the drive that far in with the skaters already waiting at their bench doors, for headless
    /// screenshots of a chosen moment (SAP_SCREENSHOT takes one a second in).</summary>
    public sealed class ZamboniScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "zamboni";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = MatchScenes.Exhibition(clubs, settings, 60f);
            MatchState m = world.Match;
            m.Phase = MatchPhase.Intermission;
            m.Clock = 0;
            world.Puck.Live = false;
            MatchFlow.State(world).Timer = 4f;
            StartAt(world);
            return world;
        }

        /// <summary>SAP_ZAMBONI_AT=seconds: the next drive starts that far in, with the AI skaters already
        /// waiting at their bench doors; also used by the overtime scene's break before a playoff overtime.</summary>
        internal static void StartAt(World world)
        {
            if (
                float.TryParse(
                    Env.Get("SAP_ZAMBONI_AT"),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float at
                )
                && at > 0
            )
            {
                world.Switches.ZamboniStartAt = at;
                foreach ((Player p, Vector2 spot) in IntermissionSystem.WaitSpots(world))
                {
                    p.Position = new Vector3(spot.X, 0, spot.Y);
                    p.Heading = 0;
                }
            }
        }
    }
}
