using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match
{
    /// <summary>The time-outs of one match: who has used theirs, and whose is running.</summary>
    internal sealed class TimeOutState
    {
        /// <summary>Gets whether each club (by <see cref="Side"/>) has used its time-out.</summary>
        public bool[] Used { get; } = new bool[2];

        /// <summary>Gets or sets the club whose time-out is running, or None.</summary>
        public Maybe<Side> Running { get; set; }
    }
}
