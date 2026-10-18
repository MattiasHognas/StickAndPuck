namespace StickAndPuck.App.Core
{
    /// <summary>How a tied match is settled.</summary>
    public enum OvertimeRule
    {
        /// <summary>A tie stands (exhibition and tests).</summary>
        None,

        /// <summary>SHL: 3 on 3 sudden death, then a shootout.</summary>
        Shl,

        /// <summary>Straight to a shootout.</summary>
        ShootoutOnly,
    }
}
