using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Suspensions
{
    /// <summary>How many season games a suspension costs. Each offence in a match is worth one game, a dangerous
    /// hit's major and game misconduct two; a second offence in the same match adds one. The rules'
    /// <see cref="RuleSet.SuspensionStrength"/> takes one off (mild) or adds one (strong), and the result is
    /// always <see cref="MinGames"/> to <see cref="MaxGames"/>. No dice: the same match always suspends the same.</summary>
    public static class SuspensionModel
    {
        /// <summary>The shortest suspension.</summary>
        public const int MinGames = 1;

        /// <summary>The longest suspension.</summary>
        public const int MaxGames = 3;

        /// <summary>Gets the games one offence is worth: two for a major and game misconduct for a hit, else one.</summary>
        public static int Offence(SuspensionCause cause, bool majorForAHit)
        {
            return cause == SuspensionCause.MatchPenalty && majorForAHit ? 2 : 1;
        }

        /// <summary>Gets the games a player's offences in one match cost under the rules: 0 with suspensions off or no offence.</summary>
        public static int Games(IReadOnlyCollection<int> offences, RuleSet rules)
        {
            if (!rules.Suspensions || offences.Count == 0)
            {
                return 0;
            }

            int games = offences.Max() + offences.Count - 1 + rules.SuspensionShift;
            return Math.Clamp(games, MinGames, MaxGames);
        }
    }
}
