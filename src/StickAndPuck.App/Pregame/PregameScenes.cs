using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.Pregame;

/// <summary>
/// SAP_SCENE=warmup: a full exhibition between SAP_HOME and SAP_AWAY that opens with the warm-up,
/// the human on the home centre. Both squads skate round their own ends until space, enter or a
/// pad's A or start is pressed; then the opening faceoff follows and the match runs as usual.
/// SAP_PERIOD=seconds shortens the periods; SAP_WARMUP=seconds starts the game by itself.
/// </summary>
public sealed class WarmupScene : IScene
{
    /// <inheritdoc/>
    public string Name => "warmup";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var setup = Scenes.DefaultSetup(clubs) with { PeriodSeconds = MatchScenesPeriod() };
        var world = new World(setup, settings);
        Scenes.AddStarters(world);
        if (world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is { } centre) centre.Slot = 0;
        MatchFlow.Begin(world);
        Pregame.Enable(world);
        return world;
    }

    static float MatchScenesPeriod()
    {
        return float.TryParse(Environment.GetEnvironmentVariable("SAP_PERIOD"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s) && s > 0
            ? s
            : 20 * 60;
    }
}
