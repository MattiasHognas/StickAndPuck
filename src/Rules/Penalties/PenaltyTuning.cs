namespace StickAndPuck.Rules;

/// <summary>How often the referee calls penalties and how long they last.</summary>
public static class PenaltyTuning
{
    /// <summary>A minor penalty in game seconds at full 20 minute periods; shorter periods shorten it in proportion.</summary>
    public const float MinorSeconds = 2 * 60;

    /// <summary>The period length <see cref="MinorSeconds"/> belongs to.</summary>
    public const float FullPeriodSeconds = 20 * 60;

    /// <summary>Most penalties a club serves at once; a call that would put it three short is waved off.</summary>
    public const int MaxServing = 2;

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

    /// <summary>How long the referee shows the call's signal after the whistle, in seconds.</summary>
    public const float SignalSeconds = 2.5f;

    /// <summary>How far the box door is from the near boards, on the ice.</summary>
    public const float BoxDoorInset = 0.9f;

    /// <summary>The x of each club's penalty box door: home left of centre, away right.</summary>
    public const float BoxDoorX = 2.5f;
}
