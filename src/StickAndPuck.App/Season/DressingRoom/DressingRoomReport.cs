using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>What the dressing room screen shows, filled by every <see cref="IDressingRoomSource"/> in turn:
    /// one coach line (the highest priority said wins, so a later, sharper line can replace the placeholder),
    /// then the news, injury and board lists. A list left empty shows its empty-state line; the board
    /// section only shows when something is in it.</summary>
    public sealed class DressingRoomReport
    {
        private readonly List<DressingRoomEntry> news = [];
        private readonly List<DressingRoomEntry> injuries = [];
        private readonly List<DressingRoomEntry> board = [];

        /// <summary>Gets the coach's line, or None when no source said anything.</summary>
        public Maybe<DressingRoomEntry> Coach { get; private set; }

        /// <summary>Gets the news, highest priority first.</summary>
        public IReadOnlyList<DressingRoomEntry> News => Sorted(news);

        /// <summary>Gets the injured players, highest priority first.</summary>
        public IReadOnlyList<DressingRoomEntry> Injuries => Sorted(injuries);

        /// <summary>Gets the board's notes (expectations, the hot seat), highest priority first.</summary>
        public IReadOnlyList<DressingRoomEntry> Board => Sorted(board);

        /// <summary>Offers a coach line; it is kept when nothing said so far has a higher priority (ties go to the later line).</summary>
        public void Say(DressingRoomEntry line)
        {
            if (!Coach.Is(c => c.Priority > line.Priority))
            {
                Coach = line;
            }
        }

        /// <summary>Adds a news item.</summary>
        public void AddNews(DressingRoomEntry entry)
        {
            news.Add(entry);
        }

        /// <summary>Adds an injury.</summary>
        public void AddInjury(DressingRoomEntry entry)
        {
            injuries.Add(entry);
        }

        /// <summary>Adds a board note.</summary>
        public void AddBoard(DressingRoomEntry entry)
        {
            board.Add(entry);
        }

        private static List<DressingRoomEntry> Sorted(List<DressingRoomEntry> entries)
        {
            return [.. entries.OrderByDescending(e => e.Priority)]; // stable: equal priorities keep their order
        }
    }
}
