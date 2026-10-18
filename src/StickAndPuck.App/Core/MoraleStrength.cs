namespace StickAndPuck.App.Core
{
    /// <summary>How hard team morale swings and how much it counts, when <see cref="RuleSet.Morale"/> is on.</summary>
    public enum MoraleStrength
    {
        /// <summary>Half the swings and half the effect.</summary>
        Mild,

        /// <summary>Default.</summary>
        Normal,

        /// <summary>Half again the swings and the effect.</summary>
        Strong,
    }
}
