using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.AI;

/// <summary>
/// SAP_SCENE=positioning: a 5 on 5 match between the two clubs under W7a's match flow, every
/// skater AI, with the camera following the puck. The stand-in play (<see cref="StandInPlay"/>)
/// shoots and passes until W6b replaces it. SAP_PERIOD=seconds shortens the periods. SAP_AI_TARGETS=1 draws where each
/// skater is heading; SAP_AI_HUMAN=1 puts the keyboard on the home centre.
/// </summary>
public sealed class PositioningScene : IScene
{
    /// <inheritdoc/>
    public string Name => "positioning";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        float period = float.TryParse(Env.Get("SAP_PERIOD"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s) && s > 0 ? s : 20 * 60;
        var world = new World(Scenes.DefaultSetup(clubs) with { PeriodSeconds = period }, settings);
        Scenes.AddStarters(world);
        if (Env.Is("SAP_AI_HUMAN", "1")
            && world.OnIce(Side.Home).FirstOrDefault(p => !p.IsGoalie) is { } centre)
        {
            centre.Slot = 0;
        }

        var standIn = StandInPlay.Start(world);
        MatchFlow.Begin(world, log: false);
        standIn.FollowCamera = true;
        standIn.ShowTargets = Env.Is("SAP_AI_TARGETS", "1");
        return world;
    }
}
