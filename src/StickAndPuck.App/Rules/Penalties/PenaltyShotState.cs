using StickAndPuck.App.Rules.Overtime;

namespace StickAndPuck.App.Rules.Penalties
{
    /// <summary>One penalty shot under way (<see cref="PenaltyShot"/>).</summary>
    internal sealed class PenaltyShotState
    {
        /// <summary>Gets the fouled skater, who takes the shot.</summary>
        public required int ShooterId { get; init; }

        /// <summary>Gets the skater who fouled him.</summary>
        public required int OffenderId { get; init; }

        public PenaltyShotStage Stage { get; set; }

        /// <summary>Gets or sets the real seconds left in the current stage.</summary>
        public float Timer { get; set; }

        /// <summary>Gets the shot itself, the shootout's attempt.</summary>
        public ShootoutAttempt Attempt { get; } = new();

        /// <summary>Gets the skaters sent off the ice for the shot, with the human slot each had, to bring back after it.</summary>
        public List<(int Id, int Slot)> Benched { get; } = [];

        /// <summary>Gets or sets the shooter's own slot before a human of his club took him over for the shot.</summary>
        public int ShooterSlot { get; set; } = -1;
    }
}
