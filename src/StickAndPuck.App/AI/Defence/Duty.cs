namespace StickAndPuck.App.AI.Defence
{
    /// <summary>What the defence has a skater doing this step.</summary>
    public enum Duty
    {
        /// <summary>Not steered by the defence.</summary>
        None,

        /// <summary>On the carrier, goal side, not hitting.</summary>
        Contain,

        /// <summary>On the carrier and allowed to finish a check: positioning's pressure job.</summary>
        Engage,

        /// <summary>Marking a man and his passing lane.</summary>
        Mark,

        /// <summary>The defenceman staying home in front of the net.</summary>
        Home,
    }
}
