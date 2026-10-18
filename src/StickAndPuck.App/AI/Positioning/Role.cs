namespace StickAndPuck.App.AI.Positioning
{
    /// <summary>Where a skater plays in the team's shape. Left and right are as seen skating toward the attacked net.</summary>
    public enum Role
    {
        /// <summary>Centre: the middle lane, high slot in attack, low slot in defence.</summary>
        Centre,

        /// <summary>Left wing: the left lane, covers the left point in defence.</summary>
        LeftWing,

        /// <summary>Right wing: the right lane, covers the right point in defence.</summary>
        RightWing,

        /// <summary>Left defence: the left point in attack, the left side of the net in defence.</summary>
        LeftDefence,

        /// <summary>Right defence: the right point in attack, the right side of the net in defence.</summary>
        RightDefence,
    }
}
