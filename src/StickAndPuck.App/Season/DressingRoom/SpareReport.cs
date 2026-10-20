using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Injuries;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>The spares (W86) who dress for the human's club's next game in the slots of players the list shows as
    /// injured or suspended (<see cref="AbsenceTable.StepIns"/>), on one line under the players out: "SPARE #28
    /// ALBA IN FOR BELLOWS" for one, "SPARES IN: #28 ALBA, #57 BARKLUND" for both, so the list keeps room
    /// for the players out.</summary>
    internal sealed class SpareReport : IDressingRoomSource
    {
        /// <inheritdoc/>
        public void Fill(DressingRoomContext context, DressingRoomReport report)
        {
            if (
                !context
                    .Clubs.FirstOrNone(c =>
                        string.Equals(c.Id, context.UserClub, StringComparison.Ordinal)
                    )
                    .TryGet(out ClubData club)
            )
            {
                return;
            }

            AbsenceTable absences = context.Season.Stats.Absences;
            List<SpareIn> steps =
            [
                .. AbsenceTable
                    .StepIns(club, absences.Out(club))
                    .Where(s => Listed(context, absences, club.Id, s.For.Number)),
            ];
            if (steps.Count > 0)
            {
                report.AddInjury(new DressingRoomEntry(Line(steps), 0, Highlight: false));
            }
        }

        /// <summary>Gets a value indicating whether the player a spare stands in for is on the list of players out:
        /// injured with <see cref="RuleSet.Injuries"/> on, or suspended with <see cref="RuleSet.Suspensions"/> on.</summary>
        private static bool Listed(
            DressingRoomContext context,
            AbsenceTable absences,
            string club,
            int number
        )
        {
            return absences.Players.Any(a =>
                a.Is(club, number)
                && a.GamesLeft > 0
                && (
                    a.Reason == AbsenceReason.Injury
                        ? context.Rules.Injuries
                        : context.Rules.Suspensions
                )
            );
        }

        /// <summary>Gets the spares' line: "SPARE #28 ALBA IN FOR BELLOWS" for one spare, "SPARES IN: #28 ALBA,
        /// #57 BARKLUND" for more.</summary>
        public static string Line(IReadOnlyList<SpareIn> steps)
        {
            return steps.Count == 1
                ? Text.Format(
                    K.SpareLine,
                    steps[0].Spare.Number,
                    steps[0].Spare.LastName.ToUpperInvariant(),
                    steps[0].For.LastName.ToUpperInvariant()
                )
                : Text.Format(
                    K.SparesLine,
                    string.Join(
                        ", ",
                        steps.Select(s =>
                            Text.Format(
                                K.SpareName,
                                s.Spare.Number,
                                s.Spare.LastName.ToUpperInvariant()
                            )
                        )
                    )
                );
        }
    }
}
