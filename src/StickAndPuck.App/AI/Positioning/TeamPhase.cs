namespace StickAndPuck.App.AI.Positioning
{
    /// <summary>Whether a side is attacking (it has the puck, or touched it last) or defending.</summary>
    public enum TeamPhase
    {
        /// <summary>Our puck: spread out, support the carrier, stay onside.</summary>
        Attack,

        /// <summary>Their puck: get goal side, protect the slot, cover the points.</summary>
        Defend,
    }
}
