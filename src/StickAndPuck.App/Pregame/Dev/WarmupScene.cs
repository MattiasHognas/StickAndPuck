using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Pregame.Dev
{
    /// <summary>SAP_SCENE=warmup: a full exhibition between SAP_HOME and SAP_AWAY that opens with the warm-up,
    /// the human on the home centre. Both squads skate round their own ends until space, enter or a
    /// pad's A or start is pressed; then the opening faceoff follows and the match runs as usual.
    /// SAP_PERIOD=seconds shortens the periods; SAP_WARMUP=seconds starts the game by itself.</summary>
    public sealed class WarmupScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "warmup";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            MatchSetup setup = Scenes.DefaultSetup(clubs) with
            {
                PeriodSeconds = MatchScenesPeriod(),
            };
            World world = new(setup, settings);
            Scenes.AddStarters(world);
            if (
                world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is
                { } centre
            )
            {
                centre.Slot = 0;
            }

            MatchFlow.Begin(world);
            PregameFlow.Enable(world);
            return world;
        }

        private static float MatchScenesPeriod()
        {
            return
                float.TryParse(
                    Env.Get("SAP_PERIOD"),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float s
                )
                && s > 0
                ? s
                : 20 * 60;
        }
    }
}
