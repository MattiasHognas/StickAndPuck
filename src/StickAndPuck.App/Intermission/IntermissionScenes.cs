using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.Intermission;

/// <summary>
/// SAP_SCENE=zamboni: an exhibition with the AI on both sides that opens in the first intermission,
/// so the Zamboni comes out at once, and plays on into the second period after it. SAP_ZAMBONI_AT=seconds
/// starts the drive that far in with the skaters already waiting at their bench doors, for headless
/// screenshots of a chosen moment (SAP_SCREENSHOT takes one a second in).
/// </summary>
public sealed class ZamboniScene : IScene
{
    /// <inheritdoc/>
    public string Name => "zamboni";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = MatchScenes.Exhibition(clubs, settings, 60f);
        var m = world.Match;
        m.Phase = MatchPhase.Intermission;
        m.Clock = 0;
        world.Puck.Live = false;
        MatchFlow.State(world).Timer = 4f;
        if (float.TryParse(Env.Get("SAP_ZAMBONI_AT"), NumberStyles.Float, CultureInfo.InvariantCulture, out float at) && at > 0)
        {
            IntermissionSystem.StartAt = at;
            foreach (var (p, spot) in IntermissionSystem.WaitSpots(world))
            {
                p.Position = new Vector3(spot.X, 0, spot.Y);
                p.Heading = 0;
            }
        }
        return world;
    }
}
