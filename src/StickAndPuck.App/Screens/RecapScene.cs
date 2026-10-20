using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Board;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Screens
{
    /// <summary>The season recap's capture (SAP_SCREEN=recap, W136): a test season played out on paper and ended the
    /// way SAP_RECAP picks (champion, the default; relegated; fired), with the human put on the club it happened
    /// to, then the season's end screen and the recap over it, as its SEASON RECAP item opens it.</summary>
    internal static class RecapScene
    {
        /// <summary>Opens the recap on its test season.</summary>
        public static void Open(AppState app, IReadOnlyList<ClubData> clubs)
        {
            string ending = (
                Env.Get("SAP_RECAP") is { Length: > 0 } e ? e : "champion"
            ).ToLowerInvariant();
            switch (ending)
            {
                case "champion" or "champions":
                    PolishScenes.Champions(app, clubs);
                    Become(app, clubs, p => p.Champion);
                    app.Screens.Push(new BracketScreen(app));
                    app.Screens.Push(SeasonEnd.Screen(app));
                    break;
                case "relegated" or "relegation":
                    PolishScenes.Playout(app, clubs, int.MaxValue);
                    Become(app, clubs, p => p.Relegated);
                    app.Screens.Push(new BracketScreen(app));
                    app.Screens.Push(SeasonEnd.Screen(app));
                    break;
                case "fired":
                    PolishScenes.Fired(app, clubs);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"SAP_RECAP={ending}: use champion, relegated or fired"
                    );
            }

            SeasonEnd.ToRecap(app);
        }

        /// <summary>Puts the human on the club the playoffs name (the champion, the relegated club), with the board's goal for that club.</summary>
        private static void Become(
            AppState app,
            IReadOnlyList<ClubData> clubs,
            Func<PlayoffState, Maybe<string>> pick
        )
        {
            if (
                !app.Season.Current.TryGet(out SeasonState season)
                || !season.Playoffs.TryGet(out PlayoffState playoffs)
                || !pick(playoffs).TryGet(out string club)
            )
            {
                return;
            }

            season.UserClub = club;
            playoffs.UserClub = club;
            season.Goal = BoardExpectation.Pick(
                [.. clubs.Where(c => season.Clubs.Contains(c.Id, StringComparer.Ordinal))],
                club
            );
            Console.WriteLine($"SAP: screens: recap test, the human on {club}");
        }
    }
}
