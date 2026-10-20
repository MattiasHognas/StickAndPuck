using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Injuries;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>The suspended players of the human's club, on the dressing room's list of players out beside the
    /// injured: "#17 LINDHOLM, SUSPENDED: OUT 2 GAMES", the longest first; one handed out in the last game
    /// (none of its games served yet) in gold. Nothing with <see cref="RuleSet.Suspensions"/> off.</summary>
    internal sealed class SuspensionReport : IDressingRoomSource
    {
        /// <inheritdoc/>
        public void Fill(DressingRoomContext context, DressingRoomReport report)
        {
            if (!context.Rules.Suspensions)
            {
                return;
            }

            IReadOnlyList<PlayerData> roster = context
                .Clubs.FirstOrNone(c =>
                    string.Equals(c.Id, context.UserClub, StringComparison.Ordinal)
                )
                .Select(c => (IReadOnlyList<PlayerData>)[.. c.Everyone()])
                .Or([]);
            foreach (
                Absence a in context
                    .Season.Stats.Absences.Of(context.UserClub)
                    .Where(a => a.Reason == AbsenceReason.Suspension && a.GamesLeft > 0)
            )
            {
                string name = roster
                    .FirstOrNone(p => p.Number == a.Number)
                    .Select(p => p.LastName)
                    .Or("");
                report.AddInjury(
                    new DressingRoomEntry(Line(a, name), a.GamesLeft, a.GamesLeft == a.Games)
                );
            }
        }

        /// <summary>Gets a suspension's line: "#17 LINDHOLM, SUSPENDED: OUT 2 GAMES".</summary>
        public static string Line(Absence a, string name)
        {
            return Text.Format(
                K.InjuryLine,
                a.Number,
                name.ToUpperInvariant(),
                Text.Get(K.Suspended),
                a.GamesLeft == 1
                    ? Text.Get(K.InjuryOutOne)
                    : Text.Format(K.InjuryOutGames, a.GamesLeft)
            );
        }
    }
}
