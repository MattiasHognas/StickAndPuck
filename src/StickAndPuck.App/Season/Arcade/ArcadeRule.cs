namespace StickAndPuck.App.Season.Arcade
{
    /// <summary>How many losses an arcade run survives.</summary>
    public enum ArcadeRule
    {
        /// <summary>Sudden death: the first loss ends the run.</summary>
        SuddenDeath,

        /// <summary>Three lives: the third loss ends the run.</summary>
        ThreeLives,
    }
}
