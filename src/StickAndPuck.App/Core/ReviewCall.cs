namespace StickAndPuck.App.Core
{
    /// <summary>What takes a goal off the board after a review or a coach's challenge.</summary>
    public enum ReviewCall
    {
        /// <summary>An attacker got in the goalie's way in or around his crease as the goal went in.</summary>
        GoalieInterference,

        /// <summary>The attacking club was offside when it took the puck into the zone, and the linesman missed it.</summary>
        Offside,
    }
}
