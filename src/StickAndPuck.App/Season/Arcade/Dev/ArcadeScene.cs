using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Season.Schedule.Dev;

namespace StickAndPuck.App.Season.Arcade.Dev
{
    /// <summary>SAP_SCENE=arcade: continues the saved arcade run (or starts one) and plays the human's next game.
    /// When the match ends the result is recorded and saved, and the run's panel shows; start the game
    /// again for the next game. A finished run shows its summary. Without the start screen:
    /// SAP_ARCADE=new starts a new run over the save, SAP_ARCADE_RULE=suddendeath or lives (default lives),
    /// SAP_CLUB=id picks the human's club (default SAP_HOME, else the first club), SAP_SEED fixes the
    /// opponent order, SAP_ARCADE_SAVE moves the save file, SAP_PERIOD and SAP_OT shorten periods and
    /// overtime, SAP_HUMAN=0 lets the AI play both sides.</summary>
    public sealed class ArcadeScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "arcade";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            clubs = Leagues.ClubsFromEnvironment(clubs); // SAP_LEAGUE=nhl, or an NHL id in SAP_CLUB or SAP_HOME, plays the NHL
            ArcadeRun run = ArcadeScenes.Open(app, clubs);
            bool human = !Env.Is("SAP_HUMAN", "0");
            if (
                app.Arcade.NextSetup(human ? InputDevice.KeyboardMouse : InputDevice.None)
                is not NextMatch.Play { Setup: var setup }
            )
            {
                Console.WriteLine(
                    $"SAP: the arcade run is over ({(run.Champion ? "champions" : string.Create(CultureInfo.InvariantCulture, $"{run.Stage} club(s) beaten"))}); SAP_ARCADE=new starts another"
                );
                return ArcadeScenes.SummaryWorld(clubs, settings);
            }

            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: arcade stage {run.Stage + 1} of {run.Opponents.Count}, {run.LivesLeft} of {run.StartLives} lives: {setup.Home.Name} vs {setup.Away.Name}"
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
