using StickAndPuck.App.Core;
using StickAndPuck.App.Lines.Dev;
using StickAndPuck.App.Rules.Icing;

namespace StickAndPuck.App.Rules.Match.Dev
{
    /// <summary>SAP_SCENE=timeout: an all-AI exhibition that opens late in the third period with the home club a
    /// goal down and its tired line held on the ice by an icing, so the home AI calls its time-out at the
    /// faceoff: the banner with the club and the clock shows for 30 seconds while the skaters rest, then
    /// play resumes. SAP_PERIOD=seconds sets the period length (default 5:00); SAP_LINES_DEBUG=1 shows the
    /// energy bars filling.</summary>
    public sealed class TimeOutScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "timeout";

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
            MatchFlow.Begin(world);
            world.Match.Score[1] = 1;
            float late = Math.Min(
                TimeOuts.AiLateSeconds,
                world.Setup.PeriodSeconds * TimeOuts.AiLateShare
            );
            MatchFlow.StartPeriod(world, world.Setup.Periods, late - 10f);
            foreach (Player p in world.OnIce(Side.Home).Where(p => !p.IsGoalie))
            {
                p.Energy = 0.35f;
            }

            IcingRule.HoldLines(world, Side.Home);
            return world;
        }
    }
}
