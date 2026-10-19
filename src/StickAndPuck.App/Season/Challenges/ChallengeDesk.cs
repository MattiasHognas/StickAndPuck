using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Morale;
using StickAndPuck.App.Season.News;
using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Season.Challenges
{
    /// <summary>Coach's challenges (W134). Every <see cref="RuleSet.ChallengeGap"/> regular-season games of the
    /// human's club (counted from the season start or the last challenge settled) the coach sets a challenge
    /// for its next three games: win two of three, keep a clean sheet, score <see cref="GoalTarget"/> goals,
    /// or, when a rival is among them and rivalries are on, beat the rival. Which one comes from a stream fixed
    /// by the season seed and the games played, so a season tells the same story every time. A challenge is
    /// settled as soon as it is met or can no longer be met: met lifts the club's morale by <see cref="Reward"/>,
    /// missed takes <see cref="Penalty"/> (both through <see cref="TeamMorale.Nudge"/>, so morale's rules scale
    /// them). Challenges are saved in <see cref="SeasonState.Challenges"/>; the dressing room shows them
    /// (<see cref="DressingRoom.ChallengeNews"/>).</summary>
    public static class ChallengeDesk
    {
        /// <summary>The games a challenge runs over.</summary>
        public const int Window = 3;

        /// <summary>The wins <see cref="ChallengeKind.WinTwo"/> takes.</summary>
        public const int WinTarget = 2;

        /// <summary>The goals <see cref="ChallengeKind.Goals"/> takes.</summary>
        public const int GoalTarget = 10;

        /// <summary>The morale points, at normal strength, a challenge met gives.</summary>
        public const int Reward = 6;

        /// <summary>The morale points, at normal strength, a challenge missed gives (a small dip).</summary>
        public const int Penalty = -2;

        /// <summary>The stream name of the pick.</summary>
        private const string Stream = "challenge";

        /// <summary>Gets the human's games between challenges under the season's rules: 0 with challenges off.</summary>
        public static int Gap(SeasonState season)
        {
            return season.Rules.Or(RuleSet.Shl).ChallengeGap;
        }

        /// <summary>Gets the open challenge, or None.</summary>
        public static Maybe<SeasonChallenge> Open(SeasonState season)
        {
            return season.Challenges.FirstOrNone(c => c.Outcome == ChallengeOutcome.Open);
        }

        /// <summary>Gets the latest challenge, open or settled, or None before the first.</summary>
        public static Maybe<SeasonChallenge> Latest(SeasonState season)
        {
            return season.Challenges.Count == 0
                ? Maybe.None<SeasonChallenge>()
                : season.Challenges[^1];
        }

        /// <summary>Settles the open challenge when the last game decided it, then sets a new one when one is due;
        /// <see cref="SeasonSession.Finish"/> calls it after the human's regular-season game is recorded.</summary>
        public static void AfterGame(SeasonState season)
        {
            if (Gap(season) <= 0)
            {
                return;
            }

            if (Open(season).TryGet(out SeasonChallenge open))
            {
                _ = Settle(season, open);
            }

            if (Due(season) && Pick(season).TryGet(out SeasonChallenge next))
            {
                season.Challenges.Add(next);
            }
        }

        /// <summary>Gets a value indicating whether a new challenge is due: challenges are on, none is open, and
        /// the gap has passed since the season started or the last one was settled.</summary>
        public static bool Due(SeasonState season)
        {
            int gap = Gap(season);
            int since = Latest(season).Select(c => c.Settled).Or(0);
            return gap > 0 && !Open(season).HasValue && NewsDesk.Played(season) - since >= gap;
        }

        /// <summary>Gets the human's next three regular-season games, fewer near the end of the season.</summary>
        public static IReadOnlyList<Fixture> Upcoming(SeasonState season)
        {
            return
            [
                .. season
                    .Games.Where(g => !g.Played && g.Involves(season.UserClub))
                    .OrderBy(g => g.Number)
                    .Take(Window),
            ];
        }

        /// <summary>Picks the challenge for the human's next three games from the season's dice, or None when the
        /// season has fewer than three left.</summary>
        public static Maybe<SeasonChallenge> Pick(SeasonState season)
        {
            IReadOnlyList<Fixture> games = Upcoming(season);
            if (games.Count < Window)
            {
                return Maybe.None<SeasonChallenge>();
            }

            Random dice = Dice(season);
            bool rival = RivalIn(season, games).HasValue;
            ChallengeKind kind =
                rival && dice.Next(2) == 0
                    ? ChallengeKind.BeatRival
                    : (ChallengeKind)dice.Next((int)ChallengeKind.Goals + 1);
            return Build(season, kind, games);
        }

        /// <summary>Sets a challenge of a kind for the human's next three games at once, whatever the dice say
        /// (the dressing room preview's SAP_CHALLENGE). None with fewer than three games left, or for
        /// <see cref="ChallengeKind.BeatRival"/> with no rival among them. An open challenge is replaced.</summary>
        public static Maybe<SeasonChallenge> Force(SeasonState season, ChallengeKind kind)
        {
            if (!Build(season, kind, Upcoming(season)).TryGet(out SeasonChallenge c))
            {
                return Maybe.None<SeasonChallenge>();
            }

            foreach (
                SeasonChallenge open in season
                    .Challenges.Where(o => o.Outcome == ChallengeOutcome.Open)
                    .ToList()
            )
            {
                _ = season.Challenges.Remove(open);
            }

            season.Challenges.Add(c);
            return c;
        }

        /// <summary>Gets how a challenge is going from the results of its games so far.</summary>
        public static ChallengeTally Tally(SeasonState season, SeasonChallenge challenge)
        {
            int played = 0,
                wins = 0,
                losses = 0,
                goals = 0,
                shutouts = 0,
                lastConceded = 0;
            bool rivalBeaten = false,
                rivalLost = false,
                lastWon = false;
            foreach (int number in challenge.Games.Order())
            {
                if (
                    number < 0
                    || number >= season.Games.Count
                    || !season.Games[number].Result.TryGet(out FixtureResult r)
                )
                {
                    continue;
                }

                Fixture f = season.Games[number];
                bool home = string.Equals(f.Home, season.UserClub, StringComparison.Ordinal);
                int scored = home ? r.HomeGoals : r.AwayGoals,
                    conceded = home ? r.AwayGoals : r.HomeGoals;
                bool won = scored > conceded;
                played++;
                wins += won ? 1 : 0;
                losses += won ? 0 : 1;
                goals += scored;
                shutouts += conceded == 0 ? 1 : 0;
                lastWon = won;
                lastConceded = conceded;
                if (challenge.Rival.Length > 0 && f.Involves(challenge.Rival))
                {
                    rivalBeaten |= won;
                    rivalLost |= !won;
                }
            }

            return new ChallengeTally(
                played,
                wins,
                losses,
                goals,
                shutouts,
                rivalBeaten,
                rivalLost,
                lastWon,
                lastConceded
            );
        }

        /// <summary>Gets where a challenge stands after its games so far: met as soon as the target is reached,
        /// missed as soon as it can no longer be, else open.</summary>
        public static ChallengeOutcome Judge(SeasonChallenge challenge, ChallengeTally tally)
        {
            bool done = tally.Played >= Window;
            (bool met, bool lost) = challenge.Kind switch
            {
                ChallengeKind.WinTwo => (
                    tally.Wins >= challenge.Target,
                    tally.Losses > Window - challenge.Target
                ),
                ChallengeKind.Shutout => (tally.Shutouts >= challenge.Target, done),
                ChallengeKind.Goals => (tally.GoalsFor >= challenge.Target, done),
                ChallengeKind.BeatRival => (tally.RivalBeaten, tally.RivalLost || done),
                _ => (false, done),
            };
            return met ? ChallengeOutcome.Met
                : lost ? ChallengeOutcome.Missed
                : ChallengeOutcome.Open;
        }

        /// <summary>Settles an open challenge its games have decided: records the outcome and the games played,
        /// and moves the human's club's morale by the reward or the penalty.</summary>
        /// <returns>The outcome; Open when nothing is decided yet.</returns>
        public static ChallengeOutcome Settle(SeasonState season, SeasonChallenge challenge)
        {
            if (challenge.Outcome != ChallengeOutcome.Open)
            {
                return challenge.Outcome;
            }

            ChallengeOutcome outcome = Judge(challenge, Tally(season, challenge));
            if (outcome == ChallengeOutcome.Open)
            {
                return outcome;
            }

            challenge.Outcome = outcome;
            challenge.Settled = NewsDesk.Played(season);
            challenge.Morale = outcome == ChallengeOutcome.Met ? Reward : Penalty;
            _ = TeamMorale.Nudge(season, season.UserClub, challenge.Morale);
            return outcome;
        }

        /// <summary>Gets the rival the human's club meets in some games, when rivalries are on.</summary>
        public static Maybe<string> RivalIn(SeasonState season, IReadOnlyList<Fixture> games)
        {
            if (!season.Rules.Or(RuleSet.Shl).Rivalries)
            {
                return Maybe.None<string>();
            }

            string user = season.UserClub;
            return games
                .FirstOrNone(g => Rivals.Are(g.Home, g.Away))
                .Select(g =>
                    string.Equals(g.Home, user, StringComparison.Ordinal) ? g.Away : g.Home
                );
        }

        private static Maybe<SeasonChallenge> Build(
            SeasonState season,
            ChallengeKind kind,
            IReadOnlyList<Fixture> games
        )
        {
            if (games.Count < Window)
            {
                return Maybe.None<SeasonChallenge>();
            }

            string rival = "";
            if (kind == ChallengeKind.BeatRival)
            {
                if (!RivalIn(season, games).TryGet(out string found))
                {
                    return Maybe.None<SeasonChallenge>();
                }

                rival = found;
            }

            return new SeasonChallenge
            {
                Kind = kind,
                Games = [.. games.Select(g => g.Number)],
                Target = kind switch
                {
                    ChallengeKind.WinTwo => WinTarget,
                    ChallengeKind.Goals => GoalTarget,
                    ChallengeKind.Shutout or ChallengeKind.BeatRival => 1,
                    _ => 1,
                },
                Rival = rival,
                Issued = NewsDesk.Played(season),
            };
        }

        /// <summary>The pick's dice: fixed by the season seed and the human's games played.</summary>
        private static Random Dice(SeasonState season)
        {
            int played = NewsDesk.Played(season);
            return Seeds.Dice(
                season.Seed,
                Stream + played.ToString(CultureInfo.InvariantCulture),
                104_729 * (played + 1)
            );
        }
    }
}
