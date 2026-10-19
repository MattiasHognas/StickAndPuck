namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>Every <see cref="IPreGameTalkSource"/>, in the order they speak. Later tasks add theirs here
    /// (one line each): the scouting tip (W141) gives a line with a higher priority than the opening's 0 to
    /// put it first, or the same priority to follow it.</summary>
    public static class PreGameTalkSources
    {
        /// <summary>Gets the sources.</summary>
        public static IReadOnlyList<IPreGameTalkSource> All { get; } =
        [new TalkOpening(), new ScoutingTip()];

        /// <summary>Gets what the coach says before a game.</summary>
        public static PreGameTalkLines Build(DressingRoomContext context)
        {
            PreGameTalkLines lines = new();
            foreach (IPreGameTalkSource source in All)
            {
                source.Fill(context, lines);
            }

            return lines;
        }
    }
}
