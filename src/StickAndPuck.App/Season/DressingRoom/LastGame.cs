using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>The human's club's last game of the season, played or simmed, as the save has it.</summary>
    /// <param name="Home">The home club's id.</param>
    /// <param name="Away">The away club's id.</param>
    /// <param name="Result">The final score and how it was decided.</param>
    /// <param name="Playoff">True for a playoff game.</param>
    /// <param name="UserClub">The human's club id.</param>
    public sealed record LastGame(
        string Home,
        string Away,
        FixtureResult Result,
        bool Playoff,
        string UserClub
    )
    {
        /// <summary>Gets the game's stats key (<see cref="SeasonStats.GameKey"/> or <see cref="SeasonStats.PlayoffKey"/>), or "" when unknown.</summary>
        public string Key { get; init; } = "";

        /// <summary>Gets the game's sim seed, the one its paper stats are drawn from.</summary>
        public int Seed { get; init; }

        /// <summary>Gets the human's club's goals.</summary>
        public int GoalsFor => AtHome ? Result.HomeGoals : Result.AwayGoals;

        /// <summary>Gets the opponent's goals.</summary>
        public int GoalsAgainst => AtHome ? Result.AwayGoals : Result.HomeGoals;

        /// <summary>Gets a value indicating whether the human's club won.</summary>
        public bool Won => GoalsFor > GoalsAgainst;

        /// <summary>Gets a value indicating whether the human's club was at home.</summary>
        public bool AtHome => string.Equals(Home, UserClub, StringComparison.Ordinal);

        /// <summary>Gets the human's club's last game: the latest playoff game once the playoffs have one, else
        /// the latest played regular-season game; None before the first.</summary>
        public static Maybe<LastGame> Of(SeasonState season)
        {
            string user = season.UserClub;
            if (
                season.Playoffs.TryGet(out PlayoffState playoffs)
                && playoffs.Playout.TryGet(out PlayoffSeries playout)
                && playout.Involves(user)
                && playout.Games.Count > 0
            )
            {
                // the human's club plays the playout, never the playoffs as well
                int i = playout.Games.Count - 1;
                PlayoffGame g = playout.Games[^1];
                return new LastGame(g.Home, g.Away, g.Result, Playoff: true, user)
                {
                    Key = SeasonStats.PlayoffKey(PlayoffState.PlayoutRound, 0, i),
                    Seed = playoffs.GameSeed(PlayoffState.PlayoutRound, 0, i),
                };
            }

            if (season.Playoffs.TryGet(out playoffs))
            {
                for (int r = playoffs.Rounds.Count - 1; r >= 0; r--)
                {
                    if (
                        playoffs
                            .Rounds[r]
                            .Series.FirstOrNone(s => s.Involves(user) && s.Games.Count > 0)
                            .TryGet(out PlayoffSeries series)
                    )
                    {
                        PlayoffGame g = series.Games[^1];
                        int s = playoffs.Rounds[r].Series.IndexOf(series),
                            i = series.Games.Count - 1;
                        return new LastGame(g.Home, g.Away, g.Result, Playoff: true, user)
                        {
                            Key = SeasonStats.PlayoffKey(r, s, i),
                            Seed = playoffs.GameSeed(r, s, i),
                        };
                    }
                }
            }

            foreach (Fixture f in season.PlayedBy(user).OrderByDescending(f => f.Number))
            {
                if (f.Result.TryGet(out FixtureResult result))
                {
                    return new LastGame(f.Home, f.Away, result, Playoff: false, user)
                    {
                        Key = SeasonStats.GameKey(f.Number),
                        Seed = season.GameSeed(f.Number),
                    };
                }
            }
            return Maybe.None<LastGame>();
        }
    }
}
