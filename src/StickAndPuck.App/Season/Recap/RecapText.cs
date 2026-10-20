using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Board;
using StickAndPuck.App.Season.Injuries;
using StickAndPuck.App.Season.Stats;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.Recap
{
    /// <summary>The recap's words in the language shown (<see cref="SeasonRecap"/> holds the facts).</summary>
    public static class RecapText
    {
        private static readonly string[] MonthKeys =
        [
            K.RecapMonthSep,
            K.RecapMonthOct,
            K.RecapMonthNov,
            K.RecapMonthDec,
            K.RecapMonthJan,
            K.RecapMonthFeb,
            K.RecapMonthMar,
            K.RecapMonthApr,
        ];

        /// <summary>Gets a month's short name, 0 for September.</summary>
        public static string Month(int month)
        {
            return Text.Get(MonthKeys[Math.Clamp(month, 0, MonthKeys.Length - 1)]);
        }

        /// <summary>Gets a block's record, "5-2-1", or "-" before any game of it.</summary>
        public static string Record(RecapBlock block)
        {
            return block.Played == 0
                ? "-"
                : $"{N(block.Wins)}-{N(block.Losses)}-{N(block.OvertimeLosses)}";
        }

        /// <summary>Gets the season line under the club: the record, the points and the place.</summary>
        public static string SeasonLine(SeasonRecap recap)
        {
            RecapBlock t = recap.Total;
            return Text.Format(
                K.RecapSeasonLine,
                t.Wins,
                t.Losses,
                t.OvertimeLosses,
                t.Points,
                recap.Place,
                recap.ClubCount
            );
        }

        /// <summary>Gets the best game: "6-1 AT HOME TO LULEÅ HOCKEY", or "-" before any game.</summary>
        public static string BestGame(SeasonRecap recap, IReadOnlyList<ClubData> clubs)
        {
            return recap.BestGame.TryGet(out RecapGame g)
                ? Text.Format(
                    g.Home ? K.RecapGameHome : K.RecapGameAway,
                    g.For,
                    g.Against,
                    Name(clubs, g.Opponent)
                )
                : "-";
        }

        /// <summary>Gets the top scorer: "#12 NAME, 40 PTS (18+22)", or "-".</summary>
        public static string TopScorer(SeasonRecap recap)
        {
            return recap.TopScorer.TryGet(out PlayerStats p)
                ? Text.Format(K.RecapScorerLine, p.Label, p.Points, p.Goals, p.Assists)
                : "-";
        }

        /// <summary>Gets the injuries: how many and the games they cost.</summary>
        public static string Injuries(SeasonRecap recap)
        {
            IReadOnlyList<InjuryRecord> injuries = recap.Injuries;
            return injuries.Count switch
            {
                0 => Text.Get(K.RecapInjuryNone),
                1 => Text.Format(K.RecapInjuryOne, recap.GamesLost),
                _ => Text.Format(K.RecapInjuryMany, injuries.Count, recap.GamesLost),
            };
        }

        /// <summary>Gets the board's verdict with its goal: "TARGET MET: MAKE THE PLAYOFFS", or "-".</summary>
        public static string Verdict(SeasonRecap recap)
        {
            if (
                !recap.Verdict.TryGet(out BoardVerdict verdict)
                || !recap.Goal.TryGet(out BoardGoal goal)
            )
            {
                return "-";
            }

            string target = BoardExpectation.Describe(goal, recap.League, recap.ClubCount);
            return Text.Format(
                verdict switch
                {
                    BoardVerdict.Exceeded => K.RecapVerdictExceeded,
                    BoardVerdict.Met => K.RecapVerdictMet,
                    BoardVerdict.Missed => K.RecapVerdictMissed,
                    _ => throw new NotSupportedException("no words for verdict " + verdict),
                },
                target
            );
        }

        /// <summary>Gets a headline in words.</summary>
        public static string Headline(RecapHeadline h, League league)
        {
            string key = h.Kind switch
            {
                HeadlineKind.Fired => K.HeadFired,
                HeadlineKind.Relegated => K.HeadRelegated,
                HeadlineKind.StayedUp => K.HeadStayedUp,
                HeadlineKind.Champions => league == League.Nhl
                    ? K.HeadChampionsNhl
                    : K.HeadChampionsShl,
                HeadlineKind.RunnersUp => K.HeadRunnersUp,
                HeadlineKind.Semifinal => K.HeadSemifinal,
                HeadlineKind.PlayoffExit => K.HeadPlayoffExit,
                HeadlineKind.MissedPlayoffs => K.HeadMissedPlayoffs,
                HeadlineKind.RegularSeasonWinner => K.HeadRegularSeason,
                HeadlineKind.WinStreak => K.HeadWinStreak,
                HeadlineKind.HatTrick => h.Number > 1 ? K.HeadHatTricks : K.HeadHatTrick,
                HeadlineKind.DerbyWins => K.HeadDerbyWins,
                HeadlineKind.DerbyLosses => K.HeadDerbyLosses,
                HeadlineKind.Breakthrough => K.HeadBreakthrough,
                HeadlineKind.Trade => K.HeadTrade,
                HeadlineKind.Challenges => K.HeadChallenges,
                HeadlineKind.LosingStreak => K.HeadLosingStreak,
                _ => throw new NotSupportedException("no words for headline " + h.Kind),
            };
            return Text.Format(key, h.Name, h.Other, h.Number, h.Second);
        }

        private static string Name(IReadOnlyList<ClubData> clubs, string id)
        {
            return clubs
                .FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal))
                .Select(c => c.Name)
                .Or(id);
        }

        private static string N(int n)
        {
            return n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
