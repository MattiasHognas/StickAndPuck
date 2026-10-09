using StickAndPuck.Core;

namespace StickAndPuck.AI;

/// <summary>
/// SAP_SCENE=positioning: a 5 on 5 period between the two clubs, every skater AI, from a centre
/// faceoff with the camera following the puck. The stand-in play (<see cref="StandInPlay"/>)
/// shoots, passes and restarts until W6b and W7a replace it. SAP_AI_TARGETS=1 draws where each
/// skater is heading; SAP_AI_HUMAN=1 puts the keyboard on the home centre.
/// </summary>
public sealed class PositioningScene : IScene
{
    /// <inheritdoc/>
    public string Name => "positioning";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs) with { PeriodSeconds = 20 * 60 }, settings);
        Scenes.AddStarters(world);
        if (string.Equals(Environment.GetEnvironmentVariable("SAP_AI_HUMAN"), "1", StringComparison.Ordinal)
            && world.OnIce(Side.Home).FirstOrDefault(p => !p.IsGoalie) is { } centre)
            centre.Slot = 0;
        StandInPlay.Start(world);
        StandInPlay.FollowCamera = true;
        StandInPlay.ShowTargets = string.Equals(Environment.GetEnvironmentVariable("SAP_AI_TARGETS"), "1", StringComparison.Ordinal);
        return world;
    }
}
