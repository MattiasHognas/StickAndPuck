using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Season.Playoffs.Dev;

namespace StickAndPuck.App.Season.Schedule.Dev
{
    /// <summary>SAP_SCENE=season: continues the saved season (or starts one) and plays the human's next game.
    /// When the match ends the result is recorded, the round's other games are settled, the season is
    /// saved and the table shows; start the game again for the next round. Without the start screen:
    /// SAP_SEASON=new starts a new season over the save, SAP_CLUB=id picks the human's club for a new
    /// season (default SAP_HOME, else the first club), SAP_SEED fixes the schedule, SAP_SAVE moves the
    /// save file, SAP_PERIOD and SAP_OT shorten periods and overtime, SAP_HUMAN=0 lets the AI play both sides.
    /// Once the regular season is over it plays the human's next playoff game instead.</summary>
    public sealed class SeasonScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "season";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            SeasonState season = SeasonScenes.Open(app, clubs);
            bool human = !Env.Is("SAP_HUMAN", "0");
            if (
                app.Season.NextSetup(human ? InputDevice.KeyboardMouse : InputDevice.None)
                    is not NextMatch.Play { Setup: var setup }
                || !app.Season.Playing.TryGet(out Fixture game)
            )
            {
                return PlayoffScenes.Play(app, clubs, settings, human);
            }

            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: season game {game.Number + 1} of {season.Games.Count}, round {game.Round + 1} of {season.Rounds}: {setup.Home.Name} vs {setup.Away.Name}"
                )
            );
            if (SeasonScenes.Seconds("SAP_PERIOD").TryGet(out float period))
            {
                setup = setup with { PeriodSeconds = period };
            }

            World world = new(setup, settings);
            if (SeasonScenes.Seconds("SAP_OT").TryGet(out float ot))
            {
                world.Switches.OvertimeSeconds = ot;
            }

            Scenes.AddStarters(world);
            Side side = setup.Inputs[0].Side;
            if (
                human
                && world.OnIce(side).FirstOrDefault(p => p.Data.Position == PlayingPosition.C)
                    is { } centre
            )
            {
                centre.Slot = 0;
            }

            MatchFlow.Begin(world);
            return world;
        }
    }
}
