namespace StickAndPuck.App.Core
{
    /// <summary>How much the dressing room screen between season games shows (the Milestone 10 season story).</summary>
    public enum DressingRoomStyle
    {
        /// <summary>The dressing room shows everything: the coach, the last game, news, injuries and the board.</summary>
        Full,

        /// <summary>The dressing room shows only the coach, the last game and the next one.</summary>
        Brief,

        /// <summary>No dressing room: a season result goes straight to the table, as before.</summary>
        Off,
    }
}
