using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules;

/// <summary>
/// How one mode or league settles a tie after regulation: the per-mode overtime rules. A mode
/// puts its format on <see cref="MatchSetup.OvertimeFormat"/>; a setup without one plays the
/// format its <see cref="MatchSetup.Overtime"/> rule names. Presets: <see cref="Shl"/> (regular
/// season: 3 on 3 for one period, then a shootout), <see cref="ShlPlayoffs"/> (5 on 5 sudden death,
/// 20-minute periods until a goal, no shootout), <see cref="ShootoutOnly"/> and <see cref="None"/>.
/// Another league makes its own with a record expression, like <c>Shl with { Skaters = 4 }</c>.
/// </summary>
public sealed record OvertimeFormat
{
    /// <summary>A tie stands (exhibition and tests).</summary>
    public static readonly OvertimeFormat None = new() { Name = "tie" };

    /// <summary>SHL regular season: one 3 on 3 sudden death period, then a shootout.</summary>
    public static readonly OvertimeFormat Shl = new() { Name = "SHL", Skaters = 3, HasShootout = true };

    /// <summary>SHL playoffs: 5 on 5 sudden death in full periods until somebody scores; no shootout.</summary>
    public static readonly OvertimeFormat ShlPlayoffs = new() { Name = "SHL playoffs", Skaters = 5, PeriodSeconds = 20 * 60, UntilGoal = true };

    /// <summary>Straight to a shootout.</summary>
    public static readonly OvertimeFormat ShootoutOnly = new() { Name = "shootout", HasShootout = true };

    /// <summary>Gets the name, for logs and screens.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the skaters per club in overtime; 0 plays no overtime period.</summary>
    public int Skaters { get; init; }

    /// <summary>Gets the length of one overtime period in game seconds, or None for <see cref="WorldSwitches.OvertimeSeconds"/> (the SHL 5 minutes, which scenes shorten).</summary>
    public Maybe<float> PeriodSeconds { get; init; }

    /// <summary>Gets a value indicating whether overtime periods go on until a goal (true) or stop after one (false).</summary>
    public bool UntilGoal { get; init; }

    /// <summary>Gets a value indicating whether a shootout follows the overtime (or the tie, with no overtime period).</summary>
    public bool HasShootout { get; init; }

    /// <summary>Gets the format a match plays: the setup's own, or the one its <see cref="OvertimeRule"/> names.</summary>
    /// <param name="setup">The match setup.</param>
    /// <returns>The format.</returns>
    public static OvertimeFormat Of(MatchSetup setup)
    {
        return setup.OvertimeFormat.TryGet(out var own) ? own : From(setup.Overtime);
    }

    /// <summary>Gets the preset an <see cref="OvertimeRule"/> names.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The format.</returns>
    public static OvertimeFormat From(OvertimeRule rule)
    {
        return rule switch
        {
            OvertimeRule.Shl => Shl,
            OvertimeRule.ShootoutOnly => ShootoutOnly,
            _ => None,
        };
    }

    /// <summary>Gets what follows a tie: another overtime period, the shootout, or None when the tie stands.</summary>
    /// <param name="overtimePeriodsPlayed">Overtime periods played so far, 0 right after regulation.</param>
    /// <returns>The next phase, or None for a draw.</returns>
    public Maybe<MatchPhase> AfterTie(int overtimePeriodsPlayed)
    {
        if (Skaters > 0 && (overtimePeriodsPlayed == 0 || UntilGoal)) return MatchPhase.Overtime;
        return HasShootout ? MatchPhase.Shootout : Maybe<MatchPhase>.None;
    }
}
