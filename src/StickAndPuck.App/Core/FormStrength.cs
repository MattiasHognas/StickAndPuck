namespace StickAndPuck.App.Core
{
    /// <summary>How hard a season player's carry-over form pushes their traits (<see cref="RuleSet.FormStrength"/>).</summary>
    public enum FormStrength
    {
        /// <summary>Half the nudge: at most about 3 trait points either way.</summary>
        Mild,

        /// <summary>Default: up to 5 trait points either way.</summary>
        Normal,

        /// <summary>Half again as much, still capped at 5 points.</summary>
        Strong,
    }
}
