using System.Globalization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Schedule.Dev
{
    /// <summary>Shared setup for the season scenes.</summary>
    internal static class SeasonScenes
    {
        /// <summary>Continues the save, or starts a new season when there is none or SAP_SEASON=new.</summary>
        public static SeasonState Open(AppState app, IReadOnlyList<ClubData> clubs)
        {
            bool fresh = string.Equals(
                Env.Get("SAP_SEASON"),
                "new",
                StringComparison.OrdinalIgnoreCase
            );
            if (!fresh && app.Season.Continue(clubs).TryGet(out SeasonState saved))
            {
                Console.WriteLine(
                    $"SAP: season continued from {app.Season.SavePath}, {saved.Games.Count(g => g.Played)} of {saved.Games.Count} games played"
                );
                return saved;
            }

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
            SeasonState season = app.Season.StartNew(clubs, club, seed);
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: new season for {club}, {season.Games.Count} games in {season.Rounds} rounds, saved to {app.Season.SavePath}"
                )
            );
            return season;
        }

        /// <summary>The starting lineups standing still, with the table on top.</summary>
        public static World TableWorld(IReadOnlyList<ClubData> clubs, Settings settings)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings);
            Scenes.AddStarters(world);
            world.Switches.ScheduleTable = true;
            return world;
        }

        /// <summary>Reads a positive number of seconds from an environment variable, or None.</summary>
        public static Maybe<float> Seconds(string variable)
        {
            return
                float.TryParse(
                    Env.Get(variable),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float s
                )
                && s > 0
                ? s
                : Maybe.None<float>();
        }
    }
}
