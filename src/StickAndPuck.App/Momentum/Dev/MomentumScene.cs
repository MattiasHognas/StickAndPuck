using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Momentum.Dev
{
    /// <summary>SAP_SCENE=momentum: an all-AI match that starts with the home club just after a goal, so the
    /// momentum bar under the scorebug and the crowd swell show at once (SAP_SCREENSHOT=1 for a still).
    /// SAP_MOMENTUM_SIDE=away boosts the away club instead.</summary>
    public sealed class MomentumScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "momentum";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            MatchSetup setup = Scenes.DefaultSetup(clubs) with
            {
                Inputs =
                [
                    new InputSlot(InputDevice.None, Side.Home),
                    new InputSlot(InputDevice.None, Side.Away),
                ],
            };
            World world = new(setup, settings);
            Scenes.AddStarters(world);
            MatchFlow.Begin(world);
            MatchFlow.StartPeriod(world, 1, setup.PeriodSeconds);
            Side side = string.Equals(
                Env.Get("SAP_MOMENTUM_SIDE"),
                "away",
                StringComparison.OrdinalIgnoreCase
            )
                ? Side.Away
                : Side.Home;
            MomentumMeter.Boost(world, side, MomentumCause.Goal);
            return world;
        }
    }
}
