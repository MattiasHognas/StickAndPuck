using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Playoffs.Dev;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season
{
    /// <summary>
    /// The NHL season (W116): 82 games a club by division and conference, the standings by division,
    /// the 16-club playoff field (top three of every division and two wild cards a conference), the
    /// fixed best of seven bracket to a champion, and the save keeping all of it. The SHL season stays
    /// 52 games of 14 clubs.
    /// </summary>
    public sealed class NhlSeasonTests : CheckedTest
    {
        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-nhltest-{Environment.ProcessId}-{Guid.NewGuid():N}"
            )
        );

        private static readonly Lazy<IReadOnlyList<ClubData>> Nhl = new(Clubs.LoadNhl);

        [After(Test)]
        public void EndSessions()
        {
            app.Season.End();
            app.Playoffs.End();
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }

        private static string Conference(string id)
        {
            return Nhl.Value.First(c => Same(c.Id, id)).Conference.Or("");
        }

        private static string Division(string id)
        {
            return Nhl.Value.First(c => Same(c.Id, id)).Division.Or("");
        }

        [Test]
        public void ScheduleHas82GamesByDivisionAndConference()
        {
            IReadOnlyList<ClubData> clubs = Nhl.Value;
            SeasonState season = SeasonState.New(clubs, clubs[0].Id, 116);
            Check(season.League == League.Nhl, "an NHL club list starts an NHL season");
            Check(
                season.Rules.Is(r => r.Preset == RulePreset.Nhl),
                "the NHL season plays NHL rules"
            );
            Check(season.Games.Count == 32 * 82 / 2, $"1312 games ({season.Games.Count})");

            foreach (string id in clubs.Select(c => c.Id))
            {
                List<Fixture> games = [.. season.Games.Where(g => g.Involves(id))];
                int home = games.Count(g => Same(g.Home, id));
                Dictionary<string, int> met = games
                    .GroupBy(g => Same(g.Home, id) ? g.Away : g.Home, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
                int division = met.Where(m => Same(Division(m.Key), Division(id)))
                    .Sum(m => m.Value);
                int conference = met.Where(m =>
                        Same(Conference(m.Key), Conference(id))
                        && !Same(Division(m.Key), Division(id))
                    )
                    .Sum(m => m.Value);
                int other = met.Where(m => !Same(Conference(m.Key), Conference(id)))
                    .Sum(m => m.Value);
                int threeGameRivals = met.Count(m =>
                    Same(Division(m.Key), Division(id)) && m.Value == 3
                );
                bool pairsRight = met.All(m =>
                    Same(Division(m.Key), Division(id)) ? m.Value is 3 or 4
                    : Same(Conference(m.Key), Conference(id)) ? m.Value == 3
                    : m.Value == 2
                );
                Check(
                    games.Count == 82
                        && home == 41
                        && met.Count == 31
                        && division == 26
                        && conference == 24
                        && other == 32
                        && threeGameRivals == 2
                        && pairsRight,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{id}: {games.Count} games, {home} at home, {division} in the division (two rivals three times: {threeGameRivals}), {conference} in the rest of the conference, {other} against the other conference, every club met"
                    )
                );
            }

            bool oncePerRound = season
                .Games.GroupBy(g => g.Round)
                .All(r =>
                    r.SelectMany(g => new[] { g.Home, g.Away })
                        .GroupBy(id => id, StringComparer.Ordinal)
                        .All(c => c.Take(2).Count() == 1)
                );
            Check(oncePerRound, "no club plays twice in a round");
            Check(
                season.Rounds is >= 82 and <= 95,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the season packs into about 82 rounds ({season.Rounds})"
                )
            );
            Check(
                season.Games.Select((g, i) => g.Number == i).All(ok => ok),
                "the games are numbered in order"
            );
            SeasonState again = SeasonState.New(clubs, clubs[0].Id, 116);
            Check(again.Games.SequenceEqual(season.Games), "the same seed gives the same schedule");
        }

        [Test]
        public void ShlSeasonStaysTheSame()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            SeasonState season = SeasonState.New(clubs, clubs[0].Id, 116);
            Check(
                season.League == League.Shl && season.Clubs.Count == 14,
                "the SHL clubs start an SHL season of 14 clubs"
            );
            Check(
                clubs.All(c => season.Games.Where(g => g.Involves(c.Id)).Take(53).Count() == 52),
                "every SHL club plays 52 games"
            );
            Check(
                season.Divisions.Count == 0 && !season.Rules.HasValue,
                "an SHL season has no divisions and takes its rules from the player's choice"
            );
            Check(
                season.Games.SequenceEqual(SeasonSchedule.Build([.. clubs.Select(c => c.Id)], 116)),
                "the SHL schedule is the double round robin twice over"
            );
            Check(
                DivisionStandings.Of(season, season.Table).Count == 0,
                "the SHL table has no division groups"
            );
        }

        [Test]
        public void StandingsGroupByDivision()
        {
            IReadOnlyList<ClubData> clubs = Nhl.Value;
            SeasonState season = SeasonState.New(clubs, clubs[0].Id, 7);
            _ = season.SimulateUpTo(30, clubs);
            IReadOnlyList<StandingRow> table = season.Table;
            IReadOnlyList<DivisionTable> divisions = DivisionStandings.Of(season, table);
            Check(
                divisions
                    .Select(d => $"{d.Conference}/{d.Division}")
                    .SequenceEqual(
                        [
                            "Eastern/Atlantic",
                            "Eastern/Metropolitan",
                            "Western/Central",
                            "Western/Pacific",
                        ],
                        StringComparer.Ordinal
                    ),
                "four divisions, by conference"
            );
            Check(divisions.All(d => d.Rows.Count == 8), "eight clubs a division");
            List<string> order = [.. table.Select(r => r.ClubId)];
            Check(
                divisions.All(d =>
                    d.Rows.Select(r => order.IndexOf(r.ClubId))
                        .Zip(d.Rows.Skip(1).Select(r => order.IndexOf(r.ClubId)))
                        .All(p => p.First < p.Second)
                ),
                "each division keeps the table's order"
            );
        }

        [Test]
        public void PlayoffFieldPicksTheRightSixteen()
        {
            IReadOnlyList<ClubData> clubs = Nhl.Value;
            SeasonState season = FinishedSeason(clubs, 2026);
            IReadOnlyList<StandingRow> table = season.Table;
            List<string> order = [.. table.Select(r => r.ClubId)];
            PlayoffState p = PlayoffState.New(season);
            PlayoffRound first = p.Rounds.Single();
            List<string> field = [.. first.Series.SelectMany(s => new[] { s.High, s.Low })];
            Check(
                p.League == League.Nhl
                    && first is { BestOf: 7, Series.Count: 8 }
                    && Same(first.Name, "FIRST ROUND"),
                "the first round is eight best of seven series"
            );
            Check(
                field.Distinct(StringComparer.Ordinal).Take(17).Count() == 16,
                "sixteen different clubs"
            );

            foreach (DivisionTable d in DivisionStandings.Of(season, table))
            {
                Check(
                    d.Rows.Take(3).All(r => field.Contains(r.ClubId)),
                    $"the top three of the {d.Division} qualify"
                );
                Check(
                    Same(p.Tags[d.Rows[0].ClubId], d.Division[..1] + "1")
                        && Same(p.Tags[d.Rows[2].ClubId], d.Division[..1] + "3"),
                    $"the {d.Division} clubs are tagged by place"
                );
            }

            foreach (string conference in new[] { "Eastern", "Western" })
            {
                List<string> rest =
                [
                    .. DivisionStandings
                        .Of(season, table)
                        .Where(d => Same(d.Conference, conference))
                        .SelectMany(d => d.Rows.Skip(3))
                        .OrderBy(r => order.IndexOf(r.ClubId))
                        .Select(r => r.ClubId),
                ];
                Check(
                    field.Contains(rest[0])
                        && field.Contains(rest[1])
                        && rest.Skip(2).All(id => !field.Contains(id)),
                    $"the {conference} wild cards are the two best of the rest ({rest[0]}, {rest[1]})"
                );
                Check(
                    Same(p.Tags[rest[0]], "WC1") && Same(p.Tags[rest[1]], "WC2"),
                    $"the {conference} wild cards are WC1 and WC2"
                );
                Check(
                    field.FindAll(id => Same(Conference(id), conference)).Count == 8,
                    $"eight {conference} clubs"
                );
            }

            VerifyBracketOrder(p, first);
            Check(
                first.Series.All(s =>
                    s.HighSeed < s.LowSeed
                    && Same(s.High, order[s.HighSeed - 1])
                    && Same(s.NextHome, s.High)
                ),
                "the better record is the higher seed and opens at home"
            );
            Check(
                p.UserQualified == field.Contains(season.UserClub),
                "the human qualifies when the club is in the field"
            );
        }

        private void VerifyBracketOrder(PlayoffState p, PlayoffRound first)
        {
            for (int c = 0; c < 2; c++)
            {
                List<PlayoffSeries> half = [.. first.Series.Skip(c * 4).Take(4)];
                string conference = p.Conferences[c];
                Check(
                    half.TrueForAll(s =>
                        Same(Conference(s.High), conference) && Same(Conference(s.Low), conference)
                    ),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"series {(c * 4) + 1} to {(c * 4) + 4} are the {conference} conference"
                    )
                );
                string Tag(PlayoffSeries s, bool winner)
                {
                    // the division side of a division winner v wild card series, or the 2 v 3 series
                    string a = p.Tags[s.High],
                        b = p.Tags[s.Low];
                    bool aWild = a.StartsWith("WC", StringComparison.Ordinal);
                    return winner ? (aWild ? b : a) : (aWild ? a : b);
                }

                string topWinner = Tag(half[0], winner: true),
                    otherWinner = Tag(half[2], winner: true);
                Check(
                    topWinner[^1] == '1' && otherWinner[^1] == '1' && !Same(topWinner, otherWinner),
                    $"{conference}: each half opens with a division winner"
                );
                Check(
                    Same(Tag(half[0], winner: false), "WC2")
                        && Same(Tag(half[2], winner: false), "WC1"),
                    $"{conference}: the best division winner meets WC2, the other WC1"
                );
                Check(
                    p.SeedOf(DivisionWinner(half[0], p)) < p.SeedOf(DivisionWinner(half[2], p)),
                    $"{conference}: the better division winner is first"
                );
                Check(
                    Tags(p, half[1]).SetEquals([topWinner[..1] + "2", topWinner[..1] + "3"])
                        && Tags(p, half[3])
                            .SetEquals([otherWinner[..1] + "2", otherWinner[..1] + "3"]),
                    $"{conference}: each division's second and third meet"
                );
            }
        }

        private static string DivisionWinner(PlayoffSeries s, PlayoffState p)
        {
            return p.Tags[s.High].StartsWith("WC", StringComparison.Ordinal) ? s.Low : s.High;
        }

        private static HashSet<string> Tags(PlayoffState p, PlayoffSeries s)
        {
            return new HashSet<string>([p.Tags[s.High], p.Tags[s.Low]], StringComparer.Ordinal);
        }

        [Test]
        public void WildCardsCanComeFromOneDivision()
        {
            IReadOnlyList<ClubData> clubs = Nhl.Value;
            SeasonState season = SeasonState.New(clubs, clubs[0].Id, 1);
            // a made-up table: the Atlantic's 4th and 5th beat everyone outside the Metropolitan's top three
            List<string> atlantic = Ids(season, "Atlantic"),
                metro = Ids(season, "Metropolitan"),
                west = [.. Ids(season, "Central"), .. Ids(season, "Pacific")];
            List<string> order =
            [
                .. atlantic.Take(5),
                .. metro.Take(3),
                .. metro.Skip(3),
                .. atlantic.Skip(5),
                .. west,
            ];
            List<StandingRow> table =
            [
                .. order.Select((id, i) => new StandingRow(id, 82, 40, 0, 0, 42, 0, 0, 200 - i)),
            ];
            NhlField f = NhlSeeding.Field(season, table);
            Check(
                Same(f.Tags[atlantic[3]], "WC1") && Same(f.Tags[atlantic[4]], "WC2"),
                "both Eastern wild cards come from the Atlantic"
            );
            Check(
                metro.Skip(3).All(id => !f.Tags.ContainsKey(id)),
                "the Metropolitan's fourth misses out"
            );
            Check(
                f.Pairs[0] == (atlantic[0], atlantic[4]) && f.Pairs[2] == (metro[0], atlantic[3]),
                "A1 meets WC2 and M1 meets WC1"
            );
            Check(f.Pairs[1] == (atlantic[1], atlantic[2]), "A2 meets A3");
            Check(
                f.Tags.Count == 16
                    && f.Conferences.SequenceEqual(["Eastern", "Western"], StringComparer.Ordinal),
                "sixteen tags, two conferences"
            );
        }

        private static List<string> Ids(SeasonState season, string division)
        {
            return
            [
                .. season
                    .Clubs.Where(id => Same(season.Divisions[id], division))
                    .Order(StringComparer.Ordinal),
            ];
        }

        [Test]
        public void BracketPlaysToAChampion()
        {
            IReadOnlyList<ClubData> clubs = Nhl.Value;
            SeasonState season = FinishedSeason(clubs, 99);
            PlayoffState p = PlayoffState.New(season);
            p.UserClub = "nobody";
            _ = p.SimulateUntilUser(clubs, PlayoffRules.Shl);
            Check(p.IsOver && p.Rounds.Count == 4, "four rounds to a champion");
            Check(
                p.Rounds.Select(r => r.Series.Count).SequenceEqual([8, 4, 2, 1])
                    && p.Rounds.All(r => r.BestOf == 7 && r.Series.All(s => s.IsOver)),
                "8, 4, 2 and 1 best of seven series"
            );
            Check(
                p.Rounds.Select(r => r.Name)
                    .SequenceEqual(
                        ["FIRST ROUND", "SECOND ROUND", "CONFERENCE FINALS", "FINAL"],
                        StringComparer.Ordinal
                    ),
                "first round, second round, conference finals, final"
            );
            bool fixedBracket = true;
            for (int r = 1; r < p.Rounds.Count; r++)
            {
                for (int s = 0; s < p.Rounds[r].Series.Count; s++)
                {
                    HashSet<string> from = new(
                        [
                            Must(p.Rounds[r - 1].Series[2 * s].Winner),
                            Must(p.Rounds[r - 1].Series[(2 * s) + 1].Winner),
                        ],
                        StringComparer.Ordinal
                    );
                    PlayoffSeries next = p.Rounds[r].Series[s];
                    fixedBracket &=
                        from.SetEquals([next.High, next.Low]) && next.HighSeed < next.LowSeed;
                }
            }
            Check(
                fixedBracket,
                "neighbouring winners meet, the better record at home: no reseeding"
            );
            Check(
                p.Rounds[1].Series.All(s => Same(Conference(s.High), Conference(s.Low))),
                "the second round stays in the conference (a wild card plays in its slot's division bracket)"
            );
            Check(
                p.Rounds[2].Series.All(s => Same(Conference(s.High), Conference(s.Low))),
                "the conference finals stay in the conference"
            );
            PlayoffSeries final = p.Rounds[3].Series[0];
            Check(
                !Same(Conference(final.High), Conference(final.Low)),
                "the final is East against West"
            );
            Check(
                Same(Must(p.Champion), Must(final.Winner)) && !p.UserQualified && !p.UserAlive,
                "the final's winner is champion"
            );
        }

        [Test]
        public void SaveKeepsTheLeague()
        {
            IReadOnlyList<ClubData> clubs = Nhl.Value;
            string path = Path.Combine(folder, "season.json");
            SeasonState season = FinishedSeason(clubs, 5, path);
            _ = app.Playoffs.Open();
            SeasonSave.Write(season, path);
            app.Season.End();

            SeasonState read = Must(app.Season.Continue(clubs, path));
            PlayoffState playoffs = Must(read.Playoffs);
            Check(
                read.League == League.Nhl
                    && read.Divisions.Count == 32
                    && Same(read.Divisions["oilers"], "Pacific")
                    && Same(read.Conferences["oilers"], "Western"),
                "the save keeps the league, divisions and conferences"
            );
            Check(
                playoffs.League == League.Nhl
                    && playoffs.Tags.Count == 16
                    && playoffs.Conferences.Count == 2
                    && read.Rules.Is(r => r.Preset == RulePreset.Nhl),
                "the save keeps the NHL playoffs and rules"
            );
            app.Season.End();
            Check(
                !app.Season.Continue(WorldBuilder.Clubs, path).HasValue,
                "an NHL save does not continue with the SHL clubs"
            );
        }

        private SeasonState FinishedSeason(
            IReadOnlyList<ClubData> clubs,
            int seed,
            string path = ""
        )
        {
            _ = app.Season.StartNew(
                clubs,
                clubs[0].Id,
                seed,
                path.Length > 0 ? path : Path.Combine(folder, "season.json")
            );
            _ = PlayoffScenes.SettleSeason(app);
            return Must(app.Season.Current);
        }

        private static T Must<T>(Maybe<T> maybe)
            where T : notnull
        {
            return maybe.TryGet(out T value)
                ? value
                : throw new InvalidOperationException("expected a value");
        }
    }
}
