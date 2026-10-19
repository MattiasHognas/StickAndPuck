namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>Every <see cref="IDressingRoomSource"/>, in the order they fill the report. Later season story
    /// tasks add their source here (one line each); a coach line with a higher priority replaces the
    /// placeholder from <see cref="LastResultCoach"/>.</summary>
    public static class DressingRoomSources
    {
        /// <summary>Gets the sources.</summary>
        public static IReadOnlyList<IDressingRoomSource> All { get; } =
        [
            new LastResultCoach(),
            new PostGameReaction(),
            new SeasonNews(),
            new NewsEvents(),
            new InjuryReport(),
            new SuspensionReport(),
            new RivalryNews(),
        ];

        /// <summary>Fills a report from every source.</summary>
        public static DressingRoomReport Build(DressingRoomContext context)
        {
            DressingRoomReport report = new();
            foreach (IDressingRoomSource source in All)
            {
                source.Fill(context, report);
            }

            return report;
        }
    }
}
