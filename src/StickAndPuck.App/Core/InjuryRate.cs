namespace StickAndPuck.App.Core
{
    /// <summary>How often hard checks, blocked slap shots, fights and games settled on paper injure a player, when <see cref="RuleSet.Injuries"/> is on.</summary>
    public enum InjuryRate
    {
        /// <summary>Half as often.</summary>
        Rare,

        /// <summary>Default.</summary>
        Normal,

        /// <summary>Twice as often.</summary>
        Frequent,
    }
}
