using System.Globalization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match.Dev
{
    /// <summary>SAP_SCENE=celebration: the end of a decided game, to watch the celebration. Both clubs are AI,
    /// the game starts in the last seconds of the third period (SAP_CLOCK, default 4) with the home club
    /// a goal up (SAP_WINNER=away puts the away club ahead), so the horn sounds at once, the winners
    /// celebrate, the losers slump, then the final and, with the screens, the summary.</summary>
    public sealed class CelebrationScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "celebration";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            float clock =
                float.TryParse(
                    Env.Get("SAP_CLOCK"),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float c
                )
                && c > 0
                    ? c
                    : 4;
            MatchSetup setup = Scenes.DefaultSetup(clubs) with
            {
                PeriodSeconds = 60,
                Inputs =
                [
                    new InputSlot(InputDevice.None, Side.Home),
                    new InputSlot(InputDevice.None, Side.Away),
                ],
            };
            World world = new(setup, settings);
            Scenes.AddStarters(world);
            MatchFlow.Begin(world);
            bool away = Env.Is("SAP_WINNER", "away");
            world.Match.Period = setup.Periods;
            world.Match.Clock = clock;
            world.Match.Score[0] = away ? 2 : 3;
            world.Match.Score[1] = away ? 3 : 2;
            return world;
        }
    }
}
