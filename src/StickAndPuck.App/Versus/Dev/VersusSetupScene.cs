using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App.Versus.Dev
{
    /// <summary>SAP_SCENE=versussetup: the start screen flow opened on player vs player's team select, so the
    /// two picks, the match and the results run through the screens. SAP_SCREEN_KEYS scripts the
    /// picks for headless runs ("enter down enter" picks the first two clubs).</summary>
    public sealed class VersusSetupScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "versussetup";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = app.Screens.Begin(clubs, new StartScreen(app));
            app.Screens.Push(new ModeScreen(app));
            ModeScreen.Choose(app, new VersusMode());
            Console.WriteLine("SAP: versus: team select for player 1");
            return world;
        }
    }
}
