namespace StickAndPuck.App.Rules.Overtime
{
    /// <summary>Where the shootout is.</summary>
    internal enum ShootoutStage
    {
        /// <summary>The break before the first shooter.</summary>
        Break,

        /// <summary>The shooter waits at centre.</summary>
        Ready,

        Attempt,

        /// <summary>The pause after an attempt.</summary>
        Result,

        /// <summary>Decided.</summary>
        Done,
    }
}
