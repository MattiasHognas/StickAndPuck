using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Season.Morale
{
    /// <summary>Team morale: one number per club, 0 to 100 with 50 neutral, kept in <see cref="SeasonState.Morale"/>
    /// and saved with the season. Every result moves it (a regulation win more than an overtime one, a big
    /// margin and a streak of three or more add to it) and it drifts back toward neutral, so it stays a
    /// story of recent form rather than a running total. It starts the momentum meter of a season game
    /// (<see cref="MatchMorale"/> on the setup) and tilts <see cref="QuickSim"/>. <see cref="RuleSet.Morale"/>
    /// turns it off and <see cref="RuleSet.MoraleStrength"/> scales it; other systems move it with
    /// <see cref="Nudge"/> (a rivalry win, a milestone, a talk).</summary>
    public static class TeamMorale
    {
        /// <summary>Neutral morale, where every club starts.</summary>
        public const int Neutral = 50;

        /// <summary>The lowest morale.</summary>
        public const int Min = 0;

        /// <summary>The highest morale.</summary>
        public const int Max = 100;

        /// <summary>How much a full (100) or empty (0) morale adds to or takes from a club's expected goals in <see cref="QuickSim"/> at normal strength.</summary>
        public const double GoalEdge = 0.12;

        private const float RegulationWin = 5f,
            ExtraWin = 4f,
            ExtraLoss = -2f,
            RegulationLoss = -5f;

        private const int MarginCap = 2,
            StreakFrom = 3,
            StreakCap = 3;

        /// <summary>The share of the distance to neutral given back after each game.</summary>
        private const float Drift = 0.15f;

        /// <summary>Gets the factor on morale's swings and effects in a season: 0 when its rules turn morale off.</summary>
        public static float Scale(SeasonState season)
        {
            return season.Rules.Or(RuleSet.Shl).MoraleScale;
        }

        /// <summary>Gets a club's morale; neutral for a club with none recorded (an older save, or a new season).</summary>
        public static int Of(SeasonState season, string club)
        {
            return season.Morale.TryGetValue(club, out int morale) ? morale : Neutral;
        }

        /// <summary>Moves a club's morale by an amount at normal strength, scaled by the season's
        /// <see cref="RuleSet.MoraleStrength"/> and kept within 0 to 100. Nothing happens when morale is off
        /// or the club is not in the season.</summary>
        /// <returns>The club's morale afterwards.</returns>
        public static int Nudge(SeasonState season, string club, int amount)
        {
            float scale = Scale(season);
            return
                scale <= 0f || amount == 0 || !season.Clubs.Contains(club, StringComparer.Ordinal)
                ? Of(season, club)
                : Set(
                    season,
                    club,
                    Of(season, club)
                        + (int)MathF.Round(amount * scale, MidpointRounding.AwayFromZero)
                );
        }

        /// <summary>Moves both clubs' morale after a game's result; <see cref="SeasonState.Record"/> calls it.</summary>
        public static void AfterResult(SeasonState season, Fixture game)
        {
            float scale = Scale(season);
            if (scale <= 0f || !game.Result.TryGet(out FixtureResult r))
            {
                return;
            }

            int home = Swing(season, game, game.Home, r.HomeGoals, r.AwayGoals, r.Kind, scale),
                away = Swing(season, game, game.Away, r.AwayGoals, r.HomeGoals, r.Kind, scale);
            _ = Set(season, game.Home, Of(season, game.Home) + home);
            _ = Set(season, game.Away, Of(season, game.Away) + away);
        }

        /// <summary>Works out every club's morale again from the results so far, for a save from before morale.</summary>
        public static void Rebuild(SeasonState season)
        {
            season.Morale.Clear();
            foreach (
                Fixture game in season
                    .Games.Where(g => g.Played)
                    .OrderBy(g => g.Round)
                    .ThenBy(g => g.Number)
            )
            {
                AfterResult(season, game);
            }
        }

        /// <summary>Gets the factor on a club's expected goals in <see cref="QuickSim"/>: 1 at neutral morale or with morale off.</summary>
        public static double GoalLift(SeasonState season, string club)
        {
            return 1.0
                + (GoalEdge * Scale(season) * (Of(season, club) - Neutral) / (Max - Neutral));
        }

        /// <summary>Settles a season game on paper with both clubs' morale tilting the odds.</summary>
        public static FixtureResult Sim(SeasonState season, ClubData home, ClubData away, int seed)
        {
            return QuickSim.Play(
                home,
                away,
                seed,
                GoalLift(season, home.Id),
                GoalLift(season, away.Id)
            );
        }

        /// <summary>Gets both clubs' morale for a game about to start, or None with morale off.</summary>
        public static Maybe<MatchMorale> ForGame(SeasonState season, string home, string away)
        {
            return Scale(season) <= 0f
                ? Maybe.None<MatchMorale>()
                : new MatchMorale(Of(season, home), Of(season, away));
        }

        private static int Set(SeasonState season, string club, int morale)
        {
            int value = Math.Clamp(morale, Min, Max);
            season.Morale[club] = value;
            return value;
        }

        private static int Swing(
            SeasonState season,
            Fixture game,
            string club,
            int scored,
            int conceded,
            ResultKind kind,
            float scale
        )
        {
            float change = 0f;
            if (scored != conceded)
            {
                bool won = scored > conceded,
                    extra = kind != ResultKind.Regulation;
                float sign = won ? 1f : -1f;
                change = won
                    ? (extra ? ExtraWin : RegulationWin)
                    : (extra ? ExtraLoss : RegulationLoss);
                if (!extra)
                {
                    change += sign * Math.Min(Math.Abs(scored - conceded) - 1, MarginCap);
                }

                int streak = Streak(season, game, club, won);
                if (streak >= StreakFrom)
                {
                    change += sign * Math.Min(streak - StreakFrom + 1, StreakCap);
                }
            }

            change = (change * scale) - ((Of(season, club) - Neutral) * Drift);
            return (int)MathF.Round(change, MidpointRounding.AwayFromZero);
        }

        // games in a row with this game's outcome, this one included; earlier rounds only, so the order games are recorded in does not matter
        private static int Streak(SeasonState season, Fixture game, string club, bool won)
        {
            int streak = 1;
            foreach (
                Fixture before in season
                    .Games.Where(g => g.Round < game.Round && g.Played && g.Involves(club))
                    .OrderByDescending(g => g.Round)
            )
            {
                FixtureResult r = before.Result.Or(new FixtureResult(0, 0, ResultKind.Regulation));
                bool home = string.Equals(before.Home, club, StringComparison.Ordinal);
                int own = home ? r.HomeGoals : r.AwayGoals,
                    other = home ? r.AwayGoals : r.HomeGoals;
                if (own == other || (own > other) != won)
                {
                    break;
                }

                streak++;
            }

            return streak;
        }
    }
}
