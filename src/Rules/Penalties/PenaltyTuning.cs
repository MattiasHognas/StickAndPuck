namespace StickAndPuck.Rules;

/// <summary>How often the referee calls penalties and how long they last.</summary>
public static class PenaltyTuning
{
    /// <summary>A minor penalty in game seconds at full 20 minute periods; shorter periods shorten it in proportion.</summary>
    public const float MinorSeconds = 2 * 60;

    /// <summary>The period length <see cref="MinorSeconds"/> belongs to.</summary>
    public const float FullPeriodSeconds = 20 * 60;

    /// <summary>Most penalties a club serves at once; a further call waits in the box (W46) or, without stacking, is waved off.</summary>
    public const int MaxServing = 2;

    /// <summary>W23: a club never plays fewer skaters than this, whatever it serves (3 on 3 overtime stays 3 a side).</summary>
    public const int MinSkaters = 3;

    /// <summary>W23: the most skaters a power play adds up to, at 5 on 3 in overtime.</summary>
    public const int MaxSkaters = 5;

    /// <summary>A knockdown of a skater without the puck, or from behind: tripping.</summary>
    public const float TripChance = 0.1f;

    /// <summary>A hit from behind on the puck carrier that leaves him standing: hooking.</summary>
    public const float HookCheckChance = 0.15f;

    /// <summary>A hard hit (<see cref="Players.CheckingModel.HardHit"/> and up) on a skater without the puck: slashing.</summary>
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

    /// <summary>W47: a major in game seconds at full 20 minute periods.</summary>
    public const float MajorSeconds = 5 * 60;

    /// <summary>W47: a misconduct in game seconds at full 20 minute periods.</summary>
    public const float MisconductSeconds = 10 * 60;

    /// <summary>W47: a hit at least this strong makes boarding, cross-checking or checking from behind a major with a game misconduct.</summary>
    public const float MajorHit = 2.0f;

    /// <summary>W47: a target this close to the boards is hit into them.</summary>
    public const float BoardsReach = 1.6f;

    /// <summary>W47: a hard hit from behind near the boards: boarding.</summary>
    public const float BoardingChance = 0.35f;

    /// <summary>W47: a hard hit from behind in open ice: checking from behind.</summary>
    public const float FromBehindChance = 0.3f;

    /// <summary>W47: a hard hit from the front on the carrier: cross-checking.</summary>
    public const float CrossCheckChance = 0.08f;

    /// <summary>W47: a hit on a skater this far from the puck, who did not just play it, is interference.</summary>
    public const float InterferenceReach = 4f;

    /// <summary>W47: the chance of an interference call on such a hit.</summary>
    public const float InterferenceChance = 0.06f;

    /// <summary>W47: a face-to-face check that is otherwise clean: the stick up, high-sticking.</summary>
    public const float HighStickChance = 0.015f;

    /// <summary>W47: the chance a high stick draws blood, a double minor.</summary>
    public const float DoubleMinorChance = 0.3f;

    /// <summary>W47: the same checker hitting the same skater again within this many seconds is roughing.</summary>
    public const float RoughingWindow = 3f;

    /// <summary>W47: the chance of a roughing call on a second hit.</summary>
    public const float RoughingChance = 0.25f;

    /// <summary>W47: a hit this many seconds after play stopped is after the whistle.</summary>
    public const float AfterWhistle = 1.5f;

    /// <summary>W47: the chance of a roughing call on a hit after the whistle.</summary>
    public const float LateHitChance = 0.6f;

    /// <summary>W47: the chance roughing also earns a misconduct.</summary>
    public const float RoughingMisconductChance = 0.2f;

    /// <summary>W47: a defender this close behind a slowed carrier is holding him.</summary>
    public const float HoldReach = 0.9f;

    /// <summary>W47: a carrier this slow or slower, but moving, is being held back.</summary>
    public const float HoldMaxSpeed = 3f;

    /// <summary>W47: holding calls per second while a defender hangs on to a slowed carrier.</summary>
    public const float HoldRate = 0.004f;
}
