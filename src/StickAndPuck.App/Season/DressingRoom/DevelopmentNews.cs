using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Stats;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>Development news (W132): a young player of the human's club whose development reached a new whole
    /// point in the last game ("levels up: +2"), highlighted, and a veteran whose decline did. Nothing
    /// with the development rule off.</summary>
    internal sealed class DevelopmentNews : IDressingRoomSource
    {
        /// <summary>The news priority, over the season's placeholder lines.</summary>
        public const int Priority = 5;

        /// <inheritdoc/>
        public void Fill(DressingRoomContext context, DressingRoomReport report)
        {
            if (context.Rules.DevelopmentScale <= 0 || !context.Last.TryGet(out LastGame last))
            {
                return;
            }

            string user = context.UserClub;
            if (
                !context
                    .Clubs.FirstOrNone(c => string.Equals(c.Id, user, StringComparison.Ordinal))
                    .TryGet(out ClubData club)
            )
            {
                return;
            }

            foreach (
                PlayerDevelopment d in context.Season.Stats.Development.Players.Where(d =>
                    string.Equals(d.Club, user, StringComparison.Ordinal)
                    && string.Equals(d.Game, last.Key, StringComparison.Ordinal)
                )
            )
            {
                int now = DevelopmentTable.Scaled(d.Value, context.Rules),
                    before = DevelopmentTable.Scaled(d.Before, context.Rules);
                if (
                    now == before
                    || !club.Everyone()
                        .FirstOrNone(p => p.Number == d.Number)
                        .TryGet(out PlayerData p)
                )
                {
                    continue;
                }

                if (now > before && now > 0)
                {
                    report.AddNews(
                        new DressingRoomEntry(
                            Text.Format(K.NewsDevelopUp, p.LastName, p.Age, Signed(now)),
                            Priority,
                            Highlight: true
                        )
                    );
                }
                else if (now < before && now < 0)
                {
                    report.AddNews(
                        new DressingRoomEntry(
                            Text.Format(K.NewsDevelopDown, p.LastName, p.Age, Signed(now)),
                            Priority
                        )
                    );
                }
            }
        }

        /// <summary>Gets points with their sign: "+2", "-1".</summary>
        public static string Signed(int points)
        {
            return points.ToString("+0;-0;0", CultureInfo.InvariantCulture);
        }
    }
}
