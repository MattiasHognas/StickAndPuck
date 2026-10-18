using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Penalties.Dev;

namespace StickAndPuck.App.Lines.Dev
{
    /// <summary>SAP_SCENE=goaliepull: an all-AI exhibition that starts late in the third period with the
    /// home club a goal down, so it pulls its goalie for a sixth skater inside the last two minutes.
    /// SAP_PERIOD=seconds sets the period length (default 5:00, the pull window shortens with it);
    /// SAP_AI_HUMAN=1 puts the keyboard on the home centre, where G pulls and returns the goalie
    /// instead of the AI doing it; SAP_PENALTY_SKIP=seconds plays that long off screen first.</summary>
    public sealed class GoaliePullScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "goaliepull";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new(
                Scenes.DefaultSetup(clubs) with
                {
                    PeriodSeconds = LinesScene.Period(300),
                },
                settings
            );
            Scenes.AddStarters(world);
            if (
                Env.Is("SAP_AI_HUMAN", "1")
                && world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C)
                    is { } centre
            )
            {
                centre.Slot = 0;
            }

            MatchFlow.Begin(world);
            world.Match.Score[1] = 1;
            MatchFlow.StartPeriod(world, world.Setup.Periods, GoaliePull.PullSeconds(world) + 8f);
            world.Switches.LinesPanel = true;
            if (
                float.TryParse(
                    Env.Get("SAP_PENALTY_SKIP"),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float skip
                )
                && skip > 0
            )
            {
                PenaltyScenes.FastForward(world, skip);
            }

            return world;
        }
    }
}
