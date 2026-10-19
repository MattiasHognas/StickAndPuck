namespace StickAndPuck.App.Core
{
    /// <summary>How much hotter a game between rivals is (<see cref="RuleSet.RivalryStrength"/>), when <see cref="RuleSet.Rivalries"/> is on.</summary>
    public enum RivalryStrength
    {
        /// <summary>Half the extra checking, fights and morale swing.</summary>
        Mild,

        /// <summary>Default.</summary>
        Normal,

        /// <summary>Half again the extra checking, fights and morale swing.</summary>
        Strong,
    }
}
