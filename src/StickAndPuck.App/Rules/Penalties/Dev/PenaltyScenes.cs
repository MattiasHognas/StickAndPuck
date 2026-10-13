using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Dev;

/// <summary>SAP_SCENE=penalty: a full AI exhibition that skips the intro. Two seconds after the opening drop,
/// once away has the puck, the home skater nearest it trips someone: the referee's arm goes up, the whistle goes when
/// home touches the puck, he goes to the box and away has a power play. SAP_PERIOD=seconds shortens
/// the periods (the minor shortens with them); SAP_PENALTY_STRICT scales how often the referee calls
/// penalties after that (1 is normal); SAP_AI_HUMAN=1 puts the keyboard on the home centre.
/// SAP_PENALTY_OT=1 starts in 3 on 3 overtime instead, so the call gives 4 on 3;
/// SAP_PENALTY_SKIP=seconds plays that long off screen first (for a headless screenshot).
/// SAP_PENALTY_MAJOR=1 makes the scripted call boarding, a major with a game misconduct: the
/// skater is ejected, his line refills from the last one (SAP_LINES_DEBUG=1 shows the units).</summary>
public sealed class PenaltyScene : IScene
{
    /// <inheritdoc/>
    public string Name => "penalty";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        float period = Env.Float("SAP_PERIOD").TryGet(out float s) && s > 0 ? s : 20 * 60;
        var world = new World(Scenes.DefaultSetup(clubs) with { PeriodSeconds = period }, settings);
        Scenes.AddStarters(world);
        if (Env.Is("SAP_AI_HUMAN", "1")
            && world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is { } centre)
        {
            centre.Slot = 0;
        }

        if (Env.Float("SAP_PENALTY_STRICT").TryGet(out float strict) && strict >= 0) world.Switches.PenaltyStrictness = strict;
        MatchFlow.Begin(world);
        if (Env.Is("SAP_PENALTY_OT", "1"))
        {
            Lines.LinesSystem.SetUp(world);
            OvertimeRules.ThreeOnThree(world);
            MatchFlow.StartPeriod(world, world.Setup.Periods + 1, world.Switches.OvertimeSeconds);
        }
        else
        {
            MatchFlow.SetUpFaceoff(world, RinkSpec.CentreDot);
        }
        Penalties.Book(world).ScriptAfterDrop = 2f;
        if (Env.Is("SAP_PENALTY_MAJOR", "1"))
            Penalties.Book(world).ScriptCall = (Infraction.Boarding, Severity.MajorAndGameMisconduct);
        if (Env.Float("SAP_PENALTY_SKIP").TryGet(out float skip) && skip > 0) PenaltyScenes.FastForward(world, skip);
        return world;
    }
}

/// <summary>Shared helpers for the penalty scenes.</summary>
internal static class PenaltyScenes
{
    /// <summary>Plays the match off screen for a while with every gameplay system, so a headless screenshot shows a later moment.</summary>
    public static void FastForward(World world, float seconds)
    {
        ISystem[] systems =
        [
            new AI.PositioningSystem(), new AI.DecisionSystem(), new Lines.LinesSystem(), new Players.SkatingSystem(), new Players.CheckingSystem(),
            new Players.PuckHandlingSystem(), new Goalie.GoalieSystem(), new Physics.PuckPhysics(), new MatchRules(), new OffsideRule(),
            new IcingRule(), new PenaltyRules(), new Referee.RefereeSystem(),
        ];
        // only the systems whose Start needs no window (the rest load GPU resources there)
        foreach (var s in systems)
            if (s is AI.PositioningSystem or AI.DecisionSystem or Lines.LinesSystem or Players.SkatingSystem or Players.CheckingSystem or Players.PuckHandlingSystem) s.Start(world);
        for (int i = 0; i < seconds / Game.Step; i++)
        {
            world.Events.Clear();
            foreach (var s in systems) s.Update(world, Game.Step);
            world.Time += Game.Step; world.Tick++;
        }
        world.Time = 0;
    }
}
