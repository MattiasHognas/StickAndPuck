using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match
{
    /// <summary>The draw of the current faceoff.</summary>
    internal sealed class DrawState
    {
        /// <summary>Gets or sets a value indicating whether the puck has dropped and the window is open (false again once the draw is won).</summary>
        public bool Dropped { get; set; }

        /// <summary>Gets or sets the seconds since the drop.</summary>
        public float Since { get; set; }

        /// <summary>Gets or sets the club whose centre pressed before the drop, if any.</summary>
        public SideOrNone FalseStart { get; set; }

        /// <summary>Gets or sets the club that won the last draw.</summary>
        public SideOrNone Winner { get; set; }

        /// <summary>Gets each club's AI reaction after the drop, by side; infinite for a human centre.</summary>
        public float[] Delay { get; } = [float.PositiveInfinity, float.PositiveInfinity];

        /// <summary>Gets whether each human slot held shoot or pass last step, so only a fresh press counts.</summary>
        public bool[] Down { get; } = new bool[MatchSetup.Slots];
    }
}
