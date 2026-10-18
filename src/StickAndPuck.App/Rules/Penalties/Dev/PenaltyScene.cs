using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Overtime;

namespace StickAndPuck.App.Rules.Penalties.Dev
{
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
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            float period = Env.GetFloat("SAP_PERIOD").TryGet(out float s) && s > 0 ? s : 20 * 60;
            World world = new(Scenes.DefaultSetup(clubs) with { PeriodSeconds = period }, settings);
            Scenes.AddStarters(world);
            if (
                Env.Is("SAP_AI_HUMAN", "1")
                && world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C)
                    is { } centre
            )
            {
                centre.Slot = 0;
            }

            if (Env.GetFloat("SAP_PENALTY_STRICT").TryGet(out float strict) && strict >= 0)
            {
                world.Switches.PenaltyStrictness = strict;
            }

            MatchFlow.Begin(world);
            if (Env.Is("SAP_PENALTY_OT", "1"))
            {
                Lines.LinesSystem.SetUp(world);
                OvertimeRules.ThreeOnThree(world);
                MatchFlow.StartPeriod(
                    world,
                    world.Setup.Periods + 1,
                    world.Switches.OvertimeSeconds
                );
            }
            else
            {
                MatchFlow.SetUpFaceoff(world, RinkSpec.CentreDot);
            }
            PenaltyCalls.Book(world).ScriptAfterDrop = 2f;
            if (Env.Is("SAP_PENALTY_MAJOR", "1"))
            {
                PenaltyCalls.Book(world).ScriptCall = (
                    Infraction.Boarding,
                    Severity.MajorAndGameMisconduct
                );
            }

            if (Env.GetFloat("SAP_PENALTY_SKIP").TryGet(out float skip) && skip > 0)
            {
                PenaltyScenes.FastForward(world, skip);
            }

            return world;
        }
    }
}
