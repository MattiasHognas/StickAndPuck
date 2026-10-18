using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Fights
{
    /// <summary>A fight on the ice.</summary>
    /// <remarks>Initializes a new instance of the <see cref="Fight"/> class.</remarks>
    public sealed class Fight(Fighter checker, Fighter victim, Vector3 spot)
    {
        /// <summary>Gets the skater who threw the hit.</summary>
        public Fighter Checker { get; } = checker;

        /// <summary>Gets the skater who took it.</summary>
        public Fighter Victim { get; } = victim;

        /// <summary>Gets both fighters, checker first.</summary>
        public IEnumerable<Fighter> Both => [Checker, Victim];

        /// <summary>Gets the point between them.</summary>
        public Vector3 Spot { get; } = spot;

        /// <summary>Gets or sets the stage.</summary>
        public FightStage Stage { get; set; } = FightStage.Grab;

        /// <summary>Gets or sets the seconds since the stage (or exchange) started.</summary>
        public float StageTime { get; set; }

        /// <summary>Gets or sets when a punch last landed (<see cref="World.Time"/>), NaN before the first; the close-up camera jolts on it.</summary>
        public float LandedAt { get; set; } = float.NaN;

        /// <summary>Gets or sets the exchanges finished.</summary>
        public int Exchange { get; set; }

        /// <summary>Gets where the timing marker is in this exchange, 0 to 1.</summary>
        public float Sweep =>
            Stage == FightStage.Exchange
                ? Math.Clamp(StageTime / FightTuning.ExchangeSeconds, 0f, 1f)
                : 0f;

        /// <summary>Gets or sets the loser once the fight is decided, or -1.</summary>
        public int LoserId { get; set; } = -1;

        /// <summary>Gets or sets a value indicating whether the loser went down (otherwise the linesmen broke it up).</summary>
        public bool Knockdown { get; set; }

        /// <summary>Gets the other fighter.</summary>
        public Fighter Other(Fighter f)
        {
            return f == Checker ? Victim : Checker;
        }

        /// <summary>Gets a skater's side of the fight, or None if he is not in it.</summary>
        public Maybe<Fighter> Of(int playerId)
        {
            return (Checker.PlayerId == playerId)
                ? ((Maybe<Fighter>)Checker)
                : (
                    (Victim.PlayerId == playerId) ? ((Maybe<Fighter>)Victim) : Maybe.None<Fighter>()
                );
        }
    }
}
