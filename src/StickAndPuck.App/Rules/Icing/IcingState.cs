using System.Numerics;

namespace StickAndPuck.App.Rules.Icing
{
    /// <summary>Icing's own state for one world.</summary>
    internal sealed class IcingState
    {
        /// <summary>Gets or sets the last player who played the puck, or -1.</summary>
        public int Touch { get; set; } = -1;

        /// <summary>Gets or sets where that player last played it.</summary>
        public Vector3 From { get; set; }

        /// <summary>Gets or sets a value indicating whether the puck was past a goal line last step.</summary>
        public bool Over { get; set; }

        /// <summary>Gets or sets the player whose touch icing waits on the next touch, or -1.</summary>
        public int Iced { get; set; } = -1;

        /// <summary>Gets or sets where that player sent the puck from.</summary>
        public Vector3 IcedFrom { get; set; }

        /// <summary>Gets, per side, whether the club iced the puck and keeps its skaters until the drop.</summary>
        public bool[] Held { get; } = new bool[2];
    }
}
