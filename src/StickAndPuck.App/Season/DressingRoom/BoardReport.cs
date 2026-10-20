using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Board;
using StickAndPuck.App.Season.Schedule;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>The board's expectation (W133) in the dressing room's BOARD section: the season's target and, once
    /// the club has played, its place against it (ahead of target, on track, under pressure or on the hot
    /// seat), in gold when the board is not happy. Only in the regular season; nothing in a season without
    /// a goal.</summary>
    internal sealed class BoardReport : IDressingRoomSource
    {
        /// <summary>The priority of the place line, above the bare target.</summary>
        public const int Priority = 10;

        /// <inheritdoc/>
        public void Fill(DressingRoomContext context, DressingRoomReport report)
        {
            SeasonState season = context.Season;
            if (!season.Goal.HasValue || season.Playoffs.HasValue)
            {
                return;
            }

            string target = BoardExpectation.Describe(season);
            if (!season.PlayedBy(season.UserClub).Any())
            {
                report.AddBoard(new DressingRoomEntry(Text.Format(K.BoardTarget, target)));
                return;
            }

            BoardStanding standing = BoardExpectation.Standing(season);
            report.AddBoard(
                new DressingRoomEntry(
                    Text.Format(
                        K.BoardTrack,
                        target,
                        BoardExpectation.Place(season),
                        season.Clubs.Count,
                        Text.Get(Words(standing))
                    ),
                    Priority,
                    Highlight: standing is BoardStanding.UnderPressure or BoardStanding.HotSeat
                )
            );
        }

        /// <summary>The words for a standing.</summary>
        public static string Words(BoardStanding standing)
        {
            return standing switch
            {
                BoardStanding.Ahead => K.BoardAhead,
                BoardStanding.OnTrack => K.BoardOnTrack,
                BoardStanding.UnderPressure => K.BoardPressure,
                BoardStanding.HotSeat => K.BoardHotSeat,
                _ => throw new NotSupportedException("no words for board standing " + standing),
            };
        }
    }
}
