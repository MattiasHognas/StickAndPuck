using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Penalties
{
    /// <summary>A penalised skater's skate to the box door: where he is and how fast he goes, kept here so
    /// the AI and skating steps that still move him on the ice are put right each step.</summary>
    internal sealed class BoxTrip(
        int playerId,
        System.Numerics.Vector3 position,
        System.Numerics.Vector3 velocity,
        float started
    )
    {
        public int PlayerId { get; } = playerId;

        public System.Numerics.Vector3 Position { get; set; } = position;

        public System.Numerics.Vector3 Velocity { get; set; } = velocity;

        /// <summary>Gets when the trip started, in <see cref="World.Time"/>.</summary>
        public float Started { get; } = started;

        /// <summary>Gets or sets a value indicating whether he has lined up in front of the door.</summary>
        public bool LinedUp { get; set; }
    }
}
