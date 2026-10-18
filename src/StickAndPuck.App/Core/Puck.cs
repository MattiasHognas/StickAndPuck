using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>The puck. Physics moves it; puck handling attaches it to a stick.</summary>
    public sealed class Puck
    {
        /// <summary>Gets or sets the centre of the puck.</summary>
        public Vector3 Position { get; set; }

        /// <summary>Gets or sets the velocity in metres per second.</summary>
        public Vector3 Velocity { get; set; }

        /// <summary>Gets or sets the id of the player carrying the puck, or -1 when it is loose.</summary>
        public int Carrier { get; set; } = -1;

        /// <summary>Gets or sets the id of the last player who touched it, or -1.</summary>
        public int LastTouch { get; set; } = -1;

        /// <summary>Gets or sets a value indicating whether the puck is live (false between whistle and faceoff).</summary>
        public bool Live { get; set; } = true;
    }
}
