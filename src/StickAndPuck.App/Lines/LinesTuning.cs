using System.Collections.Immutable;

namespace StickAndPuck.App.Lines
{
    /// <summary>How fast skaters tire and rest, and when lines and goalies change. Energy runs from 1 (fresh) to 0 (spent).</summary>
    public static class LinesTuning
    {
        /// <summary>Energy lost per second on the ice in play, whatever the skater does.</summary>
        public const float BaseDrain = 0.004f;

        /// <summary>Extra energy lost per second skating at top speed (less when slower).</summary>
        public const float SkateDrain = 0.008f;

        /// <summary>Extra energy lost per second while sprinting.</summary>
        public const float SprintDrain = 0.008f;

        /// <summary>Energy regained per second on the ice while play is stopped.</summary>
        public const float IceRest = 0.01f;

        /// <summary>Energy regained per second on the ice during a time-out: three quarters of a tank over its 30 seconds.</summary>
        public const float TimeOutRest = 0.025f;

        /// <summary>Energy regained per second on the bench.</summary>
        public const float BenchRest = 0.02f;

        /// <summary>Automatic changes on the fly start when the group on the ice is this tired, and the puck is out of their own zone.</summary>
        public const float ChangeBelow = 0.5f;

        /// <summary>Below this a tired group changes on the fly even with the puck in their own zone.</summary>
        public const float ForceBelow = 0.3f;

        /// <summary>At a faceoff, a group this tired goes off for a fresher one.</summary>
        public const float FaceoffBelow = 0.75f;

        /// <summary>A change only happens when the unit coming on is at least this much fresher.</summary>
        public const float FresherBy = 0.12f;

        /// <summary>Where a leaving skater counts as at the bench door, in metres.</summary>
        public const float DoorReach = 1.6f;

        /// <summary>A leaving skater who has not reached the door after this many seconds swaps anyway.</summary>
        public const float LeaveTimeout = 8f;

        /// <summary>A rushed change is too many men under <see cref="Core.TooManyMenCall.Lenient"/> when the skater going off is further than this from the bench door, in metres.</summary>
        public const float TooManyMenLenient = 5f;

        /// <summary>The same under <see cref="Core.TooManyMenCall.Strict"/>: the rulebook's five feet.</summary>
        public const float TooManyMenStrict = 1.5f;

        /// <summary>Goals against in a short spell that bring in the backup goalie.</summary>
        public const int GoalieGoals = 3;

        /// <summary>The spell, as a share of the period length: 0.3 of a 20 minute period is 6 game minutes.</summary>
        public const float GoalieSpell = 0.3f;

        /// <summary>An AI club trailing pulls its goalie inside the last two minutes of regulation at full 20 minute periods; shorter periods shorten it in proportion.</summary>
        public const float PullSeconds = 2 * 60;

        /// <summary>The biggest deficit a club pulls its goalie for.</summary>
        public const int PullMaxDeficit = 2;

        /// <summary>How much less likely each later unit is picked, so lines 1 to 3 play most and line 4 least.</summary>
        public static readonly ImmutableArray<float> ForwardBias = [0f, 0.03f, 0.06f, 0.15f];

        /// <summary>The same for defence pairs.</summary>
        public static readonly ImmutableArray<float> PairBias = [0f, 0.03f, 0.06f];

        /// <summary>Gets how much a skater's shots keep of their speed: 1 when fresh, 0.85 when spent.</summary>
        public static float ShotFactor(float energy)
        {
            return 0.85f + (0.15f * Math.Clamp(energy, 0f, 1f));
        }
    }
}
