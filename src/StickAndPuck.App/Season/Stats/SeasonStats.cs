using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Season.Stats
{
    /// <summary>The season's player stats and records, saved inside season.json with the rest of the
    /// season. The regular season and the playoffs each have a <see cref="StatTable"/>; every game
    /// is credited once, by key, whether the human played it (from the event log, by
    /// <see cref="StatsSystem"/>) or it was settled on paper (from the score, by <see cref="CatchUp"/>),
    /// so a save from before this existed fills in when it is next opened. The leaders page reads
    /// <see cref="Regular"/>, <see cref="Playoffs"/> and <see cref="Awards"/>.</summary>
    public sealed class SeasonStats
    {
        /// <summary>Gets or sets the regular season's table.</summary>
        public StatTable Regular { get; set; } = new();

        /// <summary>Gets or sets the playoffs' table.</summary>
        public StatTable Playoffs { get; set; } = new();

        /// <summary>Gets or sets every player of the game so far, oldest first.</summary>
        public IList<PlayerOfGameAward> Awards { get; set; } = [];

        /// <summary>Gets or sets the keys of the games credited: "g12" for season game 13, "p0.1.2" for playoff round 1 series 2 game 3.</summary>
        public ISet<string> Credited { get; set; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The key of a regular-season game.</summary>
        public static string GameKey(int number)
        {
            return string.Create(CultureInfo.InvariantCulture, $"g{number}");
        }

        /// <summary>The key of a playoff game.</summary>
        public static string PlayoffKey(int round, int series, int game)
        {
            return string.Create(CultureInfo.InvariantCulture, $"p{round}.{series}.{game}");
        }

        /// <summary>Gets the latest award, or None before any game.</summary>
        public Maybe<PlayerOfGameAward> LastAward =>
            Awards.Count == 0 ? Maybe.None<PlayerOfGameAward>() : Awards[^1];

        /// <summary>Gets the awards a club's players won.</summary>
        public IEnumerable<PlayerOfGameAward> AwardsOf(string club)
        {
            return Awards.Where(a => string.Equals(a.Club, club, StringComparison.Ordinal));
        }

        /// <summary>Credits one game's sheet, unless that key was credited before.</summary>
        public bool Credit(string key, MatchStatSheet sheet)
        {
            if (!Credited.Add(key))
            {
                return false;
            }

            (key.StartsWith('p', StringComparison.Ordinal) ? Playoffs : Regular).Add(sheet);
            if (sheet.Award(key).TryGet(out PlayerOfGameAward award))
            {
                Awards.Add(award);
            }

            return true;
        }

        /// <summary>Credits every played game that has no lines yet from its score, the way the games the
        /// human did not play are settled. Call after a round is simulated, and after a save is read.</summary>
        public int CatchUp(SeasonState season, IReadOnlyList<ClubData> clubs)
        {
            Dictionary<string, ClubData> byId = clubs.ToDictionary(
                c => c.Id,
                StringComparer.Ordinal
            );
            int count = CatchUpRegularSeason(season, byId);
            if (season.Playoffs.TryGet(out PlayoffState playoffs))
            {
                count += CatchUpPlayoffs(playoffs, byId);
            }
            return count;
        }

        private int CatchUpRegularSeason(SeasonState season, Dictionary<string, ClubData> byId)
        {
            int count = 0;
            foreach (Fixture g in season.Games)
            {
                if (
                    !g.Result.TryGet(out FixtureResult result)
                    || Credited.Contains(GameKey(g.Number))
                )
                {
                    continue;
                }

                if (
                    !byId.TryGetValue(g.Home, out ClubData home)
                    || !byId.TryGetValue(g.Away, out ClubData away)
                )
                {
                    continue;
                }

                if (
                    Credit(
                        GameKey(g.Number),
                        SyntheticStats.Sheet(home, away, result, season.GameSeed(g.Number))
                    )
                )
                {
                    count++;
                }
            }
            return count;
        }

        private int CatchUpPlayoffs(PlayoffState playoffs, Dictionary<string, ClubData> byId)
        {
            int count = 0;
            for (int r = 0; r < playoffs.Rounds.Count; r++)
            {
                for (int s = 0; s < playoffs.Rounds[r].Series.Count; s++)
                {
                    PlayoffSeries series = playoffs.Rounds[r].Series[s];
                    int i = 0;
                    foreach (PlayoffGame pg in series.Games)
                    {
                        string key = PlayoffKey(r, s, i);
                        if (Credited.Contains(key))
                        {
                            i++;
                            continue;
                        }
                        if (
                            !byId.TryGetValue(pg.Home, out ClubData home)
                            || !byId.TryGetValue(pg.Away, out ClubData away)
                        )
                        {
                            i++;
                            continue;
                        }
                        if (
                            Credit(
                                key,
                                SyntheticStats.Sheet(
                                    home,
                                    away,
                                    pg.Result,
                                    playoffs.GameSeed(r, s, i)
                                )
                            )
                        )
                        {
                            count++;
                        }

                        i++;
                    }
                }
            }
            return count;
        }
    }
}
