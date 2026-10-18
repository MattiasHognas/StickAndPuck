namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>One line on the dressing room screen: a coach line, a news item, an injury or a board note,
    /// already in the language shown. The screen shows the highest priorities first when a section is full.</summary>
    /// <param name="Text">The line as shown.</param>
    /// <param name="Priority">Higher shows first; the placeholder lines are 0.</param>
    /// <param name="Highlight">True draws the line in gold.</param>
    public sealed record DressingRoomEntry(string Text, int Priority = 0, bool Highlight = false);
}
