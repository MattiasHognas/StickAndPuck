namespace StickAndPuck.Core;

/// <summary>
/// One rule on the rules page: its key in settings.json, its label and group, the values it takes
/// (as stored, enum names or On and Off) and how to read and change it on a <see cref="RuleSet"/>.
/// </summary>
/// <param name="Key">The key in settings.json, the <see cref="RuleSet"/> property's name.</param>
/// <param name="Label">The label on the rules page.</param>
/// <param name="Group">The group heading it sits under.</param>
/// <param name="Plays">True when the game plays the rule; a rule a later task wires stays off the page until then.</param>
/// <param name="Values">The values, as stored.</param>
/// <param name="Get">Reads the value.</param>
/// <param name="Set">Changes the value.</param>
public sealed record RuleRow(string Key, string Label, string Group, bool Plays, IReadOnlyList<string> Values, Func<RuleSet, string> Get, Func<RuleSet, string, RuleSet> Set)
{
    /// <summary>Gets a value as the rules page shows it.</summary>
    /// <param name="value">One of <see cref="Values"/>.</param>
    /// <returns>The text, in capitals.</returns>
    public static string Show(string value) => value switch
    {
        nameof(OvertimeStyle.ThreeOnThree) => "3 ON 3 + SO",
        nameof(OvertimeStyle.SuddenDeath) => "5 ON 5 SUDDEN",
        nameof(OvertimeStyle.ShootoutOnly) => "SHOOTOUT",
        nameof(ShootoutFirst.HomeChooses) => "HOME CHOOSES",
        nameof(FightPenalty.MajorAndEjection) => "5 + EJECTION",
        nameof(FightPenalty.LoserMinor) => "LOSER 2 MIN",
        nameof(FightPenalty.Major) => "5 MIN MAJOR",
        nameof(PenaltySeverity.MinorsOnly) => "MINORS ONLY",
        nameof(CoincidentalMinors.FourOnFour) => "4 ON 4",
        _ => value.ToUpperInvariant(),
    };

    /// <summary>The value one step along from the current one, wrapping.</summary>
    /// <param name="rules">The rules.</param>
    /// <param name="dir">-1 or 1.</param>
    /// <returns>The value.</returns>
    public string Next(RuleSet rules, int dir)
    {
        int i = Math.Max(0, Values.ToList().IndexOf(Get(rules)));
        return Values[(((i + dir) % Values.Count) + Values.Count) % Values.Count];
    }
}

/// <summary>
/// Every rule switch, in page order: Game (offside, icing, goalie pull), Overtime, Penalties,
/// Infractions (W47) and Fights. W45 to W49 flip their row's <see cref="RuleRow.Plays"/> when they wire the rule.
/// </summary>
public static class RuleRows
{
    const string On = "On", Off = "Off";

    /// <summary>Gets every rule, shown or not.</summary>
    public static IReadOnlyList<RuleRow> All { get; } =
    [
        Flag(nameof(RuleSet.Offside), "OFFSIDE", "GAME", true, r => r.Offside, (r, v) => r with { Offside = v }),
        Choice(nameof(RuleSet.Icing), "ICING", "GAME", true, Enum.GetValues<IcingCall>(), r => r.Icing, (r, v) => r with { Icing = v }),
        Flag(nameof(RuleSet.GoaliePull), "GOALIE PULL", "GAME", true, r => r.GoaliePull, (r, v) => r with { GoaliePull = v }),
        Choice(nameof(RuleSet.Overtime), "OVERTIME", "OVERTIME", true, Enum.GetValues<OvertimeStyle>(), r => r.Overtime, (r, v) => r with { Overtime = v }),
        Choice(nameof(RuleSet.PlayoffOvertime), "PLAYOFF OVERTIME", "OVERTIME", true, [OvertimeStyle.SuddenDeath, OvertimeStyle.ThreeOnThree, OvertimeStyle.ShootoutOnly], r => r.PlayoffOvertime, (r, v) => r with { PlayoffOvertime = v }),
        new(nameof(RuleSet.ShootoutShooters), "SHOOTOUT SHOOTERS", "OVERTIME", true, ["3", "5"], r => r.ShootoutShooters.ToString(System.Globalization.CultureInfo.InvariantCulture), (r, v) => r with { ShootoutShooters = int.Parse(v, System.Globalization.CultureInfo.InvariantCulture) }),
        Choice(nameof(RuleSet.ShootoutFirst), "SHOOTS FIRST", "OVERTIME", true, Enum.GetValues<ShootoutFirst>(), r => r.ShootoutFirst, (r, v) => r with { ShootoutFirst = v }),
        Flag(nameof(RuleSet.Penalties), "PENALTIES", "PENALTIES", true, r => r.Penalties, (r, v) => r with { Penalties = v }),
        Choice(nameof(RuleSet.Strictness), "REFEREE", "PENALTIES", true, Enum.GetValues<RefereeStrictness>(), r => r.Strictness, (r, v) => r with { Strictness = v }),
        Flag(nameof(RuleSet.PenaltiesCarryIntoOvertime), "CARRY INTO OT", "PENALTIES", true, r => r.PenaltiesCarryIntoOvertime, (r, v) => r with { PenaltiesCarryIntoOvertime = v }),
        Flag(nameof(RuleSet.StackPenalties), "THIRD PENALTY WAITS", "PENALTIES", true, r => r.StackPenalties, (r, v) => r with { StackPenalties = v }),
        Choice(nameof(RuleSet.CoincidentalMinors), "COINCIDENTAL MINORS", "PENALTIES", true, Enum.GetValues<CoincidentalMinors>(), r => r.CoincidentalMinors, (r, v) => r with { CoincidentalMinors = v }),
        Choice(nameof(RuleSet.PenaltySeverity), "PENALTY LENGTHS", "PENALTIES", true, Enum.GetValues<PenaltySeverity>(), r => r.PenaltySeverity, (r, v) => r with { PenaltySeverity = v }),
        Flag(nameof(RuleSet.Interference), "INTERFERENCE", "INFRACTIONS", true, r => r.Interference, (r, v) => r with { Interference = v }),
        Flag(nameof(RuleSet.Holding), "HOLDING", "INFRACTIONS", true, r => r.Holding, (r, v) => r with { Holding = v }),
        Flag(nameof(RuleSet.HighSticking), "HIGH-STICKING", "INFRACTIONS", true, r => r.HighSticking, (r, v) => r with { HighSticking = v }),
        Flag(nameof(RuleSet.CrossChecking), "CROSS-CHECKING", "INFRACTIONS", true, r => r.CrossChecking, (r, v) => r with { CrossChecking = v }),
        Flag(nameof(RuleSet.Boarding), "BOARDING", "INFRACTIONS", true, r => r.Boarding, (r, v) => r with { Boarding = v }),
        Flag(nameof(RuleSet.Roughing), "ROUGHING", "INFRACTIONS", true, r => r.Roughing, (r, v) => r with { Roughing = v }),
        Flag(nameof(RuleSet.IcingNoChange), "NO CHANGE AFTER ICING", "GAME", true, r => r.IcingNoChange, (r, v) => r with { IcingNoChange = v }),
        Flag(nameof(RuleSet.Fights), "FIGHTS", "FIGHTS", true, r => r.Fights, (r, v) => r with { Fights = v }),
        Choice(nameof(RuleSet.FightPenalty), "FIGHT PENALTY", "FIGHTS", false, Enum.GetValues<FightPenalty>(), r => r.FightPenalty, (r, v) => r with { FightPenalty = v }),
    ];

    /// <summary>Gets the rules the game plays, grouped as the rules page shows them.</summary>
    public static IReadOnlyList<RuleRow> Shown => [.. All.Where(r => r.Plays).GroupBy(r => r.Group).SelectMany(g => g)];

    static RuleRow Flag(string key, string label, string group, bool plays, Func<RuleSet, bool> get, Func<RuleSet, bool, RuleSet> set)
    {
        return new RuleRow(key, label, group, plays, [On, Off], r => get(r) ? On : Off, (r, v) => set(r, string.Equals(v, On, StringComparison.Ordinal)));
    }

    static RuleRow Choice<T>(string key, string label, string group, bool plays, IReadOnlyList<T> values, Func<RuleSet, T> get, Func<RuleSet, T, RuleSet> set)
        where T : struct, Enum
    {
        return new RuleRow(key, label, group, plays, [.. values.Select(v => v.ToString())], r => get(r).ToString(), (r, v) => set(r, Enum.Parse<T>(v)));
    }
}
