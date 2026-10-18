using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Replay
{
    /// <summary>A moment in a match to scrub to: a period and the clock, or a step.</summary>
    /// <param name="Period">The period, or 0 for a step.</param>
    /// <param name="Clock">The clock as the scorebug shows it (seconds left), or the step.</param>
    public sealed record ScrubTarget(int Period, double Clock)
    {
        /// <summary>Reads SAP_AT: "2:12:30" (period 2 at 12:30 on the clock), "2:750" (seconds) or "t3600" (step 3600).</summary>
        public static Maybe<ScrubTarget> Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Maybe.None<ScrubTarget>();
            }

            CultureInfo c = CultureInfo.InvariantCulture;
            if (text.StartsWith('t', StringComparison.Ordinal))
            {
                return new ScrubTarget(0, long.Parse(text[1..], c));
            }

            string[] parts = text.Split(':');
            if (parts.Length is < 2 or > 3)
            {
                throw new FormatException(
                    $"SAP_AT={text}: use period:mm:ss, period:seconds or t<step>"
                );
            }

            int period = int.Parse(parts[0], c);
            double seconds =
                parts.Length == 3
                    ? (int.Parse(parts[1], c) * 60) + double.Parse(parts[2], c)
                    : double.Parse(parts[1], c);
            return new ScrubTarget(period, seconds);
        }

        /// <summary>Whether the world has reached the moment.</summary>
        public bool Hit(World world)
        {
            if (Period == 0)
            {
                return world.Tick - 1 >= (long)Clock;
            }

            MatchState m = world.Match;
            return m.Period > Period
                || (m.Period == Period && m.Clock <= Clock && m.Phase != MatchPhase.Intro);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Period == 0
                ? string.Create(CultureInfo.InvariantCulture, $"step {Clock}")
                : $"period {Period} {MatchFlow.ClockText((float)Clock)}";
        }
    }
}
