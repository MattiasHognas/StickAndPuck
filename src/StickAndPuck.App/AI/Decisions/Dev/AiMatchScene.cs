using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.AI.Decisions.Dev
{
    /// <summary>SAP_SCENE=aimatch: a full exhibition with every skater on both sides played by the AI, under
    /// the match flow with the gameplay camera. SAP_PERIOD=seconds shortens the periods.
    /// SAP_IQ_HOME and SAP_IQ_AWAY (0 to 100) set every skater's smart passes trait for a club,
    /// to see a smarter club pass better; a line at the top shows each club's completed passes, shots
    /// and checks. SAP_AI_HUMAN=1 puts the keyboard on the home centre, like SAP_SCENE=match.</summary>
    public sealed class AiMatchScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "aimatch";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            float period =
                float.TryParse(
                    Env.Get("SAP_PERIOD"),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float s
                )
                && s > 0
                    ? s
                    : 20 * 60;
            World world = new(Scenes.DefaultSetup(clubs) with { PeriodSeconds = period }, settings);
            Scenes.AddStarters(world);
            DecisionScenes.SetHockeyIq(world, Side.Home, Iq("SAP_IQ_HOME"));
            DecisionScenes.SetHockeyIq(world, Side.Away, Iq("SAP_IQ_AWAY"));
            if (
                Env.Is("SAP_AI_HUMAN", "1")
                && world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C)
                    is { } centre
            )
            {
                centre.Slot = 0;
            }

            MatchFlow.Begin(world);
            world.Switches.DecisionLine = true;
            return world;
        }

        private static Maybe<int> Iq(string name)
        {
            return Env.GetInt(name).TryGet(out int v) ? Math.Clamp(v, 0, 100) : Maybe.None<int>();
        }
    }
}
