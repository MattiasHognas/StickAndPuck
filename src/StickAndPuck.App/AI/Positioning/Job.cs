namespace StickAndPuck.App.AI.Positioning
{
    /// <summary>What an AI skater is doing this step, which decides where it skates.</summary>
    public enum Job
    {
        /// <summary>Play is stopped: glide to a halt.</summary>
        Idle,

        /// <summary>Holding the role's spot in the team shape.</summary>
        Shape,

        /// <summary>Off the puck with a teammate carrying it in attack: getting open for a pass around the
        /// shape spot or the cycle station, so on purpose away from the shape's ideal spot.</summary>
        Support,

        /// <summary>Lining up for a faceoff around the puck.</summary>
        Faceoff,

        /// <summary>Carrying the puck up ice (until decisions take over).</summary>
        Carry,

        /// <summary>Going for a loose puck: the team's quickest skater to it.</summary>
        Chase,

        /// <summary>Closing down the opposing carrier from the net side.</summary>
        Pressure,

        /// <summary>Skating to a spot asked for with <see cref="PositioningModel.Request"/>.</summary>
        Requested,

        /// <summary>A human controls this skater; it keeps its role but positioning leaves it alone.</summary>
        Human,
    }
}
