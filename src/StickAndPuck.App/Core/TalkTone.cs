namespace StickAndPuck.App.Core
{
    /// <summary>The tone of the coach's pre-game talk (W125): what the human picks before a season game.
    /// Each changes how the club plays that match (<see cref="CoachTalk"/>).</summary>
    public enum TalkTone
    {
        /// <summary>Keep calm: steadier passing and fewer penalties.</summary>
        Calm,

        /// <summary>Fire them up: a momentum head start and more hits, but more penalties.</summary>
        FireUp,

        /// <summary>Demand discipline: far fewer penalties, slightly less aggression.</summary>
        Discipline,
    }
}
