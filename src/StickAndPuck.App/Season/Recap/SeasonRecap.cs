using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Board;
using StickAndPuck.App.Season.Challenges;
using StickAndPuck.App.Season.Injuries;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;
using StickAndPuck.App.Season.Trades;

namespace StickAndPuck.App.Season.Recap
{
    /// <summary>The season recap (W136): the human's season told in one page once it is over, after the trophy, the
    /// relegation or the sack. The regular season by "month" (<see cref="Blocks"/>), the best game, the club's top
    /// scorer, its injuries (<see cref="SeasonStats.Injuries"/>), the board's verdict (<see cref="BoardExpectation.Verdict"/>)
    /// and a few newspaper headlines picked from what happened (<see cref="Headlines"/>). Worked out from the
    /// save alone, so it needs no state of its own; <see cref="Screens.SeasonRecapScreen"/> shows it.</summary>
    public sealed class SeasonRecap
    {
        /// <summary>The "months" the regular season is split into: September to March in the SHL, October to April in the NHL.</summary>
        public const int Months = 7;

        /// <summary>The run of wins (or losses) in a row that makes a headline.</summary>
        public const int StreakLength = 5;

        /// <summary>The development points (W132) that make a young player's season a breakthrough.</summary>
        public const int BreakthroughPoints = 2;

        /// <summary>The goals in a game that make a hat-trick.</summary>
        public const int HatTrickGoals = 3;

        /// <summary>The most headlines the recap prints.</summary>
        public const int MaxHeadlines = 4;

        private SeasonRecap(SeasonState season)
        {
            UserClub = season.UserClub;
            League = season.League;
            ClubCount = season.Clubs.Count;
        }

        /// <summary>Gets the human's club id.</summary>
        public string UserClub { get; }

        /// <summary>Gets the league the season was played in.</summary>
        public League League { get; }

        /// <summary>Gets the clubs in the league.</summary>
        public int ClubCount { get; }

        /// <summary>Gets the club's place in the regular season table, 1 for the top.</summary>
        public int Place { get; private set; }

        /// <summary>Gets how the season ended.</summary>
        public SeasonEnding Ending { get; private set; }

        /// <summary>Gets the whole regular season's record (its <see cref="RecapBlock.Month"/> is -1).</summary>
        public RecapBlock Total { get; private set; }

        /// <summary>Gets the regular season by month, <see cref="Months"/> of them.</summary>
        public IReadOnlyList<RecapBlock> Blocks { get; private set; } = [];

        /// <summary>Gets the best game: the biggest win (more goals, then a playoff game, then the later one breaks a
        /// tie), or without a win the narrowest defeat; None before any game.</summary>
        public Maybe<RecapGame> BestGame { get; private set; }

        /// <summary>Gets the club's top scorer over the regular season and the playoffs, None when nobody scored.</summary>
        public Maybe<PlayerStats> TopScorer { get; private set; }

        /// <summary>Gets the club's injuries of the season, oldest first.</summary>
        public IReadOnlyList<InjuryRecord> Injuries { get; private set; } = [];

        /// <summary>Gets the games the club's injured players were out for, together.</summary>
        public int GamesLost => Injuries.Sum(i => i.Games);

        /// <summary>Gets the longest injury, None without one.</summary>
        public Maybe<InjuryRecord> LongestInjury =>
            Injuries.OrderByDescending(i => i.Games).FirstOrNone();

        /// <summary>Gets the board's goal, None in a season without one.</summary>
        public Maybe<BoardGoal> Goal { get; private set; }

        /// <summary>Gets the board's verdict, None with no goal.</summary>
        public Maybe<BoardVerdict> Verdict { get; private set; }

        /// <summary>Gets the headlines, the season's ending first, at most <see cref="MaxHeadlines"/>.</summary>
        public IReadOnlyList<RecapHeadline> Headlines { get; private set; } = [];

        /// <summary>Gets every headline the season gives, in order, before <see cref="Headlines"/> keeps the first <see cref="MaxHeadlines"/>.</summary>
        public IReadOnlyList<RecapHeadline> Candidates { get; private set; } = [];

        /// <summary>Gets the human's games, regular season then playoffs and playout, oldest first.</summary>
        public IReadOnlyList<RecapGame> Games { get; private set; } = [];

        /// <summary>Works the recap out from a season and its clubs (as traded, for the names and ages). Credit the
        /// season's stats first (<see cref="SeasonStats.CatchUp"/>) so games settled on paper count.</summary>
        public static SeasonRecap Build(SeasonState season, IReadOnlyList<ClubData> clubs)
        {
            List<Fixture> regular = [.. season.Games.Where(g => g.Involves(season.UserClub))];
            List<RecapGame> games = [.. regular.Where(g => g.Played).Select(g => From(season, g))];
            games.AddRange(PlayoffGames(season));
            SeasonRecap recap = new(season)
            {
                Place = BoardExpectation.Place(season),
                Ending = EndingOf(season),
                Games = games,
                Blocks = BlocksOf(season, regular),
                Total = Block(-1, season, regular),
                BestGame = Best(games),
                TopScorer = TopOf(season.Stats, season.UserClub),
                Injuries =
                [
                    .. season.Stats.Injuries.Where(i =>
                        string.Equals(i.Club, season.UserClub, StringComparison.Ordinal)
                    ),
                ],
                Goal = season.Goal,
                Verdict = BoardExpectation.Verdict(season),
            };
            recap.Candidates = PickHeadlines(season, clubs, recap);
            recap.Headlines = [.. recap.Candidates.Take(MaxHeadlines)];
            return recap;
        }

        /// <summary>Gets how the human's season ended: fired, relegated, the final won or lost, out in the semi-finals,
        /// or over some other way.</summary>
        public static SeasonEnding EndingOf(SeasonState season)
        {
            if (season.Fired)
            {
                return SeasonEnding.Fired;
            }

            if (!season.Playoffs.TryGet(out PlayoffState playoffs))
            {
                return SeasonEnding.SeasonOver;
            }

            if (playoffs.UserRelegated)
            {
                return SeasonEnding.Relegated;
            }

            if (playoffs.Champion.Is(c => Same(c, season.UserClub)))
            {
                return SeasonEnding.Champions;
            }

            int last = LastRound(playoffs);
            int finalRound = PlayoffState.RoundCount - 1;
            return last == finalRound && playoffs.Rounds[last].IsOver ? SeasonEnding.RunnersUp
                : last == finalRound - 1 && Lost(playoffs, last) ? SeasonEnding.Semifinal
                : SeasonEnding.SeasonOver;
        }

        /// <summary>Gets the regular season's blocks: the human's schedule split into <see cref="Months"/> equal runs of games.</summary>
        private static List<RecapBlock> BlocksOf(SeasonState season, List<Fixture> regular)
        {
            int first = League.Nhl == season.League ? 1 : 0; // the SHL starts in September, the NHL in October
            List<RecapBlock> blocks = [];
            for (int i = 0; i < Months; i++)
            {
                int from = i * regular.Count / Months,
                    to = (i + 1) * regular.Count / Months;
                blocks.Add(Block(first + i, season, regular.GetRange(from, to - from)));
            }

            return blocks;
        }

        private static RecapBlock Block(int month, SeasonState season, List<Fixture> games)
        {
            TablePoints system = season.Rules.Or(RuleSet.Shl).TablePoints;
            int played = 0,
                wins = 0,
                losses = 0,
                overtime = 0,
                points = 0;
            foreach (Fixture g in games)
            {
                if (!g.Result.TryGet(out FixtureResult result))
                {
                    continue;
                }

                bool home = Same(g.Home, season.UserClub);
                int own = home ? result.HomeGoals : result.AwayGoals,
                    other = home ? result.AwayGoals : result.HomeGoals;
                played++;
                points += result.PointsFor(home, system);
                if (own > other)
                {
                    wins++;
                }
                else if (result.Kind == ResultKind.Regulation)
                {
                    losses++;
                }
                else
                {
                    overtime++;
                }
            }

            return new RecapBlock(month, games.Count, played, wins, losses, overtime, points);
        }

        private static RecapGame From(SeasonState season, Fixture g)
        {
            FixtureResult r = g.Result.Or(new FixtureResult(0, 0, ResultKind.Regulation));
            bool home = Same(g.Home, season.UserClub);
            return new RecapGame(
                home ? g.Away : g.Home,
                home ? r.HomeGoals : r.AwayGoals,
                home ? r.AwayGoals : r.HomeGoals,
                home,
                Playoff: false,
                r.Kind
            );
        }

        /// <summary>The human's playoff games, round by round, then the playout's.</summary>
        private static IEnumerable<RecapGame> PlayoffGames(SeasonState season)
        {
            if (!season.Playoffs.TryGet(out PlayoffState playoffs))
            {
                yield break;
            }

            IEnumerable<PlayoffSeries> series = playoffs.Rounds.SelectMany(r => r.Series);
            if (playoffs.Playout.TryGet(out PlayoffSeries playout))
            {
                series = series.Append(playout);
            }

            foreach (PlayoffSeries s in series.Where(s => s.Involves(season.UserClub)))
            {
                foreach (PlayoffGame g in s.Games)
                {
                    bool home = Same(g.Home, season.UserClub);
                    yield return new RecapGame(
                        home ? g.Away : g.Home,
                        home ? g.Result.HomeGoals : g.Result.AwayGoals,
                        home ? g.Result.AwayGoals : g.Result.HomeGoals,
                        home,
                        Playoff: true,
                        g.Result.Kind
                    );
                }
            }
        }

        private static Maybe<RecapGame> Best(List<RecapGame> games)
        {
            return games
                .Select((g, i) => (Game: g, Order: i))
                .OrderByDescending(x => x.Game.Margin)
                .ThenByDescending(x => x.Game.For)
                .ThenByDescending(x => x.Game.Playoff)
                .ThenByDescending(x => x.Order)
                .Select(x => x.Game)
                .FirstOrNone();
        }

        /// <summary>The club's top scorer, the regular season and the playoffs together: points, then goals.</summary>
        private static Maybe<PlayerStats> TopOf(SeasonStats stats, string club)
        {
            Dictionary<int, PlayerStats> byNumber = [];
            foreach (
                PlayerStats p in stats
                    .Regular.Players.Concat(stats.Playoffs.Players)
                    .Where(p => Same(p.Club, club) && !p.IsGoalie)
            )
            {
                if (!byNumber.TryGetValue(p.Number, out PlayerStats sum))
                {
                    sum = new PlayerStats
                    {
                        Club = club,
                        Number = p.Number,
                        Name = p.Name,
                        Position = p.Position,
                    };
                    byNumber[p.Number] = sum;
                }

                sum.Games += p.Games;
                sum.Goals += p.Goals;
                sum.Assists += p.Assists;
            }

            return byNumber
                .Values.Where(p => p.Points > 0)
                .OrderByDescending(p => p.Points)
                .ThenByDescending(p => p.Goals)
                .ThenBy(p => p.Number)
                .FirstOrNone();
        }

        /// <summary>Picks the headlines: how the season ended first, then what stood out, in <see cref="HeadlineKind"/> order.</summary>
        private static List<RecapHeadline> PickHeadlines(
            SeasonState season,
            IReadOnlyList<ClubData> clubs,
            SeasonRecap recap
        )
        {
            string club = Name(clubs, season.UserClub);
            List<RecapHeadline> lines = [Outcome(season, recap.Ending, club)];
            if (recap.Place == 1 && season.IsOver && !season.Fired)
            {
                lines.Add(new RecapHeadline(HeadlineKind.RegularSeasonWinner, club));
            }

            List<RecapGame> regular = [.. recap.Games.Where(g => !g.Playoff)];
            int wins = Longest(regular, won: true),
                losses = Longest(regular, won: false);
            if (wins >= StreakLength)
            {
                lines.Add(new RecapHeadline(HeadlineKind.WinStreak, club, Number: wins));
            }

            AddHatTrick(lines, season, clubs);
            AddDerby(lines, season, clubs, recap.Games, club);
            AddBreakthrough(lines, season, clubs);
            AddTrade(lines, season.Trades);
            int settled = season.Challenges.Count(c => c.Outcome != ChallengeOutcome.Open),
                met = season.Challenges.Count(c => c.Outcome == ChallengeOutcome.Met);
            if (met > 0)
            {
                lines.Add(new RecapHeadline(HeadlineKind.Challenges, club, "", met, settled));
            }

            if (losses >= StreakLength)
            {
                lines.Add(new RecapHeadline(HeadlineKind.LosingStreak, club, Number: losses));
            }

            return lines;
        }

        private static RecapHeadline Outcome(SeasonState season, SeasonEnding ending, string club)
        {
            Maybe<PlayoffState> playoffs = season.Playoffs;
            HeadlineKind kind = ending switch
            {
                SeasonEnding.Fired => HeadlineKind.Fired,
                SeasonEnding.Relegated => HeadlineKind.Relegated,
                SeasonEnding.Champions => HeadlineKind.Champions,
                SeasonEnding.RunnersUp => HeadlineKind.RunnersUp,
                SeasonEnding.Semifinal => HeadlineKind.Semifinal,
                SeasonEnding.SeasonOver when playoffs.Is(p => p.UserInPlayout) =>
                    HeadlineKind.StayedUp,
                SeasonEnding.SeasonOver when playoffs.Is(p => p.UserQualified) =>
                    HeadlineKind.PlayoffExit,
                SeasonEnding.SeasonOver => HeadlineKind.MissedPlayoffs,
                _ => throw new NotSupportedException("no headline for ending " + ending),
            };
            return new RecapHeadline(kind, club);
        }

        /// <summary>The longest run of wins (or of losses, overtime ones too) in a row.</summary>
        private static int Longest(List<RecapGame> games, bool won)
        {
            int best = 0,
                run = 0;
            foreach (RecapGame g in games)
            {
                run = g.Won == won ? run + 1 : 0;
                best = Math.Max(best, run);
            }

            return best;
        }

        /// <summary>The club's hat-trick hero: the player with the most games of three goals or more (as player of
        /// the game), against the opponent of his latest.</summary>
        private static void AddHatTrick(
            List<RecapHeadline> lines,
            SeasonState season,
            IReadOnlyList<ClubData> clubs
        )
        {
            List<PlayerOfGameAward> hats =
            [
                .. season.Stats.AwardsOf(season.UserClub).Where(a => a.Goals >= HatTrickGoals),
            ];
            if (
                !hats.GroupBy(a => a.Number)
                    .OrderByDescending(g => g.Count())
                    .ThenByDescending(g => hats.LastIndexOf(g.Last()))
                    .FirstOrNone()
                    .TryGet(out IGrouping<int, PlayerOfGameAward> hero)
            )
            {
                return;
            }

            PlayerOfGameAward latest = hero.Last();
            string opponent = Same(latest.Home, season.UserClub) ? latest.Away : latest.Home;
            lines.Add(
                new RecapHeadline(
                    HeadlineKind.HatTrick,
                    latest.Name,
                    Name(clubs, opponent),
                    hero.Count()
                )
            );
        }

        /// <summary>The derby: the rival the club met most, when it won or lost more of those games than not.</summary>
        private static void AddDerby(
            List<RecapHeadline> lines,
            SeasonState season,
            IReadOnlyList<ClubData> clubs,
            IReadOnlyList<RecapGame> games,
            string club
        )
        {
            if (
                !games
                    .Where(g => Rivals.Are(season.UserClub, g.Opponent))
                    .GroupBy(g => g.Opponent, StringComparer.Ordinal)
                    .Select(g =>
                        (Rival: g.Key, Won: g.Count(x => x.Won), Lost: g.Count(x => !x.Won))
                    )
                    .Where(d => d.Won != d.Lost)
                    .OrderByDescending(d => d.Won + d.Lost)
                    .ThenByDescending(d => Math.Abs(d.Won - d.Lost))
                    .ThenBy(d => d.Rival, StringComparer.Ordinal)
                    .FirstOrNone()
                    .TryGet(out (string Rival, int Won, int Lost) derby)
            )
            {
                return;
            }

            lines.Add(
                new RecapHeadline(
                    derby.Won > derby.Lost ? HeadlineKind.DerbyWins : HeadlineKind.DerbyLosses,
                    club,
                    Name(clubs, derby.Rival),
                    derby.Won,
                    derby.Lost
                )
            );
        }

        /// <summary>The young player who developed most (W132), when it is <see cref="BreakthroughPoints"/> or more.</summary>
        private static void AddBreakthrough(
            List<RecapHeadline> lines,
            SeasonState season,
            IReadOnlyList<ClubData> clubs
        )
        {
            if (
                !clubs.FirstOrNone(c => Same(c.Id, season.UserClub)).TryGet(out ClubData club)
                || !club.Everyone()
                    .Where(p => p.Age is > 0 and <= DevelopmentTable.YoungAge)
                    .Select(p => (Player: p, Points: season.DevelopmentPoints(club.Id, p.Number)))
                    .Where(x => x.Points >= BreakthroughPoints)
                    .OrderByDescending(x => x.Points)
                    .ThenBy(x => x.Player.Age)
                    .ThenBy(x => x.Player.Number)
                    .FirstOrNone()
                    .TryGet(out (PlayerData Player, int Points) young)
            )
            {
                return;
            }

            lines.Add(
                new RecapHeadline(
                    HeadlineKind.Breakthrough,
                    young.Player.LastName,
                    "",
                    young.Player.Age,
                    young.Points
                )
            );
        }

        /// <summary>The trade the club made at the deadline (the last, when more than one).</summary>
        private static void AddTrade(List<RecapHeadline> lines, SeasonTrades trades)
        {
            if (
                Enumerable
                    .Reverse(trades.Offers.Where(o => o.Answer == TradeAnswer.Accepted))
                    .FirstOrNone()
                    .TryGet(out TradeOffer deal)
            )
            {
                lines.Add(new RecapHeadline(HeadlineKind.Trade, deal.GetName, deal.GiveName));
            }
        }

        private static int LastRound(PlayoffState playoffs)
        {
            for (int r = playoffs.Rounds.Count - 1; r >= 0; r--)
            {
                if (playoffs.Rounds[r].Series.Any(s => s.Involves(playoffs.UserClub)))
                {
                    return r;
                }
            }

            return -1;
        }

        private static bool Lost(PlayoffState playoffs, int round)
        {
            return playoffs
                .Rounds[round]
                .Series.Any(s =>
                    s.Involves(playoffs.UserClub) && s.Winner.Is(w => !Same(w, playoffs.UserClub))
                );
        }

        private static string Name(IReadOnlyList<ClubData> clubs, string id)
        {
            return clubs.FirstOrNone(c => Same(c.Id, id)).Select(c => c.Name).Or(id);
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }
    }
}
