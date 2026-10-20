namespace StickAndPuck.App.Rules.Penalties
{
    /// <summary>Where a penalty shot is.</summary>
    internal enum PenaltyShotStage
    {
        /// <summary>The whistle: play stands still before the ice is cleared.</summary>
        Whistle,

        /// <summary>The ice is clear and the shooter waits at centre with the puck.</summary>
        Ready,

        /// <summary>The shot is under way.</summary>
        Attempt,

        /// <summary>The pause after a miss, before the skaters come back for the faceoff.</summary>
        Result,
    }
}
