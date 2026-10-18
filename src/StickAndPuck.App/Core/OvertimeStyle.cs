namespace StickAndPuck.App.Core
{
    /// <summary>How a tie after regulation is settled, as a rule switch (mapped onto an overtime format by <see cref="MatchSetup.WithRules"/>).</summary>
    public enum OvertimeStyle
    {
        /// <summary>One 3 on 3 sudden death period, then a shootout (SHL and NHL regular season).</summary>
        ThreeOnThree,

        /// <summary>5 on 5 sudden death in full periods until a goal, no shootout (SHL and NHL playoffs).</summary>
        SuddenDeath,

        /// <summary>Straight to a shootout.</summary>
        ShootoutOnly,

        /// <summary>The tie stands (regular season only).</summary>
        Tie,
    }
}
