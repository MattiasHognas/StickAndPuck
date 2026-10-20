using StickAndPuck.App.Core;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>The screen a finished season ends on: the relegation screen when the human's club lost the
    /// playout (W76), else the champion's trophy.</summary>
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
    }
}
