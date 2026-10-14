using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;
using StickAndPuck.App.Rules.Dev;

namespace StickAndPuck.App.Referee.Dev;

/// <summary>SAP_SCENE=referee: the match demo (shots on goal in turn, short periods, the score line) with a
/// short intro and the referee already beside the centre dot, so a screenshot after one second shows
/// it set for the opening drop with both lineups in place. Afterwards it runs like matchdemo.</summary>
public sealed class RefereeScene : IScene
{
    /// <inheritdoc/>
    public string Name => "referee";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
    {
        var world = new MatchDemoScene().Create(clubs, settings, app);
        MatchFlow.State(world).Timer = 0.7f;
        Referee.Of(world).Position = Referee.DropSpot(RinkSpec.CentreDot);
        return world;
    }
}
