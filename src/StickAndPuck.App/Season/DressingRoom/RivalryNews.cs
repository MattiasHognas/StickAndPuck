using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Schedule;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>Rivalries (W131) in the dressing room: when the club's next game is against a rival, a gold news
    /// line says so (the derby in the SHL, rivalry night in the NHL). Nothing with <see cref="RuleSet.Rivalries"/> off.</summary>
    internal sealed class RivalryNews : IDressingRoomSource
    {
        /// <summary>The news priority: above the placeholder season news.</summary>
        public const int Priority = 5;

        /// <inheritdoc/>
        public void Fill(DressingRoomContext context, DressingRoomReport report)
        {
            if (
                context.Rules.RivalryScale <= 0f
                || !context.Season.NextGame.TryGet(out Fixture next)
                || !Rivals.Are(next.Home, next.Away)
            )
            {
                return;
            }

            string user = context.UserClub;
            string rival = string.Equals(next.Home, user, StringComparison.Ordinal)
                ? next.Away
                : next.Home;
            report.AddNews(
                new DressingRoomEntry(
                    Text.Format(
                        context.Season.League == League.Nhl ? K.NewsNextRivalry : K.NewsNextDerby,
                        context.Name(rival).ToUpperInvariant()
                    ),
                    Priority,
                    Highlight: true
                )
            );
        }
    }
}
