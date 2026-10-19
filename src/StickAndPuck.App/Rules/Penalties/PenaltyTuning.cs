namespace StickAndPuck.App.Rules.Penalties
{
    /// <summary>How often the referee calls penalties and how long they last.</summary>
    public static class PenaltyTuning
    {
        /// <summary>A breakaway: an opposing skater this far (metres) or less nearer the goal than the carrier is level
        /// with him, not between him and the goal.</summary>
        public const float BreakawayLevel = 0.5f;

        /// <summary>A carrier tripped off the puck still had it when it lies this close to him (metres).</summary>
        public const float BreakawayPuckReach = 2.5f;

        /// <summary>The whistle for a penalty shot: play stands still this long before the ice is cleared for the shot.</summary>
        public const float ShotWhistleSeconds = 2f;

        /// <summary>The pause after a missed penalty shot before the skaters come back for the faceoff at centre.</summary>
        public const float ShotResultSeconds = 1.5f;

        /// <summary>A minor penalty in game seconds at full 20 minute periods; shorter periods shorten it in proportion.</summary>
        public const float MinorSeconds = 2 * 60;

        /// <summary>The period length <see cref="MinorSeconds"/> belongs to.</summary>
        public const float FullPeriodSeconds = 20 * 60;

        /// <summary>Most penalties a club serves at once; a further call waits in the box or, without stacking, is waved off.</summary>
        public const int MaxServing = 2;

        /// <summary>A club never plays fewer skaters than this, whatever it serves (3 on 3 overtime stays 3 a side).</summary>
        public const int MinSkaters = 3;

        /// <summary>The most skaters a power play adds up to, at 5 on 3 in overtime.</summary>
        public const int MaxSkaters = 5;

        /// <summary>A knockdown of a skater without the puck, or from behind: tripping.</summary>
        public const float TripChance = 0.1f;

        /// <summary>A hit from behind on the puck carrier that leaves him standing: hooking.</summary>
        public const float HookCheckChance = 0.15f;

        /// <summary>A hard hit (<see cref="Players.Checks.CheckingModel.HardHit"/> and up) on a skater without the puck: slashing.</summary>
        public const float SlashChance = 0.15f;

        /// <summary>Any other check: a small chance the stick came up, slashing.</summary>
        public const float StickChance = 0.015f;

        /// <summary>A hit counts as from behind when it lands within this many degrees of the target's back.</summary>
        public const float BehindDegrees = 55f;

        /// <summary>A defender this close behind a carrier can hook him.</summary>
        public const float HookReach = 1.3f;

        /// <summary>A carrier slower than this is not being chased, so no hooking.</summary>
        public const float HookMinSpeed = 3.5f;

        /// <summary>Hooking calls per second while a defender trails a carrier inside <see cref="HookReach"/>.</summary>
        public const float HookRate = 0.003f;

        /// <summary>How far the box door is from the near boards, on the ice.</summary>
        public const float BoxDoorInset = 0.9f;

        /// <summary>The x of each club's penalty box door: home left of centre, away right.</summary>
        public const float BoxDoorX = 2.5f;

        /// <summary>A major in game seconds at full 20 minute periods.</summary>
        public const float MajorSeconds = 5 * 60;

        /// <summary>A misconduct in game seconds at full 20 minute periods.</summary>
        public const float MisconductSeconds = 10 * 60;

        /// <summary>A hit at least this strong makes boarding, cross-checking or checking from behind a major with a game misconduct.</summary>
        public const float MajorHit = 2.0f;

        /// <summary>A target this close to the boards is hit into them.</summary>
        public const float BoardsReach = 1.6f;

        /// <summary>A hard hit from behind near the boards: boarding.</summary>
        public const float BoardingChance = 0.35f;

        /// <summary>A hard hit from behind in open ice: checking from behind.</summary>
        public const float FromBehindChance = 0.3f;

        /// <summary>A hard hit from the front on the carrier: cross-checking.</summary>
        public const float CrossCheckChance = 0.08f;

        /// <summary>A hit on a skater this far from the puck, who did not just play it, is interference.</summary>
        public const float InterferenceReach = 4f;

        /// <summary>The chance of an interference call on such a hit.</summary>
        public const float InterferenceChance = 0.06f;

        /// <summary>A face-to-face check that is otherwise clean: the stick up, high-sticking.</summary>
        public const float HighStickChance = 0.015f;

        /// <summary>The chance a high stick draws blood, a double minor.</summary>
        public const float DoubleMinorChance = 0.3f;

        /// <summary>The same checker hitting the same skater again within this many seconds is roughing.</summary>
        public const float RoughingWindow = 3f;

        /// <summary>The chance of a roughing call on a second hit.</summary>
        public const float RoughingChance = 0.25f;

        /// <summary>A hit this many seconds after play stopped is after the whistle.</summary>
        public const float AfterWhistle = 1.5f;

        /// <summary>The chance of a roughing call on a hit after the whistle.</summary>
        public const float LateHitChance = 0.6f;

        /// <summary>The chance roughing also earns a misconduct.</summary>
        public const float RoughingMisconductChance = 0.2f;

        /// <summary>A defender this close behind a slowed carrier is holding him.</summary>
        public const float HoldReach = 0.9f;

        /// <summary>A carrier this slow or slower, but moving, is being held back.</summary>
        public const float HoldMaxSpeed = 3f;

        /// <summary>Holding calls per second while a defender hangs on to a slowed carrier.</summary>
        public const float HoldRate = 0.004f;

        /// <summary>How fast a penalised skater glides to the box (m/s): a steady cruise, not a sprint.</summary>
        public const float TripSpeed = 6.5f;

        /// <summary>How quickly he picks up speed on the way (m/s²).</summary>
        public const float TripAccel = 5f;

        /// <summary>How hard he slows for the door (m/s²), and the speed he steps through it at (m/s).</summary>
        public const float TripBrake = 4f,
            TripDoorSpeed = 1.2f;

        /// <summary>How quickly his skating line bends toward where he is going (per second).</summary>
        public const float TripTurn = 5f;

        /// <summary>How far out in front of the door he lines up before skating in (m from the near boards).</summary>
        public const float TripLineUp = 2.2f;

        /// <summary>Where he goes through the door: this far out from the near boards (m).</summary>
        public const float TripInside = 0.5f;

        /// <summary>How close to the door counts as through it (m).</summary>
        public const float TripReach = 0.35f;

        /// <summary>The longest a skate to the box takes before he is put in it (s).</summary>
        public const float TripTimeout = 10f;

        /// <summary>How long the faceoff waits after the last skater is in the box (s).</summary>
        public const float TripAfter = 0.5f;
    }
}
