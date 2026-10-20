using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Recap;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>The screens a finished season ends on: the relegation screen when the human's club lost the
    /// playout (W76), else the champion's trophy (or the fired screen, W133); from each of them the season recap
    /// (W136, <see cref="ToRecap"/>) is the last step, with an ending scene before it (<see cref="PlayEnding"/>).</summary>
    internal static class SeasonEnd
    {
        /// <summary>Gets a value indicating whether the human's club went down in the playout.</summary>
        public static bool UserRelegated(AppState app)
        {
            return app.Playoffs.Current.Is(p => p.UserRelegated);
        }

        /// <summary>Gets the season's last screen.</summary>
        public static Screen Screen(AppState app)
        {
            return UserRelegated(app) ? new RelegationScreen(app) : TrophyScreen.Season(app);
        }

        /// <summary>Gets the label of the menu item that opens it: RELEGATION, or the trophy item's own key.</summary>
        public static string Label(AppState app, string trophyKey)
        {
            return Text.Get(UserRelegated(app) ? K.Relegation : trophyKey);
        }

        /// <summary>Gets the menu item that ends the season on its recap (the trophy, relegation and fired screens show it).</summary>
        public static MenuItem RecapItem(AppState app)
        {
            return new MenuItem(Text.Get(K.SeasonRecap), () => ToRecap(app));
        }

        /// <summary>Ends the season: the ending scene for how it ended, then the recap in place of the screen on top.</summary>
        public static void ToRecap(AppState app)
        {
            SeasonEnding ending = app
                .Season.Current.Select(SeasonRecap.EndingOf)
                .Or(SeasonEnding.SeasonOver);
            PlayEnding(app, ending, () => app.Screens.Replace(new SeasonRecapScreen(app)));
        }

        /// <summary>THE ENDING SCENE HOOK: a cinematic ending for how the season ended (champions, runners-up,
        /// semi-final, season over, relegated, fired) plays here, before the recap, and calls <paramref name="then"/>
        /// when it is over or skipped. There is no ending scene yet, so the recap shows at once.</summary>
        public static void PlayEnding(AppState app, SeasonEnding ending, Action then)
        {
            Console.WriteLine(
                $"SAP: screens: season ending {ending} for {app.Season.Current.Select(s => s.UserClub).Or("")}"
            );
            then();
        }
    }
}
