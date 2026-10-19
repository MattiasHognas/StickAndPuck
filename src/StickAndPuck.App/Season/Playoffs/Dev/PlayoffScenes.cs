using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Schedule.Dev;

namespace StickAndPuck.App.Season.Playoffs.Dev
{
    /// <summary>Shared setup for the playoff scenes; SAP_SCENE=season also ends up here once the regular season is over.</summary>
    internal static class PlayoffScenes
    {
        /// <summary>Gets a value indicating whether a human plays (SAP_HUMAN=0 lets the AI play both sides).</summary>
        public static bool Human => !Env.Is("SAP_HUMAN", "0");

        /// <summary>Plays the human's next playoff game of the season in <see cref="SeasonSession"/>, starting the
        /// playoffs if they have not started; shows the bracket when the human's club has no game left.</summary>
        public static World Play(
            AppState app,
            IReadOnlyList<ClubData> clubs,
            Settings settings,
            bool human
        )
        {
            ApplyOvertimeSettings(app);

            Maybe<PlayoffState> opened = app.Playoffs.Open();
            NextMatch next = app.Playoffs.NextSetup(
                human ? InputDevice.KeyboardMouse : InputDevice.None
            );
            if (!opened.TryGet(out PlayoffState playoffs))
            {
                Console.WriteLine("SAP: the regular season is not over yet");
                return BracketWorld(clubs, settings);
            }
            if (
                next is not NextMatch.Play { Setup: var setup }
                || !app.Playoffs.Playing.TryGet(out PlayoffSeries series)
            )
            {
                Console.WriteLine(
                    "SAP: the human's club has no playoff game left; showing the bracket"
                );
                PlayoffSystem.Log(playoffs);
                return BracketWorld(clubs, settings);
            }

            Console.WriteLine(GetGameAnnouncement(playoffs, series, setup));
            setup = ApplyPeriodSeconds(setup);

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

        private static void ApplyOvertimeSettings(AppState app)
        {
            // SAP_OT shortens the playoff overtime periods too, for watching one; the setup below carries it
            if (SeasonScenes.Seconds("SAP_OT").TryGet(out float overtime))
            {
                app.Playoffs.Rules = app.Playoffs.Rules with
                {
                    Overtime = app.Playoffs.Rules.Overtime with { PeriodSeconds = overtime },
                };
            }
        }

        private static MatchSetup ApplyPeriodSeconds(MatchSetup setup)
        {
            return SeasonScenes.Seconds("SAP_PERIOD").TryGet(out float period)
                ? (setup with { PeriodSeconds = period })
                : setup;
        }

        private static string GetGameAnnouncement(
            PlayoffState playoffs,
            PlayoffSeries series,
            MatchSetup setup
        )
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"SAP: playoff {playoffs.Current.Name} game {series.Games.Count + 1}: {setup.Home.Name} vs {setup.Away.Name}, series {series.High} {series.HighWins}-{series.LowWins} {series.Low}"
            );
        }

        /// <summary>The starting lineups standing still, with the bracket on top.</summary>
        public static World BracketWorld(IReadOnlyList<ClubData> clubs, Settings settings)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings);
            Scenes.AddStarters(world);
            world.Switches.PlayoffBracket = true;
            return world;
        }

        /// <summary>Opens (or starts) the playoff test season and settles whatever is left of its regular season.</summary>
        public static void OpenTestSeason(AppState app, IReadOnlyList<ClubData> clubs)
        {
            string path = Env.Get("SAP_SAVE") is { Length: > 0 }
                ? SeasonSave.PathFor(Leagues.Of(clubs)) // each league its own save
                : Path.Combine(
                    Path.GetDirectoryName(SeasonSave.DefaultPath) ?? "",
                    clubs.Count > 0 && clubs[0].League == League.Nhl
                        ? "playoffs-test-nhl.json"
                        : "playoffs-test.json"
                );
            bool fresh = string.Equals(
                Env.Get("SAP_SEASON"),
                "new",
                StringComparison.OrdinalIgnoreCase
            );
            SeasonState season =
                !fresh && app.Season.Continue(clubs, path).TryGet(out SeasonState saved)
                    ? saved
                    : StartTestSeason(app, clubs, path);
            int settled = SettleSeason(app);
            if (settled > 0)
            {
                SeasonSave.Write(season, path);
            }

            int place =
                season
                    .Table.ToList()
                    .FindIndex(r =>
                        string.Equals(r.ClubId, season.UserClub, StringComparison.Ordinal)
                    ) + 1;
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: playoff test season in {path}: {season.UserClub} finished {place} of {season.Clubs.Count}{(settled > 0 ? $", {settled} of the human's games settled on paper" : "")}"
                )
            );
        }

        /// <summary>Starts the playoff test season at its own save (SAP_CLUB, SAP_SEED as in the season scene).</summary>
        private static SeasonState StartTestSeason(
            AppState app,
            IReadOnlyList<ClubData> clubs,
            string path
        )
        {
            string club = Env.Get("SAP_CLUB") is { Length: > 0 } c
                ? c
                : Scenes.DefaultSetup(clubs).Home.Id;
            Maybe<int> seed = int.TryParse(
                Env.Get("SAP_SEED"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int s
            )
                ? Maybe.Some(s)
                : Maybe.None<int>();
            return app.Season.StartNew(clubs, club, seed, path);
        }

        /// <summary>Settles the human's remaining regular season games on paper, and with them the rest of the season.</summary>
        public static int SettleSeason(AppState app)
        {
            int count = 0;
            while (
                app.Season.NextSetup(InputDevice.None) is NextMatch.Play { Setup: var setup }
                && app.Season.Playing.TryGet(out Fixture game)
                && app.Season.Current.TryGet(out SeasonState season)
            )
            {
                _ = app.Season.Finish(
                    QuickSim.Play(setup.Home, setup.Away, season.GameSeed(game.Number))
                );
                count++;
            }
            app.Season.Unsaved = false;
            return count;
        }
    }
}
