using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Challenges;
using StickAndPuck.App.Season.News;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>The coach's challenge (W134) in the dressing room. Right after it is set: a gold news line
    /// ("NEW CHALLENGE: WIN 2 OF 3") and the coach setting it. While it runs: its progress ("WIN 2 OF 3 - 1 OF 3:
    /// WON"). Right after it is settled: the result in gold and the coach's verdict. The coach lines sit
    /// below the post-game reaction's priority, so a sharper reaction to the game still wins. Nothing with
    /// <see cref="RuleSet.Challenges"/> off.</summary>
    internal sealed class ChallengeNews : IDressingRoomSource
    {
        /// <summary>The news priority: above the rivalry and placeholder news, below a news event.</summary>
        public const int Priority = 8;

        /// <summary>The coach line's priority: above the placeholder, below the post-game reaction.</summary>
        public const int CoachPriority = 3;

        /// <inheritdoc/>
        public void Fill(DressingRoomContext context, DressingRoomReport report)
        {
            if (
                context.Rules.ChallengeGap <= 0
                || !ChallengeDesk.Latest(context.Season).TryGet(out SeasonChallenge c)
            )
            {
                return;
            }

            string goal = Goal(c, context);
            if (c.Outcome != ChallengeOutcome.Open)
            {
                if (c.Settled != NewsDesk.Played(context.Season))
                {
                    return; // settled games ago: old news
                }

                bool met = c.Outcome == ChallengeOutcome.Met;
                report.AddNews(
                    new DressingRoomEntry(
                        Text.Format(met ? K.ChallengeMet : K.ChallengeMissed, goal),
                        Priority,
                        Highlight: true
                    )
                );
                report.Say(
                    new DressingRoomEntry(
                        Text.Get(met ? K.CoachChallengeMet : K.CoachChallengeMissed),
                        CoachPriority,
                        Mood: met ? CoachMood.Pleased : CoachMood.Angry
                    )
                );
                return;
            }

            ChallengeTally tally = ChallengeDesk.Tally(context.Season, c);
            if (tally.Played == 0)
            {
                report.AddNews(
                    new DressingRoomEntry(
                        Text.Format(K.ChallengeNew, goal),
                        Priority,
                        Highlight: true
                    )
                );
                report.Say(new DressingRoomEntry(CoachLine(c, context), CoachPriority));
                return;
            }

            report.AddNews(new DressingRoomEntry(Progress(c, tally, goal), Priority));
        }

        /// <summary>Gets a challenge's goal as the news says it: "WIN 2 OF 3", "10 GOALS IN 3", "BEAT FÄRJESTAD".</summary>
        public static string Goal(SeasonChallenge c, DressingRoomContext context)
        {
            return c.Kind switch
            {
                ChallengeKind.WinTwo => Text.Get(K.ChallengeWinTwo),
                ChallengeKind.Shutout => Text.Get(K.ChallengeShutout),
                ChallengeKind.Goals => Text.Format(K.ChallengeGoals, c.Target),
                ChallengeKind.BeatRival => Text.Format(
                    K.ChallengeRival,
                    context.Name(c.Rival).ToUpperInvariant()
                ),
                _ => "",
            };
        }

        /// <summary>Gets the progress line: "WIN 2 OF 3 - 1 OF 3: WON", with the goals so far for a goals
        /// challenge and the goals let in for a shutout one.</summary>
        public static string Progress(SeasonChallenge c, ChallengeTally tally, string goal)
        {
            string last = c.Kind switch
            {
                ChallengeKind.Goals => Text.Format(K.ChallengeGameScored, tally.GoalsFor),
                ChallengeKind.Shutout => Text.Format(K.ChallengeGameConceded, tally.LastConceded),
                ChallengeKind.WinTwo or ChallengeKind.BeatRival or _ => Text.Get(
                    tally.LastWon ? K.ChallengeGameWon : K.ChallengeGameLost
                ),
            };
            return Text.Format(K.ChallengeProgress, goal, tally.Played, last);
        }

        private static string CoachLine(SeasonChallenge c, DressingRoomContext context)
        {
            return c.Kind switch
            {
                ChallengeKind.Shutout => Text.Get(K.CoachChallengeShutout),
                ChallengeKind.Goals => Text.Format(K.CoachChallengeGoals, c.Target),
                ChallengeKind.BeatRival => Text.Format(
                    K.CoachChallengeRival,
                    context.Name(c.Rival).ToUpperInvariant()
                ),
                ChallengeKind.WinTwo => Text.Get(K.CoachChallengeWinTwo),
                _ => Text.Get(K.CoachChallengeWinTwo),
            };
        }
    }
}
