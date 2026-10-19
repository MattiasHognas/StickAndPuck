namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>One part of what the coach says in the pre-game talk (W125) before the human picks a tone:
    /// the opening line about tonight's opponent (<see cref="TalkOpening"/>), a scouting tip (W141). Each
    /// source reads the dressing room context and adds lines; add a new one to
    /// <see cref="PreGameTalkSources.All"/>. A source checks its own rule switch on
    /// <see cref="DressingRoomContext.Rules"/> and adds nothing when it is off.</summary>
    public interface IPreGameTalkSource
    {
        /// <summary>Adds this source's lines.</summary>
        void Fill(DressingRoomContext context, PreGameTalkLines lines);
    }
}
