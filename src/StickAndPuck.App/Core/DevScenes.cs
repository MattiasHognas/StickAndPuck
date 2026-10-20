namespace StickAndPuck.App.Core
{
    /// <summary>The state of the dev scenes' loops, which were statics. A loop runs while its slot is set;
    /// a normal match never sets one.</summary>
    public sealed class DevScenes
    {
        /// <summary>Gets the goalie scene's shot loop and tally.</summary>
        internal Slot<Goalie.Dev.GoalieDemo> Goalie { get; } = new();

        /// <summary>Gets the positioning scenes' stand-in for the rest of a match.</summary>
        internal Slot<AI.Positioning.Dev.StandInPlay> StandIn { get; } = new();

        /// <summary>Gets the shot scene's loop.</summary>
        internal Slot<Players.Puck.Dev.ShotDemo> Shot { get; } = new();

        /// <summary>Gets the puck handling scene's loop.</summary>
        internal Slot<Players.Puck.Dev.HandlingDemo> Handling { get; } = new();

        /// <summary>Gets the checks scene's loop.</summary>
        internal Slot<Players.Checks.Dev.ChecksDemo> Checks { get; } = new();

        /// <summary>Gets the match demo's shot loop and camera.</summary>
        internal Slot<Rules.Match.Dev.MatchDemo> Match { get; } = new();

        /// <summary>Gets the puck scene's shot loop.</summary>
        internal Slot<Physics.Dev.PuckDemo> Puck { get; } = new();

        /// <summary>Gets the line calls scene's plays.</summary>
        internal Slot<Rules.Offside.Dev.LineCallsDemo> LineCalls { get; } = new();

        /// <summary>Gets the too many men scene's scripted rushed change.</summary>
        internal Slot<Lines.Dev.TooManyMenDemo> TooManyMen { get; } = new();

        /// <summary>Gets the penalty shot scene's scripted breakaway trip.</summary>
        internal Slot<Rules.Penalties.Dev.PenaltyShotDemo> PenaltyShot { get; } = new();

        /// <summary>Gets the trapezoid scene's script.</summary>
        internal Slot<Rules.Penalties.Dev.TrapezoidDemo> Trapezoid { get; } = new();

        /// <summary>Gets the goal review scene's script.</summary>
        internal Slot<Rules.Match.Dev.GoalReviewDemo> GoalReview { get; } = new();
    }
}
