using System.Globalization;
using StickAndPuck.AI;
using StickAndPuck.Core;
using StickAndPuck.Rules;

namespace StickAndPuck.Lines;

/// <summary>
/// SAP_SCENE=lines: a 5 on 5 match with every skater AI (W6a's stand-in play) where skaters tire
/// six times faster than normal (SAP_FATIGUE sets the pace), so lines change on the fly every
/// few seconds. The bottom panel shows each club's unit on the ice, its energy and the goalie.
/// SAP_PERIOD=seconds shortens the periods; SAP_AI_HUMAN=1 puts the keyboard on the home centre
/// (L or gamepad LB changes line early).
/// </summary>
public sealed class LinesScene : IScene
{
    /// <inheritdoc/>
    public string Name => "lines";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs) with { PeriodSeconds = Period(20 * 60) }, settings);
        Scenes.AddStarters(world);
        if (string.Equals(Environment.GetEnvironmentVariable("SAP_AI_HUMAN"), "1", StringComparison.Ordinal)
            && world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is { } centre)
        {
            centre.Slot = 0;
        }

        StandInPlay.Start(world);
        MatchFlow.Begin(world);
        StandInPlay.FollowCamera = true;
        LinesScenes.Demo();
        return world;
    }

    /// <summary>Gets the period length from SAP_PERIOD, or a default.</summary>
    /// <param name="fallback">Seconds when SAP_PERIOD is not set.</param>
    /// <returns>The period in seconds.</returns>
    internal static float Period(float fallback)
    {
        return float.TryParse(Environment.GetEnvironmentVariable("SAP_PERIOD"), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0 ? s : fallback;
    }
}

/// <summary>
/// SAP_SCENE=goalieswap: W7a's match demo (shots at each net in turn) with two-minute periods and
/// the energy panel, so a goalie soon lets in three in a short spell and the backup comes in at
/// the next faceoff.
/// </summary>
public sealed class GoalieSwapScene : IScene
{
    /// <inheritdoc/>
    public string Name => "goalieswap";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = MatchScenes.Exhibition(clubs, settings, LinesScene.Period(120));
        MatchDemo.Enabled = true;
        LinesScenes.Demo();
        return world;
    }
}

/// <summary>
/// SAP_SCENE=goaliepull (W23): an all-AI exhibition that starts late in the third period with the
/// home club a goal down, so it pulls its goalie for a sixth skater inside the last two minutes.
/// SAP_PERIOD=seconds sets the period length (default 5:00, the pull window shortens with it);
/// SAP_AI_HUMAN=1 puts the keyboard on the home centre, where G pulls and returns the goalie
/// instead of the AI doing it; SAP_PENALTY_SKIP=seconds plays that long off screen first.
/// </summary>
public sealed class GoaliePullScene : IScene
{
    /// <inheritdoc/>
    public string Name => "goaliepull";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs) with { PeriodSeconds = LinesScene.Period(300) }, settings);
        Scenes.AddStarters(world);
        if (string.Equals(Environment.GetEnvironmentVariable("SAP_AI_HUMAN"), "1", StringComparison.Ordinal)
            && world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is { } centre)
        {
            centre.Slot = 0;
        }

        MatchFlow.Begin(world);
        world.Match.Score[1] = 1;
        MatchFlow.StartPeriod(world, world.Setup.Periods, GoaliePull.PullSeconds(world) + 8f);
        LinesSystem.ShowPanel = true;
        if (float.TryParse(Environment.GetEnvironmentVariable("SAP_PENALTY_SKIP"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float skip) && skip > 0)
            PenaltyScenes.FastForward(world, skip);
        return world;
    }
}

/// <summary>Shared setup for the lines scenes.</summary>
internal static class LinesScenes
{
    /// <summary>Fast fatigue (unless SAP_FATIGUE says otherwise) and the panel on.</summary>
    public static void Demo()
    {
        if (Environment.GetEnvironmentVariable("SAP_FATIGUE") is not { Length: > 0 }) LinesTuning.Pace = 6f;
        LinesSystem.ShowPanel = true;
    }
}
