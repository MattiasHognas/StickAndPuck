namespace StickAndPuck.App.AI.SpecialTeams
{
    /// <summary>The shape a club plays on a power play or a penalty kill.</summary>
    public enum Formation
    {
        /// <summary>Even strength, or the puck out of the zone: positioning's shape.</summary>
        None,

        /// <summary>Power play: a quarterback at the point, two shooters on the half walls, one at the net and one low.</summary>
        Umbrella,

        /// <summary>Power play: one at the point, three across the middle (two half walls and the bumper in the slot), one at the net.</summary>
        OneThreeOne,

        /// <summary>Penalty kill: two low beside the net, two high at the tops of the circles (a triangle with three).</summary>
        Box,

        /// <summary>Penalty kill: one high, one each side, one low in front of the net; it covers the slot and the bumper.</summary>
        Diamond,
    }
}
