using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Injuries;
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

        /// <summary>Gets or sets every player's carry-over form, moved by each game credited.</summary>
        public FormTable Form { get; set; } = new();

        /// <summary>Gets or sets the players injured or suspended for some of their club's next games, counted down by each game credited.</summary>
        public AbsenceTable Absences { get; set; } = new();

        /// <summary>Gets or sets every player's development with age (W132), moved by each game credited after its form.</summary>
        public DevelopmentTable Development { get; set; } = new();

        /// <summary>Gets or sets the keys of the games credited: "g12" for season game 13, "p0.1.2" for playoff round 1 series 2 game 3.</summary>
        public ISet<string> Credited { get; set; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Gets or sets the sheet of the latest game the human played (not simmed), for the coach's post-game reaction; None before one.</summary>
        public Maybe<MatchStatSheet> LastPlayed { get; set; }

        /// <summary>Gets or sets the key of <see cref="LastPlayed"/>'s game (see <see cref="GameKey"/> and <see cref="PlayoffKey"/>).</summary>
        public string LastPlayedKey { get; set; } = "";

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
            Form.Add(sheet);
            Absences.Play(sheet);
            Development.Add(key, sheet, Form);
            if (sheet.Award(key).TryGet(out PlayerOfGameAward award))
            {
                Awards.Add(award);
            }

            return true;
        }

        /// <summary>Credits every played game that has no lines yet from its score, the way the games the
        /// human did not play are settled. Call after a round is simulated, and after a save is read. The
        /// clubs play without their absent players, and the season's rules may injure one on paper
        /// (<see cref="InjuryModel.DrawPaper"/>).</summary>
        public int CatchUp(SeasonState season, IReadOnlyList<ClubData> clubs)
        {
            Dictionary<string, ClubData> byId = clubs.ToDictionary(
                c => c.Id,
                StringComparer.Ordinal
            );
            float injuries = season.Rules.Or(RuleSet.Shl).InjuryScale;
            int count = CatchUpRegularSeason(season, byId, injuries);
            if (season.Playoffs.TryGet(out PlayoffState playoffs))
            {
                count += CatchUpPlayoffs(playoffs, byId, injuries);
            }
            return count;
        }

        /// <summary>A game settled on paper, credited: its lines from the score, played by the clubs without
        /// their absent players, with the injuries drawn on paper.</summary>
        private bool CreditPaper(
            string key,
            ClubData home,
            ClubData away,
            FixtureResult result,
            int seed,
            float injuries
        )
        {
            if (Credited.Contains(key))
            {
                return false;
            }

            MatchStatSheet sheet = SyntheticStats.Sheet(
                Absences.Without(home),
                Absences.Without(away),
                result,
                seed
            );
            InjuryModel.DrawPaper(sheet, injuries, seed);
            return Credit(key, sheet);
        }

        private int CatchUpRegularSeason(
            SeasonState season,
            Dictionary<string, ClubData> byId,
            float injuries
        )
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
                    CreditPaper(
                        GameKey(g.Number),
                        home,
                        away,
                        result,
                        season.GameSeed(g.Number),
                        injuries
                    )
                )
                {
                    count++;
                }
            }
            return count;
        }

        private int CatchUpPlayoffs(
            PlayoffState playoffs,
            Dictionary<string, ClubData> byId,
            float injuries
        )
        {
            int count = 0;
            for (int r = 0; r < playoffs.Rounds.Count; r++)
            {
                for (int s = 0; s < playoffs.Rounds[r].Series.Count; s++)
                {
                    count += CatchUpSeries(
                        playoffs,
                        playoffs.Rounds[r].Series[s],
                        r,
                        s,
                        byId,
                        injuries
                    );
                }
            }

            if (playoffs.Playout.TryGet(out PlayoffSeries playout))
            {
                count += CatchUpSeries(
                    playoffs,
                    playout,
                    PlayoffState.PlayoutRound,
                    0,
                    byId,
                    injuries
                );
            }

            return count;
        }

        private int CatchUpSeries(
            PlayoffState playoffs,
            PlayoffSeries series,
            int r,
            int s,
            Dictionary<string, ClubData> byId,
            float injuries
        )
        {
            int count = 0;
            for (int i = 0; i < series.Games.Count; i++)
            {
                PlayoffGame pg = series.Games[i];
                string key = PlayoffKey(r, s, i);
                if (
                    !Credited.Contains(key)
                    && byId.TryGetValue(pg.Home, out ClubData home)
                    && byId.TryGetValue(pg.Away, out ClubData away)
                    && CreditPaper(key, home, away, pg.Result, playoffs.GameSeed(r, s, i), injuries)
                )
                {
                    count++;
                }
            }

            return count;
        }
    }
}
