namespace StickAndPuck.App.Core
{
    /// <summary>How fast season players develop and decline (<see cref="RuleSet.DevelopmentStrength"/>).</summary>
    public enum DevelopmentStrength
    {
        /// <summary>Half as fast: a regular teenager gains about two points in an SHL season.</summary>
        Mild,

        /// <summary>Default: a regular teenager gains about four points in an SHL season.</summary>
        Normal,

        /// <summary>Half again as fast, still capped at +5 and -4.</summary>
        Strong,
    }
}
