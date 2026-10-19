namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>One system's part of the dressing room screen: the coach's talk (W125, W127), injuries (W128),
    /// news (W130), the board (W133). Each source reads the context and adds lines to the report; add a
    /// new one to <see cref="DressingRoomSources.All"/>. A source checks its own rule switch on
    /// <see cref="DressingRoomContext.Rules"/> and adds nothing when it is off.</summary>
    public interface IDressingRoomSource
    {
        /// <summary>Adds this source's lines.</summary>
        void Fill(DressingRoomContext context, DressingRoomReport report);
    }
}
