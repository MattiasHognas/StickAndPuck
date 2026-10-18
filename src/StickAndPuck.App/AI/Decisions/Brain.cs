using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.Decisions
{
    /// <summary>One AI skater's short memory.</summary>
    internal sealed class Brain
    {
        /// <summary>Gets or sets when the carrier next looks at the play.</summary>
        public float NextThink { get; set; }

        /// <summary>Gets or sets when this skater got the puck.</summary>
        public float GotAt { get; set; }

        /// <summary>Gets or sets the steps left to hold shoot; 0 when not shooting.</summary>
        public int ShootSteps { get; set; }

        /// <summary>Gets or sets the stick direction held through a shot.</summary>
        public Vector2 ShootAim { get; set; }

        /// <summary>Gets or sets the earliest time this skater thinks about another check.</summary>
        public float CheckReady { get; set; }

        /// <summary>Gets or sets the teammate this skater last passed to.</summary>
        public int PassTo { get; set; } = -1;

        /// <summary>Gets or sets a pass decided on but fumbled: released a beat late, at <see cref="PassAt"/>.</summary>
        public Maybe<PassOption> LatePass { get; set; }

        /// <summary>Gets or sets when a late pass is released.</summary>
        public float PassAt { get; set; }

        /// <summary>Gets or sets the possession (its <see cref="GotAt"/>) whose blue-line hold-up was decided, so it is rolled once.</summary>
        public float HoldRun { get; set; } = float.NaN;

        /// <summary>Gets or sets until when the carrier holds up short of the blue line; past it he carries on.</summary>
        public float HoldUntil { get; set; } = float.NegativeInfinity;

        /// <summary>Gets or sets the possession whose own-zone clear was rolled, so a carrier weighs it once per pressing.</summary>
        public float ClearRun { get; set; } = float.NaN;

        /// <summary>Gets or sets until when this skater goes for a hit on the carrier.</summary>
        public float HitUntil { get; set; } = float.NegativeInfinity;

        /// <summary>Gets or sets when this carrier last read a defender for a deke.</summary>
        public float DekeRolled { get; set; } = float.NegativeInfinity;

        /// <summary>Gets or sets the distance to the defender, in metres, at which this carrier presses deke; minus infinity for no deke.</summary>
        public float DekeAt { get; set; } = float.NegativeInfinity;

        /// <summary>Gets or sets the pass (its <see cref="InFlight.At"/>) whose one-timer was read, so it is rolled once.</summary>
        public float OneTimerRun { get; set; } = float.NaN;

        /// <summary>Gets or sets when this receiver pressed shoot to one-time the pass in flight.</summary>
        public float OneTimerPressed { get; set; } = float.NegativeInfinity;

        /// <summary>Gets or sets a value indicating whether this receiver is one-timing the pass in flight.</summary>
        public bool OneTiming { get; set; }

        /// <summary>Gets or sets the step on which special teams or tactics last claimed this carrier's next action (<see cref="AiCommand.Hold"/>).</summary>
        public long HeldTick { get; set; } = long.MinValue;
    }
}
