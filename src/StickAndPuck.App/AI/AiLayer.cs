namespace StickAndPuck.App.AI
{
    /// <summary>The AI layers that write a skater's command, in the order they run each step.</summary>
    public enum AiLayer
    {
        /// <summary>Nobody has written this skater's command yet.</summary>
        None,

        /// <summary>Positioning: the base move every step.</summary>
        Positioning,

        /// <summary>Defence: contain, stay home, mark.</summary>
        Defence,

        /// <summary>Special teams: power play and kill shapes, power play shots, kill clears.</summary>
        SpecialTeams,

        /// <summary>Club tactics: forecheck, trap, dump and chase.</summary>
        Tactics,

        /// <summary>Decisions: shots, passes, checks and the skating that goes with them.</summary>
        Decisions,
    }
}
