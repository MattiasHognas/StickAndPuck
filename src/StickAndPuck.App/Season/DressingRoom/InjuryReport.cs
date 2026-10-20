using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Injuries;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>The injury list: every injured player of the human's club with what hurt him and the games he
    /// still misses, the longest first; an injury from the last game (none of its games missed yet) in
    /// gold. Nothing with <see cref="RuleSet.Injuries"/> off. Suspensions (W129) list themselves.</summary>
    internal sealed class InjuryReport : IDressingRoomSource
    {
        /// <inheritdoc/>
        public void Fill(DressingRoomContext context, DressingRoomReport report)
        {
            if (!context.Rules.Injuries)
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
                    .Where(a => a.Reason == AbsenceReason.Injury && a.GamesLeft > 0)
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

        /// <summary>Gets an injury's line: "#17 LINDHOLM, HIT: OUT 3 GAMES".</summary>
        public static string Line(Absence a, string name)
        {
            return Text.Format(
                K.InjuryLine,
                a.Number,
                name.ToUpperInvariant(),
                Text.Get(CauseKey(a.Cause)),
                a.GamesLeft == 1
                    ? Text.Get(K.InjuryOutOne)
                    : Text.Format(K.InjuryOutGames, a.GamesLeft)
            );
        }

        /// <summary>Gets the string key of what hurt a player.</summary>
        public static string CauseKey(InjuryCause cause)
        {
            return cause switch
            {
                InjuryCause.BlockedShot => K.InjuryBlockedShot,
                InjuryCause.Fight => K.InjuryFight,
                InjuryCause.Check or InjuryCause.None => K.InjuryCheck,
                _ => throw new NotSupportedException($"no text for injury cause {cause}"),
            };
        }
    }
}
