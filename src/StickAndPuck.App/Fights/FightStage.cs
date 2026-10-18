namespace StickAndPuck.App.Fights
{
    /// <summary>Where a fight is.</summary>
    public enum FightStage
    {
        /// <summary>Gloves off, sweaters grabbed, before the first exchange.</summary>
        Grab,

        /// <summary>The timing marker sweeps; both fighters punch and block.</summary>
        Exchange,

        /// <summary>Decided: the winner celebrates before the loser goes to the box.</summary>
        Over,
    }
}
