using System.Globalization;
using StickAndPuck.Core;

namespace StickAndPuck.Rules;

/// <summary>
/// SAP_SCENE=penalty: a full AI exhibition that skips the intro. Two seconds after the opening drop,
/// once away has the puck, the home skater nearest it trips someone: the referee's arm goes up, the whistle goes when
/// home touches the puck, he goes to the box and away has a power play. SAP_PERIOD=seconds shortens
/// the periods (the minor shortens with them); SAP_PENALTY_STRICT scales how often the referee calls
/// penalties after that (1 is normal); SAP_AI_HUMAN=1 puts the keyboard on the home centre.
/// </summary>
public sealed class PenaltyScene : IScene
{
    /// <inheritdoc/>
    public string Name => "penalty";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        float period = Env("SAP_PERIOD") is { } s and > 0 ? s : 20 * 60;
        var world = new World(Scenes.DefaultSetup(clubs) with { PeriodSeconds = period }, settings);
        Scenes.AddStarters(world);
        if (string.Equals(Environment.GetEnvironmentVariable("SAP_AI_HUMAN"), "1", StringComparison.Ordinal)
            && world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is { } centre)
            centre.Slot = 0;
        if (Env("SAP_PENALTY_STRICT") is { } strict and >= 0) Penalties.Strictness = strict;
        MatchFlow.Begin(world);
        MatchFlow.SetUpFaceoff(world, RinkSpec.CentreDot);
        Penalties.Book(world).ScriptAfterDrop = 2f;
        return world;
    }

    static float? Env(string name)
    {
        return float.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : null;
    }
}
