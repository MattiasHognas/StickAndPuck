using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Endings
{
    /// <summary>The end-of-season scene a world plays (<see cref="DevScenes.Ending"/>): which ending, for the
    /// club in <see cref="MatchSetup.Home"/>, with <see cref="MatchSetup.Away"/> as the club that beat it
    /// (or was beaten). Presentation only: nothing is simulated, so nothing reaches a save or a replay.</summary>
    internal sealed class EndingStage(EndingKind kind)
    {
        /// <summary>Gets which ending plays.</summary>
        public EndingKind Kind { get; } = kind;

        /// <summary>Gets the title line's string key.</summary>
        public string TitleKey =>
            Kind switch
            {
                EndingKind.Champions => Text.Keys.EndingChampions,
                EndingKind.RunnersUp => Text.Keys.EndingRunnersUp,
                EndingKind.SemiFinal => Text.Keys.EndingSemiFinal,
                EndingKind.SeasonOver => Text.Keys.EndingSeasonOver,
                EndingKind.Relegated => Text.Keys.EndingRelegated,
                EndingKind.Fired => Text.Keys.EndingFired,
                _ => Text.Keys.EndingSeasonOver,
            };

        /// <summary>Reads an ending the way SAP_ENDING names it: champions, runnersup, semifinal, seasonover,
        /// relegated or fired, in any case, with or without dashes and underscores.</summary>
        public static Maybe<EndingKind> Parse(string value)
        {
            string plain = value
                .Replace("-", "", StringComparison.Ordinal)
                .Replace("_", "", StringComparison.Ordinal);
            return
                plain.Length > 0
                && !plain.All(char.IsDigit)
                && Enum.TryParse(plain, ignoreCase: true, out EndingKind kind)
                ? kind
                : Maybe.None<EndingKind>();
        }
    }
}
