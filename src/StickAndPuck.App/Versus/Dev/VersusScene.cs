using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Versus.Dev
{
    /// <summary>SAP_SCENE=versus: a player vs player match straight away, keyboard and mouse on the home centre
    /// and the first gamepad on the away centre (SAP_VERSUS_P2 changes the pad, SAP_HOME and SAP_AWAY
    /// the clubs, SAP_PERIOD=seconds the period length, SAP_HUMAN=0 gives both clubs to the AI).</summary>
    public sealed class VersusScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "versus";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            bool ai = Env.Is("SAP_HUMAN", "0");
            MatchSetup setup = Scenes.DefaultSetup(clubs) with
            {
                Inputs = VersusMode.Inputs(
                    ai ? InputDevice.None : InputDevice.KeyboardMouse,
                    app.Screens.Prefs.Player2
                ),
                Overtime = OvertimeRule.Shl,
            };
            if (
                float.TryParse(
                    Env.Get("SAP_PERIOD"),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float s
                )
                && s > 0
            )
            {
                setup = setup with { PeriodSeconds = s };
            }

            World world = new(setup, settings) { App = app };
            Scenes.AddStarters(world);
            VersusMode.AssignSlots(world);
            MatchFlow.Begin(world);
            Console.WriteLine(
                $"SAP: versus: {setup.Home.Id} ({setup.Inputs[0].Device}) vs {setup.Away.Id} ({setup.Inputs[1].Device})"
            );
            return world;
        }
    }
}
