using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Overtime
{
    /// <summary>Overtime and shootout state for one world.</summary>
    internal sealed class OvertimeState
    {
        public bool InBreak { get; set; }

        public float Timer { get; set; }

        public Maybe<ShootoutState> Shootout { get; set; }

        /// <summary>Gets the shootout under way; only asked once it has begun.</summary>
        public ShootoutState Live =>
            Shootout.TryGet(out ShootoutState so)
                ? so
                : throw new InvalidOperationException("no shootout under way");

        /// <summary>Gets or sets the club that shoots first, or None while the home human chooses.</summary>
        public SideOrNone First { get; set; }

        public ShootoutStage Stage { get; set; }

        /// <summary>Gets each club's shooting order, indexed by <see cref="Side"/>.</summary>
        public List<Player>[] Order { get; } =
        [
            [],
            [],
        ];

        /// <summary>Gets each club's next place in its order.</summary>
        public int[] Next { get; } = new int[2];

        /// <summary>Gets the attempt under way: the shooter's lane, his distance and his deke.</summary>
        public ShootoutAttempt Attempt { get; } = new();
    }
}
