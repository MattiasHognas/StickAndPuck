namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>The coach's lines in the pre-game talk, filled by every <see cref="IPreGameTalkSource"/>;
    /// the screen shows the highest priorities first (equal priorities keep the order they were said in)
    /// as far as there is room.</summary>
    public sealed class PreGameTalkLines
    {
        private readonly List<DressingRoomEntry> lines = [];

        /// <summary>Gets the lines, highest priority first.</summary>
        public IReadOnlyList<DressingRoomEntry> All =>
            [.. lines.OrderByDescending(l => l.Priority)];

        /// <summary>Adds a line.</summary>
        public void Say(DressingRoomEntry line)
        {
            lines.Add(line);
        }
    }
}
