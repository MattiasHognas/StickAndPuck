using StickAndPuck.App.Core;

namespace StickAndPuck.App.Pregame
{
    /// <summary>The warm-up's own state for one world.</summary>
    internal sealed class WarmupState
    {
        /// <summary>Gets or sets the explicit choice from <see cref="PregameFlow.Enable"/>, or None to decide from the human slots.</summary>
        public Maybe<bool> Forced { get; set; }

        /// <summary>Gets or sets a value indicating whether the warm-up has begun in this world's intro.</summary>
        public bool Running { get; set; }

        /// <summary>Gets or sets a value indicating whether start was pressed.</summary>
        public bool Started { get; set; }

        /// <summary>Gets or sets the seconds since the warm-up began.</summary>
        public float Elapsed { get; set; }

        /// <summary>Gets or sets a value indicating whether every start button has been seen released, so a press held over from a menu does not count.</summary>
        public bool Armed { get; set; }

        /// <summary>Gets the route of each skater, by player id.</summary>
        public Dictionary<int, WarmupRoute> Routes { get; } = [];
    }
}
