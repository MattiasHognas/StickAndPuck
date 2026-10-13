using StickAndPuck.App.Rules;

namespace StickAndPuck.App.Season;

/// <summary>
/// The rules of one playoff mode (W19): what a played or simulated playoff game follows that a
/// regular season game does not. <see cref="Shl"/> is the SHL's; another league makes its own
/// with a record expression and puts it on <see cref="PlayoffSession.Rules"/>.
/// </summary>
public sealed record PlayoffRules
{
    /// <summary>SHL playoffs: 5 on 5 sudden death until a goal, no shootout.</summary>
    public static readonly PlayoffRules Shl = new() { Name = "SHL", Overtime = OvertimeFormat.ShlPlayoffs };

    /// <summary>Gets the name of the league or mode.</summary>
    public required string Name { get; init; }

    /// <summary>Gets how a tied playoff game is settled; played games get it on their <see cref="Core.MatchSetup.OvertimeFormat"/> and simulated ones follow it too.</summary>
    public required OvertimeFormat Overtime { get; init; }
}
