using StickAndPuck.App.Core;
using StickAndPuck.App.Season.News;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>The news events in play for the human's club (W130), above the placeholder news: one line each
    /// with the players' last names and the games left, the one drawn after the last game in gold.
    /// Nothing with <see cref="RuleSet.News"/> off.</summary>
    internal sealed class NewsEvents : IDressingRoomSource
    {
        /// <summary>The priority of a news event's line, above the table and streak lines.</summary>
        public const int Priority = 10;

        /// <inheritdoc/>
        public void Fill(DressingRoomContext context, DressingRoomReport report)
        {
            if (context.Rules.NewsEvery <= 0)
            {
                return;
            }

            int played = NewsDesk.Played(context.Season);
            foreach (
                NewsEvent e in context
                    .Season.News.Where(e =>
                        string.Equals(e.Club, context.UserClub, StringComparison.Ordinal)
                    )
                    .OrderByDescending(e => e.Drawn)
            )
            {
                bool fresh = e.Drawn == played;
                report.AddNews(
                    new DressingRoomEntry(
                        Line(e, context),
                        Priority + (fresh ? 1 : 0),
                        Highlight: fresh
                    )
                );
            }
        }

        /// <summary>Gets an event's news line: {0} the players' last names, {1} the games left.</summary>
        public static string Line(NewsEvent e, DressingRoomContext context)
        {
            Maybe<ClubData> club = context.Clubs.FirstOrNone(c =>
                string.Equals(c.Id, e.Club, StringComparison.Ordinal)
            );
            string names = string.Join(
                ", ",
                e.Players.Select(n =>
                        club.Select(c =>
                                c.Roster.FirstOrNone(p => p.Number == n)
                                    .Select(p => p.LastName)
                                    .Or("")
                            )
                            .Or("")
                    )
                    .Where(s => s.Length > 0)
            );
            return Text.Format(e.Text, names, e.GamesLeft);
        }
    }
}
