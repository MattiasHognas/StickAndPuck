using System.Globalization;
using System.Runtime.InteropServices;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Arcade;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Schedule;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.Board
{
    /// <summary>The board's expectation (W133). At season start the board ranks the human's club by strength
    /// (<see cref="ArcadeRun.Strength"/>, the overall on team select) against every club of the league and sets
    /// a <see cref="BoardGoal"/>: the strongest must win the regular season, the next must finish near the top, the
    /// middle must make the league's own playoffs and the weakest must stay out of the bottom. The goal is
    /// saved on <see cref="SeasonState.Goal"/> (an older save gets it on load). The dressing room shows the
    /// club's place against it (<see cref="Standing"/>); with <see cref="RuleSet.HotSeat"/> on, a club far below
    /// it late in the regular season, or well below it at the end, is fired (<see cref="CheckHotSeat"/>), which
    /// ends its season. <see cref="Verdict"/> is the board's word on a finished season.</summary>
    public static class BoardExpectation
    {
        /// <summary>The league size whose strongest club alone is expected to win (the SHL); a bigger league expects it of more clubs.</summary>
        private const double BandLeague = 14.0;

        /// <summary>The share of the league the club may finish below its goal's place before the board loses patience.</summary>
        private const double MarginShare = 0.2;

        /// <summary>How far into the regular season (the human's games played) a <see cref="HotSeat.Lenient"/> board judges.</summary>
        public const double LenientFrom = 0.75;

        /// <summary>How far into the regular season a <see cref="HotSeat.Strict"/> board judges.</summary>
        public const double StrictFrom = 0.5;

        /// <summary>Sets the board's goal for a club: by its strength rank among the league's clubs.</summary>
        public static BoardGoal Pick(IReadOnlyList<ClubData> clubs, string userClub)
        {
            int rank = Rank(clubs, userClub);
            League league = clubs.Count > 0 ? clubs[0].League : League.Shl;
            Lines lines = LinesOf(league, clubs.Count);
            return rank <= lines.Band ? BoardGoal.WinLeague
                : rank <= lines.Top ? BoardGoal.Top
                : rank <= lines.Playoffs ? BoardGoal.MakePlayoffs
                : BoardGoal.AvoidBottom;
        }

        /// <summary>Gets a club's strength rank in its league, 1 for the strongest (equal strengths by id).</summary>
        public static int Rank(IReadOnlyList<ClubData> clubs, string userClub)
        {
            List<string> order =
            [
                .. clubs
                    .OrderByDescending(ArcadeRun.Strength)
                    .ThenBy(c => c.Id, StringComparer.Ordinal)
                    .Select(c => c.Id),
            ];
            int i = order.IndexOf(userClub);
            return i < 0
                ? throw new ArgumentException($"{userClub} is not a club", nameof(userClub))
                : i + 1;
        }

        /// <summary>Gives a season without a goal (a save from before the board) the one its club would get now.</summary>
        public static void EnsureGoal(SeasonState season, IReadOnlyList<ClubData> clubs)
        {
            if (season.Goal.HasValue)
            {
                return;
            }

            List<ClubData> playing =
            [
                .. clubs.Where(c => season.Clubs.Contains(c.Id, StringComparer.Ordinal)),
            ];
            if (playing.Exists(c => string.Equals(c.Id, season.UserClub, StringComparison.Ordinal)))
            {
                season.Goal = Pick(playing, season.UserClub);
            }
        }

        /// <summary>Gets the worst table place that still meets a goal in a league of this many clubs: 1 to win it,
        /// the top 4 (SHL) or 8 (NHL), the playoff line (SHL 10 with the play-in; NHL 16, though the NHL counts
        /// division and wild card spots, see <see cref="MeetsGoal"/>) or all but the bottom 2 (SHL) or 4 (NHL).</summary>
        public static int Line(BoardGoal goal, League league, int clubs)
        {
            Lines lines = LinesOf(league, clubs);
            return goal switch
            {
                BoardGoal.WinLeague => 1,
                BoardGoal.Top => lines.Top,
                BoardGoal.MakePlayoffs => lines.Playoffs,
                BoardGoal.AvoidBottom => lines.Avoid,
                _ => throw new NotSupportedException("no board line for " + goal),
            };
        }

        /// <summary>Gets the line of the next harder goal (finishing there exceeds this one), or None for winning the league.</summary>
        public static Maybe<int> HarderLine(BoardGoal goal, League league, int clubs)
        {
            return goal switch
            {
                BoardGoal.WinLeague => Maybe.None<int>(),
                BoardGoal.Top => 1,
                BoardGoal.MakePlayoffs => Line(BoardGoal.Top, league, clubs),
                BoardGoal.AvoidBottom => Line(BoardGoal.MakePlayoffs, league, clubs),
                _ => throw new NotSupportedException("no harder board line for " + goal),
            };
        }

        /// <summary>Gets how many places below its goal's line a club may sit before the board fires it: a fifth of
        /// the league (3 in the SHL, 6 in the NHL), but never more than there are places below the line, so
        /// finishing last always misses badly.</summary>
        public static int Margin(BoardGoal goal, League league, int clubs)
        {
            int share = Math.Max(1, (int)Math.Round(clubs * MarginShare, MidpointRounding.ToEven));
            return Math.Min(share, Math.Max(1, clubs - Line(goal, league, clubs)));
        }

        /// <summary>Gets the goal in words, in the language shown.</summary>
        public static string Describe(BoardGoal goal, League league, int clubs)
        {
            return goal switch
            {
                BoardGoal.WinLeague => Text.Get(K.BoardGoalWin),
                BoardGoal.Top => Text.Format(K.BoardGoalTop, Line(goal, league, clubs)),
                BoardGoal.MakePlayoffs => Text.Get(K.BoardGoalPlayoffs),
                BoardGoal.AvoidBottom => Text.Format(
                    K.BoardGoalAvoidBottom,
                    clubs - Line(goal, league, clubs)
                ),
                _ => throw new NotSupportedException("no words for board goal " + goal),
            };
        }

        /// <summary>Gets the season's goal in words, or an empty string when it has none.</summary>
        public static string Describe(SeasonState season)
        {
            return season.Goal.TryGet(out BoardGoal goal)
                ? Describe(goal, season.League, season.Clubs.Count)
                : "";
        }

        /// <summary>Gets the human's club's place in the table, 1 for the top.</summary>
        public static int Place(SeasonState season)
        {
            return Place(season, season.Table);
        }

        private static int Place(SeasonState season, IReadOnlyList<StandingRow> table)
        {
            for (int i = 0; i < table.Count; i++)
            {
                if (string.Equals(table[i].ClubId, season.UserClub, StringComparison.Ordinal))
                {
                    return i + 1;
                }
            }

            return table.Count;
        }

        /// <summary>Gets a value indicating whether the human's club meets a goal as the table stands: at or above its
        /// line, or for the NHL playoffs in a division or wild card spot.</summary>
        public static bool MeetsGoal(SeasonState season, BoardGoal goal)
        {
            IReadOnlyList<StandingRow> table = season.Table;
            return Meets(season, goal, table, Place(season, table));
        }

        private static bool Meets(
            SeasonState season,
            BoardGoal goal,
            IReadOnlyList<StandingRow> table,
            int place
        )
        {
            return
                goal == BoardGoal.MakePlayoffs
                && NhlPlayoffs(season, table).TryGet(out bool inField)
                ? inField
                : place <= Line(goal, season.League, season.Clubs.Count);
        }

        /// <summary>Whether the club holds an NHL playoff spot (top 3 of its division or a wild card); None outside the
        /// NHL's division format.</summary>
        private static Maybe<bool> NhlPlayoffs(SeasonState season, IReadOnlyList<StandingRow> table)
        {
            if (season.League != League.Nhl || season.Divisions.Count == 0)
            {
                return Maybe.None<bool>();
            }

            try
            {
                return NhlSeeding.Field(season, table).Tags.ContainsKey(season.UserClub);
            }
            catch (InvalidOperationException)
            {
                return Maybe.None<bool>(); // not a full NHL field: the plain playoff line
            }
        }

        /// <summary>Gets how many places the club is below its goal: 0 when it meets it.</summary>
        public static int Deficit(SeasonState season)
        {
            if (!season.Goal.TryGet(out BoardGoal goal))
            {
                return 0;
            }

            IReadOnlyList<StandingRow> table = season.Table;
            int place = Place(season, table);
            return Meets(season, goal, table, place)
                ? 0
                : Math.Max(1, place - Line(goal, season.League, season.Clubs.Count));
        }

        /// <summary>Gets where the club stands against its goal as the table stands now. A club far below its goal
        /// is <see cref="BoardStanding.HotSeat"/> only when the board can fire it (<see cref="CanFire"/>); otherwise it
        /// is under pressure. On track with no goal.</summary>
        public static BoardStanding Standing(SeasonState season)
        {
            if (!season.Goal.TryGet(out BoardGoal goal))
            {
                return BoardStanding.OnTrack;
            }

            int clubs = season.Clubs.Count;
            IReadOnlyList<StandingRow> table = season.Table;
            int place = Place(season, table);
            if (Meets(season, goal, table, place))
            {
                return HarderLine(goal, season.League, clubs).Is(line => place <= line)
                    ? BoardStanding.Ahead
                    : BoardStanding.OnTrack;
            }

            int deficit = Math.Max(1, place - Line(goal, season.League, clubs));
            return deficit >= Margin(goal, season.League, clubs) && CanFire(season, goal)
                ? BoardStanding.HotSeat
                : BoardStanding.UnderPressure;
        }

        /// <summary>Gets a value indicating whether the board can fire the human over this goal: the season's rules
        /// have the hot seat on, and the goal is not staying out of the bottom of an SHL season with the relegation
        /// playout (W76) on. Those bottom places are the playout's, and losing it (relegation, which ends the season
        /// on its own screen) is the price of missing that goal, so the board leaves it to the playout.</summary>
        public static bool CanFire(SeasonState season, BoardGoal goal)
        {
            RuleSet rules = season.Rules.Or(RuleSet.Shl);
            bool playout = season.League == League.Shl && rules.Playout;
            return rules.HotSeat != HotSeat.Off && !(goal == BoardGoal.AvoidBottom && playout);
        }

        /// <summary>Gets the share of its regular season games the human's club has played, 0 to 1.</summary>
        public static double Progress(SeasonState season)
        {
            int total = season.Games.Count(g => g.Involves(season.UserClub));
            return total == 0 ? 0 : (double)season.PlayedBy(season.UserClub).Count() / total;
        }

        /// <summary>Gets how far into the regular season the board judges under a hot seat rule, or None when it never fires.</summary>
        public static Maybe<double> JudgedFrom(HotSeat rule)
        {
            return rule switch
            {
                HotSeat.Off => Maybe.None<double>(),
                HotSeat.Lenient => LenientFrom,
                HotSeat.Strict => StrictFrom,
                _ => throw new NotSupportedException("no hot seat point for " + rule),
            };
        }

        /// <summary>Gets a value indicating whether the board fires the human now: the season's rules have the hot
        /// seat on, the regular season is far enough along (<see cref="JudgedFrom"/>; its end included) and the
        /// club sits at least <see cref="Margin"/> places below its goal. Never in the playoffs or the playout, and
        /// never where the playout decides the goal (<see cref="CanFire"/>).</summary>
        public static bool ShouldFire(SeasonState season)
        {
            return !season.Fired
                && season.Goal.HasValue
                && !season.Playoffs.HasValue
                && JudgedFrom(season.Rules.Or(RuleSet.Shl).HotSeat)
                    .Is(from => Progress(season) >= from)
                && season.Goal.Is(goal =>
                    CanFire(season, goal)
                    && Deficit(season) >= Margin(goal, season.League, season.Clubs.Count)
                );
        }

        /// <summary>Fires the human when <see cref="ShouldFire"/> says so, after each of the club's results: the
        /// season ends there (<see cref="SeasonState.Fired"/>). True when it did.</summary>
        public static bool CheckHotSeat(SeasonState season)
        {
            if (!ShouldFire(season))
            {
                return false;
            }

            season.Fired = true;
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: board: {season.UserClub} fired, place {Place(season)} of {season.Clubs.Count} against {season.Goal.Or(BoardGoal.WinLeague)}"
                )
            );
            return true;
        }

        /// <summary>Gets the board's verdict on the regular season: Missed when fired or relegated in the playout, else, once every regular season
        /// game is played, Exceeded (placed where a harder goal asks), Met or Missed; None while it runs or with no goal.</summary>
        public static Maybe<BoardVerdict> Verdict(SeasonState season)
        {
            if (
                season.Fired
                || season.Relegated.Is(r =>
                    string.Equals(r, season.UserClub, StringComparison.Ordinal)
                )
            )
            {
                return BoardVerdict.Missed;
            }

            if (!season.IsOver || !season.Goal.TryGet(out BoardGoal goal))
            {
                return Maybe.None<BoardVerdict>();
            }

            IReadOnlyList<StandingRow> table = season.Table;
            int place = Place(season, table);
            return !Meets(season, goal, table, place) ? BoardVerdict.Missed
                : HarderLine(goal, season.League, season.Clubs.Count).Is(line => place <= line)
                    ? BoardVerdict.Exceeded
                : BoardVerdict.Met;
        }

        private static Lines LinesOf(League league, int clubs)
        {
            int band = Math.Max(1, (int)Math.Round(clubs / BandLeague, MidpointRounding.ToEven));
            int top = Math.Min(clubs, 4 * band);
            int playoffs =
                league == League.Nhl
                    ? 2 * ((2 * NhlSeeding.PerDivision) + NhlSeeding.WildCards)
                    : PlayoffState.LastPlayIn;
            playoffs = Math.Min(clubs, Math.Max(top, playoffs));
            int avoid = Math.Max(1, clubs - (2 * band));
            return new Lines(band, top, playoffs, avoid);
        }

        /// <summary>The places that split a league: the strength ranks expected to win (<paramref name="Band"/>), the
        /// top goal's line, the playoff line and the bottom line.</summary>
        [StructLayout(LayoutKind.Auto)]
        private readonly record struct Lines(int Band, int Top, int Playoffs, int Avoid);
    }
}
